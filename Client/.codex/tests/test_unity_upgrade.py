"""Offline contracts for the Pipeline 1.0 / CLI 1.0.1 migration."""

import json
from pathlib import Path
import sys
import unittest
from types import SimpleNamespace
from unittest.mock import Mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.core import Context, WorkflowError
from workflow_lib.unity import (
    READ_ONLY, MUTATIONS, STATUS_QUERIES, Unity,
    decode_response, parse_instances, test_listing, validate_parameters, validate_test_result,
)


class UnityUpgradeTests(unittest.TestCase):
    def setUp(self):
        self.context = Context()
        self.unity = Unity(SimpleNamespace(context=self.context))
        self.unity.pid = 123

    def instance(self, project=None, pid=123):
        return {"projectPath": str(project or self.context.project), "pid": pid, "isRunning": True,
                "pipelineServer": {"isReachable": True}}

    def test_cli_uses_dedicated_directory(self):
        self.assertEqual(self.context.cli, self.context.project / "Tools/UnityCLI/unity.exe")

    def test_audited_catalog_uses_only_grouped_builtin_commands(self):
        for command in READ_ONLY | set(MUTATIONS) | STATUS_QUERIES:
            if command.startswith("tengine_"):
                continue
            self.assertEqual(len(command.split()), 2, command)
        self.assertLessEqual(STATUS_QUERIES, READ_ONLY)
        self.assertNotIn("build run", MUTATIONS)

    def test_instance_discovery_filters_other_projects(self):
        value = {"instances": [self.instance(self.context.project.parent / "Other"), self.instance()]}
        self.assertEqual(parse_instances(json.dumps(value), self.context.project)["pid"], 123)

    def test_duplicate_target_instances_are_blocked(self):
        with self.assertRaises(WorkflowError):
            parse_instances(json.dumps({"instances": [self.instance(), self.instance(pid=456)]}), self.context.project)

    def test_missing_target_is_blocked(self):
        with self.assertRaises(WorkflowError):
            parse_instances(json.dumps({"instances": [self.instance(self.context.project.parent / "Other")]}), self.context.project)

    def test_unreachable_or_invalid_pid_is_blocked(self):
        instance = self.instance()
        instance["pipelineServer"]["isReachable"] = False
        with self.assertRaises(WorkflowError):
            parse_instances(json.dumps({"instances": [instance]}), self.context.project)
        for pid in (None, True, 0, "123"):
            with self.assertRaises(WorkflowError):
                parse_instances(json.dumps({"instances": [self.instance(pid=pid)]}), self.context.project)

    def test_raw_disables_update_checks_and_uses_noninteractive_output(self):
        self.unity.run.execute = Mock(return_value=SimpleNamespace(returncode=0, stdout='{}', stderr=''))
        self.assertEqual(self.unity.raw(["pipeline", "editors"]), '{}')
        call = self.unity.run.execute.call_args
        self.assertEqual(call.kwargs["env"]["UNITY_NO_UPDATE_CHECK"], "1")
        self.assertEqual(call.kwargs["env"]["UNITY_NON_INTERACTIVE"], "1")
        self.assertEqual(call.kwargs["env"]["UNITY_NO_PAGER"], "1")
        self.assertIn("--non-interactive", call.args[0])
        self.unity.run.execute.return_value.returncode = 12
        with self.assertRaises(WorkflowError) as failure:
            self.unity.raw(["pipeline", "editors"])
        self.assertEqual(failure.exception.status, "blocked")

    def test_ready_rejects_pid_change(self):
        self.unity.raw = Mock(return_value=json.dumps({"instances": [self.instance(pid=456)]}))
        with self.assertRaises(WorkflowError):
            self.unity.ready()

    def test_ready_checks_bound_status_project(self):
        self.unity.raw = Mock(side_effect=[json.dumps({"instances": [self.instance()]}),
            json.dumps({"projectPath": str(self.context.project.parent / "Other"), "status": "ready"})])
        with self.assertRaises(WorkflowError):
            self.unity.ready()

    def test_discovery_reads_all_pages_and_prefers_full_name(self):
        self.unity.ready = Mock(return_value={"playMode": "stopped"})
        self.unity.raw = Mock(side_effect=[json.dumps({"commands": [{"name": "get", "command": "console get"}], "total": 2}),
            json.dumps({"commands": [{"name": "list", "command": "test list"}], "total": 2})])
        self.assertEqual(self.unity.discover(), ["console get", "test list"])
        self.assertIn("1", self.unity.raw.call_args_list[1].args[0])

    def test_incomplete_discovery_is_blocked(self):
        self.unity.ready = Mock()
        self.unity.raw = Mock(return_value=json.dumps({"commands": [], "total": 2}))
        with self.assertRaises(WorkflowError):
            self.unity.discover()

    def test_grouped_call_passes_registered_parameter_names_unchanged(self):
        self.unity.capabilities = {"test run", "tengine_bake_ugui"}
        self.unity.raw = Mock(return_value='{"success":true}')
        self.unity.call("test run", {"filter-type": "testName", "async-tests": True})
        args = self.unity.raw.call_args.args[0]
        self.assertIn("test", args)
        self.assertIn("run", args)
        self.assertIn("--filter-type", args)
        self.assertIn("--async-tests", args)
        self.unity.call("tengine_bake_ugui", {"legacy_text": False})
        self.assertIn("--legacy_text", self.unity.raw.call_args.args[0])

    def test_old_commands_and_builtin_parameter_spellings_are_rejected(self):
        self.unity.capabilities = {"scene list_open", "test run"}
        self.unity.raw = Mock()
        self.unity.ready = Mock()
        for command in ("list_open_scenes", "editor_status", "run_tests"):
            with self.assertRaises(WorkflowError):
                self.unity.query(command, {})
            with self.assertRaises(WorkflowError):
                self.unity.call(command)
        with self.assertRaises(WorkflowError):
            self.unity.call("test run", {"filter_type": "testName"})
        self.unity.raw.assert_not_called()

    def test_reserved_target_parameters_and_option_injection_blocked(self):
        for key in ("pid", "port", "instance", "project_path", "runtime-path", "detach"):
            with self.assertRaises(WorkflowError):
                validate_parameters({key: "123"})
        self.unity.capabilities = {"asset find"}
        with self.assertRaises(WorkflowError):
            self.unity.call("asset find", {"name": "--pid"})
        with self.assertRaises(WorkflowError):
            self.unity.call("asset find", {"search_in": "Assets", "search-in": "Assets"})

    def test_new_canonical_query_retains_read_only_policy(self):
        self.unity.ready = Mock()
        self.unity.capabilities = {"scene list_open"}
        self.unity.call = Mock(return_value={"count": 1})
        self.assertEqual(self.unity.query("scene list_open", {}), {"count": 1})
        with self.assertRaises(WorkflowError):
            self.unity.query("script eval", {})

    def test_test_runner_uses_grouped_commands_and_registered_parameters(self):
        self.unity.ready = Mock(return_value={"playMode": "stopped"})
        self.unity.call = Mock(side_effect=[
            {"Tests": [{"FullName": "Example", "Mode": "EditMode"}]},
            {"status": "idle"}, {"result": "running"},
        ])
        self.unity.poll = Mock(return_value={"status": "completed",
            "summary": dict(total=1, passed=1, failed=0, skipped=0, inconclusive=0),
            "results": [{"FullName": "Example", "Status": "Passed"}]})
        self.assertEqual(self.unity.tests("editor", "Example", "testName", 5)["summary"]["passed"], 1)
        self.assertEqual([call.args[0] for call in self.unity.call.call_args_list],
                         ["test list", "test status", "test run"])
        self.assertEqual(self.unity.call.call_args.args[1],
                         {"mode": "editor", "filter": "Example", "filter-type": "testName", "async-tests": True})
        self.unity.poll.assert_called_once_with("test status", {"completed"}, 5)

    def test_async_test_payload_is_not_unwrapped_as_envelope(self):
        value = {"success": True, "result": "running", "Summary": {}, "StatusPath": "Temp/status.json"}
        self.assertEqual(decode_response(json.dumps(value)), value)

    def test_failed_compilation_is_not_accepted(self):
        self.unity.ready = Mock(return_value={"playMode": "stopped"})
        self.unity.call = Mock(return_value={"status": "compiling"})
        self.unity.poll = Mock(return_value={"status": "completed", "compilationFailed": True})
        with self.assertRaises(WorkflowError):
            self.unity.recompile(5)

    def test_verification_does_not_take_over_user_play(self):
        self.unity.ready = Mock(return_value={"playMode": "playing"})
        self.unity.call = Mock()
        with self.assertRaises(WorkflowError):
            self.unity.recompile(5)
        with self.assertRaises(WorkflowError):
            self.unity.tests("editor", "Example", "testName", 5)
        self.unity.call.assert_not_called()

    def test_zero_or_stale_tests_are_not_accepted(self):
        with self.assertRaises(WorkflowError):
            validate_test_result({"status": "completed", "summary": dict(total=0, passed=0, failed=0, skipped=0, inconclusive=0)}, {"Expected"})
        with self.assertRaises(WorkflowError):
            validate_test_result({"status": "completed", "summary": dict(total=1, passed=1, failed=0, skipped=0, inconclusive=0),
                "results": [{"FullName": "Stale", "Status": "Passed"}]}, {"Expected"})

    def test_pipeline_1_records_and_empty_async_acknowledgement(self):
        tests = [{"fullName": "Example", "mode": "EditMode", "assembly": "Example.Tests"}]
        self.assertEqual(test_listing(tests, "editor", "Example", "testName"), {"Example"})
        for acknowledgement in (None, [], {}):
            self.unity.ready = Mock(return_value={"playMode": "stopped"})
            self.unity.call = Mock(side_effect=[tests, {"status": "no_tests"}, acknowledgement])
            self.unity.poll = Mock(return_value={"status": "completed",
                "summary": dict(total=1, passed=1, failed=0, skipped=0, inconclusive=0),
                "results": [{"fullName": "Example", "status": "Passed"}]})
            self.assertEqual(self.unity.tests("editor", "Example", "testName", 5)["summary"]["passed"], 1)

    def test_pipeline_1_failed_start_never_accepts_retained_status(self):
        self.unity.ready = Mock(return_value={"playMode": "stopped"})
        self.unity.call = Mock(side_effect=[
            [{"fullName": "Example", "mode": "EditMode"}],
            {"status": "completed"}, WorkflowError("CLI exited with 6"),
        ])
        self.unity.poll = Mock()
        with self.assertRaises(WorkflowError):
            self.unity.tests("editor", "Example", "testName", 5)
        self.unity.poll.assert_not_called()

    def test_pipeline_1_empty_stdout_is_only_allowed_for_async_test_start(self):
        self.unity.capabilities = {"test run", "test list"}
        self.unity.raw = Mock(return_value=" \r\n")
        self.assertIsNone(self.unity.call("test run", {"async-tests": True}))
        with self.assertRaises(WorkflowError):
            self.unity.call("test run", {"async-tests": False})
        self.assertEqual(self.unity.call("test list"), [])

    def test_pipeline_1_real_record_stream_and_single_record(self):
        record = {"FullName": "Example", "Mode": "EditMode", "Assembly": "Example.Tests"}
        second = {**record, "FullName": "Other"}
        self.unity.capabilities = {"test list"}
        self.unity.raw = Mock(return_value=json.dumps(record) + "\n" + json.dumps(second) + "\n")
        self.assertEqual(self.unity.call("test list"), [record, second])
        self.unity.raw.return_value = json.dumps(record)
        self.assertEqual(self.unity.call("test list"), [record])
        self.unity.raw.return_value = json.dumps(record) + "\nnot-json\n"
        with self.assertRaises(WorkflowError):
            self.unity.call("test list")

    def test_failed_retained_status_does_not_block_fresh_test_run(self):
        self.unity.capabilities = {"test status"}
        self.unity.run.execute = Mock(return_value=SimpleNamespace(returncode=6,
            stdout='{"status":"completed","summary":{"failed":1}}', stderr='previous run failed'))
        self.assertEqual(self.unity.call("test status", accept_retained_test_result=True)["status"], "completed")
        with self.assertRaises(WorkflowError):
            self.unity.call("test status")
        self.unity.run.execute.return_value.returncode = 2
        self.unity.run.execute.return_value.stdout = '{"status":"completed","summary":{"total":0}}'
        self.assertEqual(self.unity.call("test status", accept_retained_test_result=True)["summary"]["total"], 0)
        with self.assertRaises(WorkflowError):
            self.unity.call("test status")
        self.unity.run.execute.return_value.returncode = 12
        with self.assertRaises(WorkflowError):
            self.unity.call("test status", accept_retained_test_result=True)


if __name__ == "__main__":
    unittest.main()
