"""HTML-to-JSON adapter, without the source project's unavailable Unity commands."""

import importlib.util
from pathlib import Path
import sys

from .core import WorkflowError, blocked, read_json, require_success


def bake(run, source, width=1920, height=1080, browser_channel="msedge"):
    if not all(type(value) is int and 1 <= value <= 8192 for value in (width, height)):
        raise WorkflowError("Design dimensions must be integers in 1..8192.")
    if browser_channel not in ("msedge", "chromium"):
        raise WorkflowError("Unsupported browser channel; use msedge or chromium.")
    source = Path(source).resolve()
    if not source.is_file():
        blocked(f"Missing HTML input: {source}")
    if importlib.util.find_spec("playwright") is None:
        blocked("Playwright is missing in this Python environment. Installation requires explicit authorization; nothing was installed.")
    script = run.context.project / ".codex/skills/html-to-ugui/scripts/bake_html_to_json.py"
    output, screenshot = run.path / "ui.json", run.path / "browser.png"
    result = run.execute([sys.executable, "-B", script, source, "-o", output,
                          "-w", str(width), "-H", str(height), "--screenshot", screenshot,
                          "--browser-channel", browser_channel], timeout=90)
    require_success(result, "Browser UI bake (selected browser must already be installed)")
    if not output.is_file() or not screenshot.is_file() or screenshot.stat().st_size == 0:
        raise WorkflowError("Browser bake did not produce both JSON and screenshot evidence.")
    data = read_json(output)
    if not isinstance(data, dict) or not data.get("name") or data.get("width", 0) <= 0 or data.get("height", 0) <= 0:
        raise WorkflowError("Browser bake produced no valid nonzero root.")
    return {"json": str(output), "screenshot": str(screenshot), "root": data["name"], "browserChannel": browser_channel,
            "scope": "Browser JSON only; Unity scene/Prefab generation and visual acceptance are not validated."}
