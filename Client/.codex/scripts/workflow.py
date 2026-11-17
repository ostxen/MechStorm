#!/usr/bin/env python3
"""MechStorm Unity CLI workflow. Explicit verification, no installation or publication."""

import argparse
from pathlib import Path
import sys

from workflow_lib.core import Context, Run, WorkflowError, approve, project_lock, read_json, render_report
from workflow_lib.unity import Unity


def parser():
    root = argparse.ArgumentParser(description=__doc__)
    root.add_argument("--project", type=Path, help="Must be this script's own project.")
    commands = root.add_subparsers(dest="command", required=True)
    commands.add_parser("doctor", help="Query this project's Editor; writes a local report only.")
    verify = commands.add_parser("verify", help="Run only the explicitly selected validation profile.")
    verify.add_argument("--profile", choices=("docs", "unity"), required=True)
    verify.add_argument("--test-filter", default="Unity.Pipeline.Tests.Editor")
    verify.add_argument("--filter-type", choices=("assembly", "testName", "category"), default="assembly")
    verify.add_argument("--timeout", type=int, default=300)
    ui = commands.add_parser("ui", help="Offline HTML tools only; no Unity asset writes.").add_subparsers(dest="action", required=True)
    bake = ui.add_parser("bake", help="Bake local UI-DSL HTML to JSON and a browser screenshot.")
    bake.add_argument("source", type=Path)
    bake.add_argument("--width", type=int, default=1920)
    bake.add_argument("--height", type=int, default=1080)
    bake.add_argument("--browser-channel", choices=("msedge", "chromium"), default="msedge")
    sub = commands.add_parser("unity").add_subparsers(dest="action", required=True)
    sub.add_parser("list", help="Discover all registered command names.")
    for action in ("query", "preview"):
        command = sub.add_parser(action)
        command.add_argument("name", help='Canonical name, quoted, e.g. "scene list_open".')
        command.add_argument("--params-file", type=Path)
    apply = sub.add_parser("apply")
    apply.add_argument("--plan", type=Path, required=True)
    apply.add_argument("--approval", type=Path, required=True)
    approval = commands.add_parser("approve", help="Record an actual human confirmation, not self-authorization.")
    approval.add_argument("--plan", type=Path, required=True)
    approval.add_argument("--by", required=True)
    approval.add_argument("--reason", required=True)
    approval.add_argument("--high-risk", action="store_true")
    report = commands.add_parser("report")
    report.add_argument("--run", type=Path, required=True)
    return root


def dispatch(run, args):
    if args.command == "verify":
        if args.timeout < 1 or not args.test_filter.strip():
            raise WorkflowError("Timeout and test filter must be nonempty/positive.")
        if args.profile == "docs":
            result = run.execute([sys.executable, "-B", "-m", "unittest", "discover", "-s",
                                  str(run.context.project / ".codex/tests"), "-p", "test_*.py", "-v"], timeout=args.timeout)
            if result.returncode:
                raise WorkflowError("Workflow unit tests failed; see command evidence.")
            return {"unitTests": "passed", "scope": "Isolated workflow contracts only; no real browser or Unity asset checks."}
        unity = Unity(run)
        run.step("editor-compilation", lambda: unity.recompile(args.timeout))
        if run.data["steps"][-1]["status"] != "passed":
            raise WorkflowError("Compilation did not pass; tests were not started.", "blocked")
        status = unity.ready()
        if status.get("playMode") != "stopped":
            raise WorkflowError("Verification requires Play Mode stopped; do not take over a user run.", "blocked")
        return unity.tests("editor", args.test_filter, args.filter_type, args.timeout)
    if args.command == "approve":
        return approve(run, args.plan, args.by, args.reason, args.high_risk)
    if args.command == "ui":
        from workflow_lib.ui import bake
        return bake(run, args.source, args.width, args.height, args.browser_channel)
    unity = Unity(run)
    if args.command == "doctor":
        return {"cli": str(run.context.cli), "editor": unity.ready()}
    if args.action == "list":
        return unity.discover()
    if args.action == "apply":
        return unity.apply(read_json(args.plan), args.approval)
    parameters = read_json(args.params_file) if args.params_file else {}
    method = unity.query if args.action == "query" else unity.preview
    return method(args.name, parameters)


def main(argv=None):
    args = parser().parse_args(argv)
    if sys.version_info < (3, 11):
        print("Python 3.11+ is required; do not install automatically.", file=sys.stderr)
        return 2
    try:
        context = Context(args.project)
        if args.command == "report":
            from workflow_lib.core import within
            path = within(args.run, context.runs)
            status = render_report(path)
            print(f"{status}: {path / 'report.md'}")
            return {"passed": 0, "failed": 1, "blocked": 2}[status]
        run = Run(context, " ".join(argv if argv is not None else sys.argv[1:]))
        with project_lock(context):
            entry = run.step(args.command, lambda: dispatch(run, args))
        code = run.finish()
        print(f"{run.data['status']}: {run.path / 'report.md'}")
        if entry.get("message"):
            print(entry["message"])
        elif "result" in entry:
            from workflow_lib.core import canonical
            print(canonical(entry["result"]))
        return code
    except WorkflowError as exc:
        print(str(exc), file=sys.stderr)
        return 2 if exc.status == "blocked" else 1


if __name__ == "__main__":
    raise SystemExit(main())
