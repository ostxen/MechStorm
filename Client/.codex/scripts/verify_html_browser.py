#!/usr/bin/env python3
"""Opt-in real Edge regression, separate from offline docs tests. Never installs tools."""

import base64
import importlib.util
from pathlib import Path
import shutil

from workflow_lib.core import Context, Run, WorkflowError, blocked, project_lock, write_json


def verify(run):
    if importlib.util.find_spec("playwright") is None:
        blocked("Playwright is missing; install only after explicit authorization.")
    from playwright.sync_api import sync_playwright
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, channel="msedge")
        version = browser.version
        browser.close()
    spec = importlib.util.spec_from_file_location(
        "html_baker_regression", run.context.project / ".codex/skills/html-to-ugui/scripts/bake_html_to_json.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    root = run.path / "browser fixture 中文"
    root.mkdir()
    (root / "pixel.png").write_bytes(base64.b64decode(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="))
    font = run.context.project / "Assets/AssetRaw/Fonts/SourceHanSansSC-Regular.otf"
    if not font.is_file():
        blocked(f"Missing local font fixture: {font}")
    shutil.copyfile(font, root / "fixture.otf")
    markup = """<style>@font-face{font-family:Fixture;src:url('fixture.otf')}</style>
<div data-u-type="div" data-u-name="Root" data-u-layout="stretch"
style="position:relative;width:100%;height:100%;background:#e0e4e8">
 <div data-u-type="image" data-u-name="Icon" data-u-src="pixel.png"
 style="position:absolute;left:20px;top:20px;width:96px;height:96px;background-color:#23846e"></div>
 <span data-u-type="text" data-u-name="Label"
 style="position:absolute;left:20px;top:120px;width:200px;height:40px;font-family:Fixture">机兵风暴 Workflow</span>
 <input data-u-type="slider" data-u-name="Volume" data-u-value="0"
 style="position:absolute;left:20px;top:170px;width:180px;height:30px" type="range">
 <input data-u-type="toggle" data-u-name="Sound" data-u-checked="false"
 style="position:absolute;left:20px;top:210px;width:30px;height:30px" type="checkbox">
 <div style="position:absolute;top:260px;left:20px;width:200px;height:100px">
   <div data-u-type="div" data-u-name="A" style="width:50px;height:30px"></div>
   <div data-u-type="div" data-u-name="B" style="width:50px;height:30px"></div>
 </div>
</div>"""
    source = root / "input.html"
    source.write_text(markup, encoding="utf-8")
    cases = []
    for name, width, height in (("pc", 1920, 1080), ("mobile", 1080, 1920), ("pad", 2048, 1536),
                                ("max-width", 8192, 1), ("max-height", 1, 8192)):
        print(f"Checking Edge {name}: {width}x{height}", flush=True)
        screenshot = root / (name + ".png")
        result = module.bake_html_to_json(markup, width, height, str(source), str(screenshot))
        children = {node["name"]: node for node in result["children"]}
        if (result["width"], result["height"], result["designWidth"], result["designHeight"]) != (width, height, width, height):
            raise WorkflowError("Browser root/design dimensions differ from requested dimensions.")
        if children["Volume"]["value"] != 0 or children["Sound"]["isChecked"] is not False:
            raise WorkflowError("Zero slider or unchecked toggle did not survive baking.")
        if children["Label"]["text"] != "机兵风暴 Workflow" or children["Icon"]["imageSrc"] != "pixel.png":
            raise WorkflowError("Text/image metadata differs from the input fixture.")
        groups = [node for node in result["children"] if node["name"].startswith("layoutGroup_")]
        if len(groups) != 1 or [node["name"] for node in groups[0]["children"]] != ["A", "B"]:
            raise WorkflowError("Unnamed layout group lost named child hierarchy.")
        if not screenshot.is_file() or screenshot.stat().st_size < 100:
            raise WorkflowError("Browser screenshot is missing/empty.")
        repeated = module.bake_html_to_json(markup, width, height, str(source))
        if result != repeated:
            raise WorkflowError("Repeated browser bake is nondeterministic.")
        write_json(root / (name + ".json"), result)
        cases.append({"name": name, "size": [width, height], "repeatEqual": True, "screenshot": str(screenshot)})
    rejected = []
    for name, bad_markup in (("missing-image", markup.replace("pixel.png", "missing.png")),
                              ("missing-font", markup.replace("fixture.otf", "missing.otf")),
                              ("remote-image", markup.replace("pixel.png", "https://example.invalid/pixel.png"))):
        print(f"Checking rejection: {name}", flush=True)
        try:
            module.bake_html_to_json(bad_markup, source_path=str(source))
        except ValueError as exc:
            rejected.append({"name": name, "message": str(exc)})
        else:
            raise WorkflowError(f"Invalid browser asset accepted: {name}")
    return {"browserChannel": "msedge", "browserVersion": version, "viewports": cases, "rejected": rejected,
            "font": font.name, "scope": "Real browser only; no Unity generation or device visual acceptance."}


def main():
    context = Context()
    run = Run(context, "verify-html-browser --channel msedge")
    with project_lock(context):
        run.step("edge-browser-regression", lambda: verify(run))
    code = run.finish()
    print(f"{run.data['status']}: {run.path / 'report.md'}")
    return code


if __name__ == "__main__":
    raise SystemExit(main())
