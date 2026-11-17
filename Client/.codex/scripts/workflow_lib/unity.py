"""Known command policies and Unity Pipeline response validation."""

from __future__ import annotations

import json
import os
from pathlib import Path
import time

from .core import WorkflowError, blocked, consume_approval, digest, make_plan


QUERIES = {
    "editor status": set(),
    "scene list_open": set(),
    "scene get_hierarchy": {"path"},
    "asset find": {"type", "name", "label", "search-in", "limit"},
    "build get_settings": set(),
}
MUTATIONS = {
    "asset delete": {"parameters": {"asset"}, "required": {"asset"}, "confirm": True},
    "asset move": {"parameters": {"asset", "destination"}, "required": {"asset", "destination"}, "confirm": False},
    "build set_settings": {"parameters": {"settings"}, "required": {"settings"}, "confirm": True},
    "editor invoke_menu": {"parameters": {"path"}, "required": {"path"}, "confirm": False},
}


def validate_policy(command, parameters, mutation=False):
    catalog = MUTATIONS if mutation else QUERIES
    if command not in catalog:
        blocked("Command is not audited for this operation: " + command)
    allowed = catalog[command]["parameters"] if mutation else catalog[command]
    if not isinstance(parameters, dict) or set(parameters) - allowed:
        blocked("Unknown or reserved parameters; target and confirmation cannot be overridden.")
    if mutation and not catalog[command]["required"].issubset(parameters):
        blocked("Required mutation parameters are missing.")
    if command == "editor invoke_menu" and parameters != {"path": "Window/General/Console"}:
        blocked("Only opening Console is audited; other menus and eval are not supported.")
    if mutation and any(value is None or value == "" for value in parameters.values()):
        blocked("Mutation inputs must be explicit and nonempty.")
    if any(isinstance(value, str) and value.startswith("-") for value in parameters.values()):
        blocked("A parameter value cannot inject CLI options.")
    return parameters


READ_ONLY = frozenset({
    "editor status", "scene list_open", "scene get_hierarchy", "build get_settings",
    "build.target list", "build list_profiles", "test list", "test status", "script recompile_status",
    "settings.player get", "settings.audio get", "settings.graphics get", "settings.input get",
    "settings.physics get", "settings.quality get", "settings.tags_layers get", "settings.time get",
    "console get", "diagnostics get_performance", "authoring get_root", "gameobject get_selection",
    "asset find", "gameobject find", "component get_properties", "component get_fields",
    "asset get_import_settings", "material get_properties", "material list_shaders", "material get_shader_properties",
    "animation.clip get", "animation.animator get_controller", "animation.timeline get", "lighting get_settings",
    "navmesh get_settings", "build status", "build.target switch_status", "package list",
    "package status", "lighting bake_status", "navmesh bake_status", "occlusion bake_status",
    "tengine_validate_assets",
})

RESERVED = {"project-path", "project_path", "runtime", "runtime-path", "runtime_path",
            "format", "json", "confirm", "dry_run", "dry-run", "non-interactive", "timeout",
            "pid", "port", "instance", "detach"}

STATUS_QUERIES = frozenset({
    "editor status", "test status", "script recompile_status", "build status", "build.target switch_status",
    "package status", "lighting bake_status", "navmesh bake_status", "occlusion bake_status",
    "console get",
})


def validate_parameters(parameters, legacy_parameters=None, mutation=False):
    if isinstance(parameters, str):
        return validate_policy(parameters, legacy_parameters, mutation)
    if not isinstance(parameters, dict):
        raise WorkflowError("Parameters must be a JSON object.")
    for key in parameters:
        if (not key or key.startswith("-") or key in RESERVED or "__" in key
                or not key.replace("_", "").replace("-", "").isalnum()):
            raise WorkflowError(f"Reserved or invalid parameter: {key}")
    return parameters


def decode_response(text):
    try:
        current = json.loads(text.lstrip("\ufeff"))
    except (ValueError, AttributeError) as exc:
        # Pipeline 1.0 streams WriteRecord results as one JSON object per line, even
        # with CLI 1.0.1's --format json. Never skip malformed lines or accept log noise.
        try:
            lines = [line for line in text.lstrip("\ufeff").splitlines() if line.strip()]
            records = [json.loads(line) for line in lines]
            if len(records) < 2 or not all(isinstance(record, dict) for record in records):
                raise ValueError("Not a record stream")
            return records
        except (ValueError, AttributeError):
            raise WorkflowError("CLI did not return a JSON response; inspect command evidence.") from exc
    # CLI envelope -> Pipeline envelope -> JSON-string status. Never search arbitrary asset data.
    for _ in range(6):
        if isinstance(current, str):
            try:
                parsed = json.loads(current)
            except ValueError:
                return current
            current = parsed
            continue
        if not isinstance(current, dict):
            return current
        if current.get("success") is False or current.get("Success") is False:
            raise WorkflowError("Command/business failure: " + json.dumps(current, ensure_ascii=False))
        if current.get("status") in ("error", "failed", "cancelled", "busy"):
            raise WorkflowError("Operation did not succeed: " + json.dumps(current, ensure_ascii=False))
        if current.get("errors") or current.get("error"):
            raise WorkflowError("Command returned errors: " + json.dumps(current.get("errors") or current.get("error"), ensure_ascii=False))
        if "data" in current and ("success" in current or "command" in current):
            current = current["data"]
        elif ("result" in current and ("command" in current or "executionTimeMs" in current)
              and not any(key in current for key in ("summary", "Summary", "statusPath", "StatusPath", "tests", "Tests"))):
            current = current["result"]
        else:
            return current
    raise WorkflowError("Unexpectedly deep response envelope.")


def parse_instances(text, project):
    data = decode_response(text)
    if not isinstance(data, dict) or not isinstance(data.get("instances"), list):
        blocked("Unknown CLI status schema; do not guess the selected project.")
    instances = [item for item in data["instances"] if isinstance(item, dict)
                 and item.get("projectPath") and Path(item["projectPath"]).resolve() == Path(project).resolve()
                 and item.get("isRunning") is True]
    if len(instances) != 1:
        blocked(f"Expected exactly one project instance; received {len(instances)}.")
    instance = instances[0]
    if not instance.get("pipelineServer", {}).get("isReachable"):
        blocked("This project's Pipeline server is unreachable.")
    pid = instance.get("pid")
    if not isinstance(pid, int) or isinstance(pid, bool) or pid <= 0:
        blocked("Missing target Editor PID.")
    return instance


def command_names(value):
    if isinstance(value, dict):
        for key in ("commands", "tools"):
            if isinstance(value.get(key), list):
                return {item.get("command") or item.get("name") for item in value[key] if isinstance(item, dict)}
        if "result" in value:
            return command_names(value["result"])
    if isinstance(value, list):
        return {item.get("command") or item.get("name") for item in value if isinstance(item, dict)}
    blocked("Unrecognized command discovery schema; inspect the registered capabilities.")


def test_listing(value, mode, query, filter_type):
    tests = value if isinstance(value, list) else value.get("tests", value.get("Tests")) if isinstance(value, dict) else None
    if not isinstance(tests, list):
        raise WorkflowError("Test listing lacks individual test identities.")
    expected_mode = {"editor": "EditMode", "playmode": "PlayMode"}[mode]
    names = []
    for test in tests:
        name = test.get("fullName", test.get("FullName", ""))
        test_mode = test.get("mode", test.get("Mode", ""))
        explicit = test.get("explicit", test.get("Explicit", False))
        categories = test.get("categories", test.get("Categories", [])) or []
        value = {"testName": name, "assembly": test.get("assembly", test.get("Assembly", "")),
                 "category": ""}[filter_type]
        matches = (any(query.lower() == category.lower() for category in categories) if filter_type == "category"
                   else query.lower() in value.lower())
        if not explicit and test_mode.lower() == expected_mode.lower() and matches:
            names.append(name)
    if not names:
        blocked(f"No runnable {mode} tests match {filter_type}={query!r}.")
    return set(names)


def validate_test_result(value, expected_names):
    if not isinstance(value, dict) or value.get("status") != "completed":
        raise WorkflowError("Tests have not reached a completed state.")
    summary = value.get("summary", {})
    fields = ("total", "passed", "failed", "skipped", "inconclusive")
    if any(not isinstance(summary.get(key), int) or summary[key] < 0 for key in fields):
        raise WorkflowError("Invalid test summary.")
    if summary["total"] == 0 or summary["passed"] == 0:
        raise WorkflowError("Zero executed/passed tests, or all tests skipped.")
    if summary["failed"] or summary["inconclusive"]:
        raise WorkflowError("Tests failed or were inconclusive.")
    if summary["total"] != sum(summary[key] for key in fields[1:]):
        raise WorkflowError("Test summary counts are inconsistent.")
    results = value.get("results", [])
    if not isinstance(results, list) or len(results) != summary["total"]:
        raise WorkflowError("Test result list/count mismatch.")
    actual_counts = {"passed": 0, "failed": 0, "skipped": 0, "inconclusive": 0}
    for item in results:
        status = str(item.get("status", item.get("Status", ""))).lower()
        if status not in actual_counts:
            raise WorkflowError("Unknown individual test outcome.")
        actual_counts[status] += 1
    if any(summary[key] != count for key, count in actual_counts.items()):
        raise WorkflowError("Individual test outcomes disagree with the summary.")
    names = {item.get("fullName", item.get("FullName", item.get("name", item.get("Name"))))
             for item in results if isinstance(item, dict)}
    if names != expected_names or summary["total"] != len(expected_names):
        raise WorkflowError("Test identities/count differ from the selected discovery set; stale or changed result.")
    return summary


class Unity:
    def __init__(self, run):
        self.run = run
        self.context = run.context
        self.capabilities = None
        self.pid = None

    def raw(self, arguments, timeout=45, mutating=False, allowed_exit_codes=()):
        if not self.context.cli.is_file():
            blocked(f"Unity CLI is missing: {self.context.cli}")
        env = dict(os.environ, UNITY_NO_UPDATE_CHECK="1", UNITY_NON_INTERACTIVE="1", UNITY_NO_PAGER="1")
        result = self.run.execute(
            [self.context.cli, *arguments, "--format", "json", "--non-interactive", "--no-log-proxy"],
            timeout=timeout, mutating=mutating, env=env,
        )
        if result.returncode and result.returncode not in allowed_exit_codes:
            # Connection errors are blockers, never a successful validation.
            if result.returncode in (7, 10, 12, 14, 17, 18) or any(
                    code in result.stdout + result.stderr for code in ("NO_INSTANCES", "UNREACHABLE", "CONNECTION", "ECONNREFUSED", "TIMEOUT",
                                                                      "502 Bad Gateway", "503 Service Unavailable")):
                blocked("Unity CLI connection unavailable; see command evidence.")
            raise WorkflowError(f"Unity CLI exited with {result.returncode}; see command evidence.")
        return result.stdout

    def ready(self, require_ready=True):
        instance = parse_instances(self.raw(["pipeline", "editors"]), self.context.project)
        if self.pid is not None and self.pid != instance["pid"]:
            blocked("Editor restarted during this operation; inspect state before a fresh request.")
        self.pid = instance["pid"]
        status = decode_response(self.raw(["command", "editor", "status", "--project-path",
                                          str(self.context.project), "--pid", str(self.pid), "--timeout", "20"]))
        if not isinstance(status, dict) or Path(status.get("projectPath", "")).resolve() != self.context.project:
            blocked("Editor status project does not match the target.")
        if require_ready and (status.get("status") != "ready" or status.get("compiling") is not False
                              or status.get("domainReloadInProgress") is not False):
            blocked("Editor is busy; no new operation was started.")
        return status

    def discover(self, require_ready=True):
        self.ready(require_ready)
        names, offset = set(), 0
        while True:
            value = decode_response(self.raw(["command", "--project-path", str(self.context.project),
                "--pid", str(self.pid), "--detail", "compact", "--offset", str(offset), "--limit", "100"]))
            if not isinstance(value, dict) or not isinstance(value.get("commands"), list):
                blocked("Unknown paginated command discovery response.")
            names.update(command_names(value))
            offset += len(value["commands"])
            total = value.get("total")
            if not isinstance(total, int) or total < offset:
                blocked("Invalid command discovery pagination.")
            if offset == total:
                break
            if not value["commands"] or offset > 10000:
                blocked("Incomplete command discovery.")
        self.capabilities = names
        return sorted(name for name in self.capabilities if name)

    def call(self, command, parameters=None, timeout=45, mutating=False, accept_retained_test_result=False):
        if self.capabilities is None:
            self.discover()
        validate_parameters(parameters or {})
        if command not in self.capabilities:
            blocked(f"Command is not registered in this project: {command}")
        arguments = ["command", "--project-path", str(self.context.project),
                     "--pid", str(self.pid), "--timeout", str(timeout), *command.split()]
        for key, value in (parameters or {}).items():
            if not command.startswith("tengine_") and "_" in key:
                raise WorkflowError("Built-in parameters must use their registered hyphenated names: " + key)
            if isinstance(value, str) and value.startswith("-"):
                blocked("Parameter values cannot inject CLI options.")
            arguments.extend(["--" + key, json.dumps(value, ensure_ascii=False) if not isinstance(value, str) else value])
        if accept_retained_test_result:
            if command != "test status":
                raise WorkflowError("Only test status may observe a retained failed result.")
            output = self.raw(arguments, timeout=timeout + 5, mutating=False, allowed_exit_codes=(2, 6, 130))
            try:
                value = json.loads(output)
            except ValueError as exc:
                raise WorkflowError("Invalid retained test status.") from exc
            if not isinstance(value, dict) or value.get("status") not in ("no_tests", "idle", "running", "completed", "cancelled", "error"):
                raise WorkflowError("Unknown retained test status.")
            return value
        output = self.raw(arguments, timeout=timeout + 5, mutating=mutating)
        # Async test run writes no records in 1.0; exit code already acknowledged the request.
        if command == "test run" and (parameters or {}).get("async-tests") is True and not output.strip():
            return None
        if command == "test list" and not output.strip():
            return []
        value = decode_response(output)
        if command == "test list" and isinstance(value, dict) and ("fullName" in value or "FullName" in value):
            return [value]
        return value

    def query(self, command, parameters):
        validate_parameters(parameters)
        if command in QUERIES:
            validate_policy(command, parameters)
        if command not in READ_ONLY:
            blocked(f"Not an audited read-only command: {command}")
        status_query = command in STATUS_QUERIES
        self.ready(require_ready=not status_query)
        if self.capabilities is None:
            self.discover(require_ready=not status_query)
        return self.call(command, parameters)

    def poll(self, command, terminal, timeout=300, interval=1):
        deadline = time.monotonic() + timeout
        errors = 0
        while time.monotonic() < deadline:
            try:
                # Domain reload may reconnect to a restarted/other instance; rebind every observation.
                self.ready(require_ready=False)
                value = self.call(command, timeout=min(15, max(1, int(deadline - time.monotonic()))))
                errors = 0
            except WorkflowError as exc:
                if exc.status != "blocked":
                    raise
                errors += 1
                if errors > 5:
                    raise
                time.sleep(interval)
                continue
            if isinstance(value, dict) and value.get("status") in terminal:
                return value
            time.sleep(interval)
        blocked(f"Timed out polling {command}. Operation may still be active; do not replay it.")

    def recompile(self, timeout):
        status = self.ready()
        if status.get("playMode") != "stopped":
            blocked("Compilation verification requires Play Mode stopped.")
        started = self.call("script recompile", mutating=True)
        if not isinstance(started, dict) or started.get("status") not in ("compiling", "up_to_date"):
            blocked("Compilation start not acknowledged; retained results cannot establish success.")
        try:
            value = self.poll("script recompile_status", {"completed", "up_to_date"}, timeout)
        except WorkflowError:
            # Import/asmdef errors can prevent compilation callbacks entirely.
            # Collect the read-only console without hiding the original incomplete operation.
            try:
                self.call("console get")
            except WorkflowError:
                pass
            raise
        if (value.get("success") is False or value.get("failed") or value.get("hasErrors")
                or value.get("compilationFailed") or value.get("errors") or value.get("errorCount", 0)):
            raise WorkflowError("Editor compilation reported errors.")
        self.ready()
        self.discover()
        logs = self.call("console get")
        return {"compilation": value, "console": logs}

    def tests(self, mode, query, filter_type, timeout):
        status = self.ready()
        if status.get("playMode") != "stopped":
            blocked("Do not take over a user Play Mode session for tests.")
        expected = test_listing(self.call("test list", {"mode": mode}), mode, query, filter_type)
        before = self.call("test status", accept_retained_test_result=True)
        if isinstance(before, dict) and before.get("status") == "running":
            blocked("Another test run is active.")
        # Pipeline 1.0 acknowledges async starts by exit code and emits no result records.
        # call raises on a nonzero exit, so no retained result can acknowledge a failed start.
        started = self.call("test run", {"mode": mode, "filter": query, "filter-type": filter_type,
                                       "async-tests": True}, mutating=True)
        if started not in (None, [], {}) and not (isinstance(started, dict) and started.get("result") == "running"):
            blocked("Async test start was not acknowledged; do not accept the last retained result.")
        value = self.poll("test status", {"completed"}, timeout)
        summary = validate_test_result(value, expected)
        return {"mode": mode, "filter": query, "summary": summary, "results": value["results"]}

    def require_command(self, command):
        if command not in self.discover():
            blocked("Command is not registered in this project: " + command)

    def inspect(self, command, parameters):
        self.require_command(command)
        status = self.ready()
        if status.get("playMode") != "stopped":
            blocked("Mutation preview/apply requires Play Mode stopped.")
        if command == "editor invoke_menu":
            return {"staticOnly": True, "path": parameters["path"], "effect": "Open/focus Console; no scene save."}
        return self.call(command, {**parameters, "dry-run": True})

    def preview(self, command, parameters):
        validate_parameters(command, parameters, mutation=True)
        preview = self.inspect(command, parameters)
        return make_plan(self.run, "unity", command, parameters, high_risk=True,
                         preview={"pid": self.pid, "result": preview})

    def apply(self, plan, approval_file):
        if plan.get("kind") != "unity" or plan.get("risk") != "high":
            blocked("Unsupported mutation plan.")
        command, parameters = plan.get("command"), plan.get("parameters")
        validate_parameters(command, parameters, mutation=True)
        from .core import validate_plan
        validate_plan(self.context, plan)
        preview = self.inspect(command, parameters)
        if digest({"pid": self.pid, "result": preview}) != digest(plan.get("preview")):
            blocked("Editor or preview changed; create and authorize a new preview.")
        consume_approval(self.run, plan, approval_file)
        options = dict(parameters)
        if command != "editor invoke_menu":
            options["dry-run"] = False
            if MUTATIONS[command]["confirm"]:
                options["confirm"] = True
        return self.call(command, options, mutating=True)
