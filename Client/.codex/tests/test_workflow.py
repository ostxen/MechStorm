"""Isolated tests: never launch Unity or modify the real project's assets."""

import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.core import (
    Context, Run, WorkflowError, aggregate, approve, consume_approval,
    make_plan, project_lock, read_json, redact, validate_plan,
)
from workflow_lib.unity import Unity, decode_response, validate_parameters


class ApprovalTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.context = SimpleNamespace(project=self.root, runs=self.root / "runs", repo=self.root)
        self.fingerprint = patch("workflow_lib.core.workspace_fingerprint", return_value="tree-a")
        self.fingerprint.start()
        self.addCleanup(self.fingerprint.stop)
        self.run = Run(self.context, "isolated-test")

    def plan(self):
        result = make_plan(self.run, "unity", "editor invoke_menu",
                           {"path": "Window/General/Console"}, high_risk=True,
                           preview={"pid": 42, "result": {"staticOnly": True}})
        return Path(result["plan"]), read_json(result["plan"])

    def approval(self, path):
        result = approve(self.run, path, "test-human", "isolated test fixture only", high_risk=True)
        return Path(result["approval"])

    def test_approval_is_one_attempt_even_after_reapproval(self):
        path, plan = self.plan()
        consume_approval(self.run, plan, self.approval(path))
        with self.assertRaises(WorkflowError):
            consume_approval(self.run, plan, self.approval(path))

    def test_changed_parameters_rejected(self):
        path, plan = self.plan()
        approval = self.approval(path)
        plan["parameters"]["path"] = "File/Exit"
        with self.assertRaises(WorkflowError):
            consume_approval(self.run, plan, approval)

    def test_changed_worktree_rejected(self):
        _, plan = self.plan()
        with patch("workflow_lib.core.workspace_fingerprint", return_value="tree-b"):
            with self.assertRaises(WorkflowError):
                validate_plan(self.context, plan)

    def test_expiry_and_wrong_project_rejected(self):
        _, plan = self.plan()
        for changes in ({"expiresAt": 0}, {"project": str(self.root / "other")}):
            with self.subTest(changes=changes), self.assertRaises(WorkflowError):
                validate_plan(self.context, {**plan, **changes})

    def test_input_hash_change_rejected(self):
        source = self.root / "input.json"
        source.write_text("before", encoding="utf-8")
        result = make_plan(self.run, "unity", "asset move", {}, inputs=[source])
        plan = read_json(result["plan"])
        source.write_text("after", encoding="utf-8")
        with self.assertRaises(WorkflowError):
            validate_plan(self.context, plan)

    def test_high_risk_requires_separate_flag(self):
        path, _ = self.plan()
        with self.assertRaises(WorkflowError):
            approve(self.run, path, "human", "actual confirmation")

    def test_project_lock_is_exclusive(self):
        with project_lock(self.context):
            with self.assertRaises(WorkflowError):
                with project_lock(self.context):
                    self.fail("Lock was not exclusive")
        self.assertFalse((self.context.runs / "editor.lock").exists())

    def test_timeout_consumes_approval_and_is_not_replayed(self):
        path, plan = self.plan()
        unity = Unity(self.run)
        unity.pid = 42
        with patch.object(unity, "inspect", return_value={"staticOnly": True}), \
                patch.object(unity, "call", side_effect=WorkflowError("unknown", "blocked")) as call:
            with self.assertRaises(WorkflowError):
                unity.apply(plan, self.approval(path))
            with self.assertRaises(WorkflowError):
                unity.apply(plan, self.approval(path))
            self.assertEqual(call.call_count, 1)

    def test_execute_timeout_reports_unknown(self):
        with patch("workflow_lib.core.subprocess.run", side_effect=subprocess.TimeoutExpired("fake", 1)):
            with self.assertRaisesRegex(WorkflowError, "outcome is unknown"):
                self.run.execute(["fake"], timeout=1, mutating=True)


class PolicyTests(unittest.TestCase):
    def test_unknown_commands_and_overrides_rejected(self):
        for command, params, mutation in [
            ("script eval", {}, False), ("scene list_open", {"pid": 1}, False),
            ("asset delete", {"asset": {}, "confirm": True}, True),
            ("editor invoke_menu", {"path": "File/Exit"}, True),
            ("asset move", {"asset": "x"}, True),
            ("asset find", {"name": "--runtime"}, False),
        ]:
            with self.subTest(command=command, params=params), self.assertRaises(WorkflowError):
                validate_parameters(command, params, mutation)

    def test_current_grouped_commands_accepted(self):
        self.assertEqual(validate_parameters("scene list_open", {}), {})
        self.assertEqual(validate_parameters("editor invoke_menu", {"path": "Window/General/Console"}, True),
                         {"path": "Window/General/Console"})

    def test_business_failure_not_treated_as_transport_success(self):
        for value in ({"success": False}, {"error": "feature_unavailable"},
                      {"success": True, "data": {"Success": False}}):
            with self.assertRaises(WorkflowError):
                decode_response(json.dumps(value))

    def test_output_redaction(self):
        value = redact({"token": "secret", "data": "Bearer sensitive-value"})
        self.assertEqual(value["token"], "[REDACTED]")
        self.assertNotIn("sensitive-value", value["data"])

    def test_other_project_cannot_be_selected(self):
        with tempfile.TemporaryDirectory() as folder, self.assertRaises(WorkflowError):
            Context(Path(folder))

    def test_other_project_cannot_satisfy_discovery(self):
        with tempfile.TemporaryDirectory() as folder:
            project = Path(folder)
            unity = Unity(SimpleNamespace(context=SimpleNamespace(project=project)))
            with patch.object(unity, "raw", return_value={"instances": [
                {"projectPath": str(project / "other"), "isRunning": True, "pid": 1,
                 "pipelineServer": {"isReachable": True}}]}):
                with self.assertRaises(WorkflowError):
                    unity.ready()

    def test_missing_required_evidence_is_blocked(self):
        self.assertEqual(aggregate([]), "blocked")
        self.assertEqual(aggregate([{"status": "skipped"}]), "blocked")
        self.assertEqual(aggregate([{"status": "failed"}]), "failed")


if __name__ == "__main__":
    unittest.main()
