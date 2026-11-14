"""Audited, project-bound Unity operations; no arbitrary command forwarding."""

import json
from pathlib import Path
import re

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


def decode_response(text):
    try:
        value = json.loads(text.lstrip("\ufeff"))
    except (ValueError, AttributeError) as exc:
        raise WorkflowError("CLI response is not JSON.") from exc
    for _ in range(6):
        if isinstance(value, str):
            try:
                value = json.loads(value)
            except ValueError:
                return value
            continue
        if not isinstance(value, dict):
            return value
        if (value.get("success") is False or value.get("Success") is False
                or value.get("errors") or value.get("error")
                or value.get("status") in ("error", "failed", "cancelled")):
            raise WorkflowError("CLI or command reported failure; see redacted evidence.")
        if "data" in value and ("success" in value or "command" in value):
            value = value["data"]
        elif "result" in value and "executionTimeMs" in value:
            value = value["result"]
        else:
            return value
    raise WorkflowError("Unexpected response nesting.")


def validate_parameters(command, parameters, mutation=False):
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


class Unity:
    def __init__(self, run):
        self.run = run
        self.context = run.context
        self.pid = None

    def raw(self, arguments, mutating=False):
        if not self.context.cli.is_file():
            blocked("Missing project CLI; do not install automatically.")
        result = self.run.execute(
            [self.context.cli, *arguments, "--format", "json", "--non-interactive", "--no-log-proxy"],
            timeout=30, mutating=mutating)
        if result.returncode:
            if result.returncode in (7, 10, 12, 14, 17, 18):
                blocked("Editor unavailable or outcome unknown; inspect evidence, do not replay writes.")
            raise WorkflowError(f"Unity CLI exited with {result.returncode}; see evidence.")
        return decode_response(result.stdout)

    def call(self, command, parameters=None, mutating=False, listing=False):
        arguments = ["command", "--project-path", str(self.context.project), "--pid", str(self.pid),
                     "--timeout", "20"]
        if not listing:
            arguments.extend(command.split())
        for key, value in (parameters or {}).items():
            arguments.extend(["--" + key, value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)])
        return self.raw(arguments, mutating)

    def ready(self, require_ready=True):
        data = self.raw(["pipeline", "editors"])
        if not isinstance(data, dict) or not isinstance(data.get("instances"), list):
            blocked("Unknown instance discovery response.")
        matches = [item for item in data["instances"] if isinstance(item, dict)
                   and item.get("projectPath") and Path(item["projectPath"]).resolve() == self.context.project
                   and item.get("isRunning") is True]
        if len(matches) != 1:
            blocked("Expected exactly one running Editor for this project.")
        instance = matches[0]
        if not instance.get("pipelineServer", {}).get("isReachable"):
            blocked("This project's Pipeline server is unreachable.")
        pid = instance.get("pid")
        if not isinstance(pid, int) or isinstance(pid, bool) or pid <= 0:
            blocked("Missing Editor PID.")
        if self.pid is not None and self.pid != pid:
            blocked("Editor restarted during the operation; create a fresh preview.")
        self.pid = pid
        status = self.call("editor status")
        if not isinstance(status, dict) or Path(status.get("projectPath", "")).resolve() != self.context.project:
            blocked("Editor status belongs to another project.")
        if require_ready and (status.get("status") != "ready" or status.get("compiling") is not False
                              or status.get("domainReloadInProgress") is not False):
            blocked("Editor is busy; no new operation was started.")
        return status

    def discover(self):
        self.ready()
        names = set()
        offset = 0
        while True:
            data = self.call("", {"offset": offset, "limit": 100, "detail": "compact"}, listing=True)
            if not isinstance(data, dict) or not isinstance(data.get("commands"), list):
                blocked("Unknown command discovery response.")
            batch = data["commands"]
            for item in batch:
                name = item.get("command")
                if not isinstance(name, str) or not re.fullmatch(r"[a-zA-Z0-9_.]+(?: [a-zA-Z0-9_]+)?", name):
                    blocked("Missing canonical command name.")
                names.add(name)
            offset += len(batch)
            total = data.get("total")
            if not isinstance(total, int) or total < offset:
                blocked("Invalid command pagination.")
            if offset == total:
                return sorted(names)
            if not batch or offset > 10000:
                blocked("Incomplete command discovery.")

    def require_command(self, command):
        if command not in self.discover():
            blocked("Command is not registered in this project: " + command)

    def query(self, command, parameters):
        validate_parameters(command, parameters)
        if command == "editor status":
            return self.ready(require_ready=False)
        self.require_command(command)
        return self.call(command, parameters)

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
