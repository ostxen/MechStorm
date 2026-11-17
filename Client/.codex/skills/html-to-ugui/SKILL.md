---
name: html-to-ugui
description: 将本地UI-DSL HTML烘焙为JSON和浏览器截图，再交给已有Unity窗口生成UGUI原型。用于HTML转UGUI，不用于普通网页或UI Toolkit。
---

# HTML 到 UGUI（MechStorm）

## 当前实际能力

- Python脚本从TEV d3f89dd0移植，支持本地HTML、图片、字体和v2布局元数据；按用户决定适配为默认使用已安装的Microsoft Edge（`channel="msedge"`）。
- Unity端是已迁入的`HtmlToUGUIBaker`交互窗口，归默认Editor程序集。
- 源文档提及的`HtmlToUGUIBakeService`、`tengine_bake_ugui`和`tengine_validate_assets`在当前源中没有对应C#实现。本项目不提供`ui preview/apply`，不通过任意脚本绕过审批补造自动写入路径。

## 使用

1. 确认设计分辨率、本地HTML及全部图片/字体。先读[UI-DSL](references/ui-dsl-spec.md)、[JSON](references/json-schema.md)和[控件映射](references/control-mapping.md)。不是任意网页转换器，不执行用户脚本，不联网下载素材。
2. 依赖是Python 3.11+、Playwright及本机Microsoft Edge，见`requirements.txt`。Edge基于Chromium，但版本不等同Playwright配套Chromium，实际兼容性须验证。只安装Python包，不执行`playwright install`或`playwright install msedge`，不安装/更新Edge。安装必须另获授权，入口绝不自动安装。缺少Python包返回blocked；缺浏览器时保留实际失败，不伪造截图或静默换浏览器。
3. 在Client目录执行：

   ```powershell
   python .codex/scripts/workflow.py ui bake path/to/input.html --width 1920 --height 1080
   ```

   JSON、browser.png与执行报告输出到`.codex/runs/<run>/`。默认1920×1080和`--browser-channel msedge`，不更改项目横屏Canvas。若以后显式选择`--browser-channel chromium`，需事先备好该浏览器，入口仍不下载。原始脚本仍可独立运行，但其默认输出在HTML旁边，常规操作优先使用工作流入口。
4. 检查根尺寸、层级、零值控件、图片和截图。输出包含本机绝对源路径，不提交运行产物。浏览器成功不代表Unity视觉正确。
5. Unity操作前读取[unity-cli](../unity-cli/SKILL.md)。确认目标Editor空闲，列出Canvas、生成节点、同名删除、图片复制/importer变化、配置和具体Prefab保存路径，取得实际授权后再操作。
6. 人工窗口菜单为`Tools/UI Architecture/HTML to UGUI Baker (Full Controls)`。可将JSON粘贴至窗口，不必把JSON导入Assets。窗口在当前场景Canvas下生成节点，**不是隔离PreviewScene或自动Prefab保存服务**。生成、资源导入和场景/Prefab保存分别遵循项目授权边界，不执行本轮未授权的资产写入。
7. TMP字体与字形、框架UI绑定、真实宿主Canvas及各目标分辨率要分别在Unity验证。safeArea仅表达布局意图，不提供运行时安全区组件；`cover`不等同CSS裁剪填充。

## 交付与限制

报告HTML来源、设计分辨率、JSON/截图/执行报告，以及Unity生成和人工视觉验收是否实际完成。窗口对缺图可能只警告，不能称严格预检通过。不要把本工具当作不可信HTML的安全沙箱，也不要把缺CLI或缺依赖记为全链验收完成。
