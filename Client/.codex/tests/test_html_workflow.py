"""Offline contracts only. No browser installation or Unity asset writes."""

import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

PROJECT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PROJECT / ".codex/scripts"))
import workflow
from workflow_lib.core import Run, WorkflowError
from workflow_lib.ui import bake

spec = importlib.util.spec_from_file_location(
    "html_baker_contracts", PROJECT / ".codex/skills/html-to-ugui/scripts/bake_html_to_json.py")
baker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baker)


class MarkupContracts(unittest.TestCase):
    def test_active_markup_is_rejected_before_browser_import(self):
        for markup in ('<script>alert(1)</script>', '<iframe></iframe>', '<object/>',
                       '<embed/>', '<base href="https://example.invalid">', '<link/>',
                       '<meta http-equiv="refresh">', '<div onclick="alert(1)">',
                       '<a href="javascript:alert(1)">'):
            with self.subTest(markup=markup), self.assertRaises(ValueError):
                baker.bake_html_to_json(markup)

    def test_invalid_dimensions_are_rejected_before_browser_import(self):
        for width, height in ((0, 1080), (1920, -1), (8193, 1080), (1920, 8193), (1.5, 1)):
            with self.subTest(size=(width, height)), self.assertRaises(ValueError):
                baker.bake_html_to_json("", width, height)

    def test_local_dsl_is_accepted_by_markup_parser(self):
        baker.SafeMarkup().feed('<div data-u-type="div" data-u-name="m_root">'
                                '<img src="images/local.png"><style>div {color:red}</style></div>')


class BakeContracts(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.source = self.root / "input with spaces.html"
        self.source.write_text('<div data-u-type="div" data-u-name="m_root"></div>', encoding="utf-8")
        self.run = Run(SimpleNamespace(project=PROJECT, runs=self.root / "runs"), "ui-test")

    def execute(self, args, **kwargs):
        self.args = args
        Path(args[args.index("-o") + 1]).write_text(json.dumps({
            "name": "m_root", "width": 1920, "height": 1080}), encoding="utf-8")
        Path(args[args.index("--screenshot") + 1]).write_bytes(b"fixture-not-a-real-browser-image")
        return subprocess.CompletedProcess(args, 0, "fixture", "")

    def test_adapter_places_outputs_only_in_run_and_never_calls_unity(self):
        with patch("workflow_lib.ui.importlib.util.find_spec", return_value=object()), \
                patch.object(self.run, "execute", side_effect=self.execute), \
                patch("workflow.Unity", side_effect=AssertionError("Must not create Unity adapter")):
            result = workflow.dispatch(self.run, workflow.parser().parse_args(["ui", "bake", str(self.source)]))
        self.assertEqual(Path(result["json"]), self.run.path / "ui.json")
        self.assertEqual(Path(result["screenshot"]), self.run.path / "browser.png")
        self.assertEqual(result["root"], "m_root")
        self.assertIn(str(self.source), [str(arg) for arg in self.args])
        self.assertFalse(self.source.with_suffix(".json").exists())
        self.assertIn("not validated", result["scope"])
        self.assertEqual(result["browserChannel"], "msedge")
        self.assertEqual(self.args[self.args.index("--browser-channel") + 1], "msedge")

    def test_missing_dependency_blocks_without_execution_or_installation(self):
        with patch("workflow_lib.ui.importlib.util.find_spec", return_value=None), \
                patch.object(self.run, "execute") as execute:
            with self.assertRaises(WorkflowError) as raised:
                bake(self.run, self.source)
            self.assertEqual(raised.exception.status, "blocked")
            execute.assert_not_called()

    def test_missing_source_blocks_before_dependency_check(self):
        with patch("workflow_lib.ui.importlib.util.find_spec") as dependency:
            with self.assertRaises(WorkflowError) as raised:
                bake(self.run, self.root / "absent.html")
            self.assertEqual(raised.exception.status, "blocked")
            dependency.assert_not_called()

    def test_invalid_sizes_do_not_launch_child(self):
        for size in ((0, 1), (1, 8193), (True, 1), (1.5, 1)):
            with self.subTest(size=size), patch.object(self.run, "execute") as execute:
                with self.assertRaises(WorkflowError):
                    bake(self.run, self.source, *size)
                execute.assert_not_called()

    def test_nonzero_child_exit_is_not_success(self):
        with patch("workflow_lib.ui.importlib.util.find_spec", return_value=object()), \
                patch.object(self.run, "execute", return_value=subprocess.CompletedProcess([], 1, "", "Edge missing")):
            with self.assertRaisesRegex(WorkflowError, "selected browser must already be installed"):
                bake(self.run, self.source)

    def test_no_outputs_or_missing_screenshot_is_not_success(self):
        def json_only(args, **kwargs):
            Path(args[args.index("-o") + 1]).write_text("{}", encoding="utf-8")
            return subprocess.CompletedProcess(args, 0, "", "")
        for action in (lambda *args, **kwargs: subprocess.CompletedProcess([], 0, "", ""), json_only):
            with self.subTest(action=action), patch("workflow_lib.ui.importlib.util.find_spec", return_value=object()), \
                    patch.object(self.run, "execute", side_effect=action):
                with self.assertRaisesRegex(WorkflowError, "both JSON and screenshot"):
                    bake(self.run, self.source)

    def test_zero_size_root_is_not_success(self):
        def zero_root(args, **kwargs):
            result = self.execute(args, **kwargs)
            Path(args[args.index("-o") + 1]).write_text('{"name":"root","width":0,"height":1}', encoding="utf-8")
            return result
        with patch("workflow_lib.ui.importlib.util.find_spec", return_value=object()), \
                patch.object(self.run, "execute", side_effect=zero_root):
            with self.assertRaisesRegex(WorkflowError, "nonzero root"):
                bake(self.run, self.source)

    def test_unsupported_asset_write_actions_remain_absent(self):
        for action in ("preview", "apply"):
            with self.subTest(action=action), patch("sys.stderr"), self.assertRaises(SystemExit):
                workflow.parser().parse_args(["ui", action])

    def test_boundary_sizes_are_forwarded_unchanged(self):
        with patch("workflow_lib.ui.importlib.util.find_spec", return_value=object()), \
                patch.object(self.run, "execute", side_effect=self.execute):
            bake(self.run, self.source, 1, 8192)
        self.assertEqual(self.args[self.args.index("-w") + 1], "1")
        self.assertEqual(self.args[self.args.index("-H") + 1], "8192")

    def test_explicit_chromium_channel_is_forwarded(self):
        with patch("workflow_lib.ui.importlib.util.find_spec", return_value=object()), \
                patch.object(self.run, "execute", side_effect=self.execute):
            result = bake(self.run, self.source, browser_channel="chromium")
        self.assertEqual(result["browserChannel"], "chromium")
        self.assertEqual(self.args[self.args.index("--browser-channel") + 1], "chromium")

    def test_invalid_channel_does_not_launch(self):
        with patch.object(self.run, "execute") as execute, self.assertRaises(WorkflowError):
            bake(self.run, self.source, browser_channel="unsupported")
        execute.assert_not_called()


if __name__ == "__main__":
    unittest.main()
