---
name: unity-cli
description: 使用 MechStorm 项目内 Unity CLI 查询和操作当前 Unity Editor。涉及场景、对象、Prefab、资源数据库、菜单、按需刷新/编译及明确授权的测试、构建时读取；普通文案和只读源码定位不连接 Editor。
---

# Unity CLI

## 入口与边界

- CLI 为 `Tools/UnityCLI/unity.exe`，不是 Unity Editor 自身的同名程序。从本项目根执行示例，在其他目录执行时使用绝对路径。
- 先读项目 [AGENTS.md](../../../AGENTS.md) 和当前进度。修改后可按需主动刷新和编译，无需逐次确认；测试、运行游戏、构建、安装或更新仍需明确授权。
- 先发现实例，绑定本项目的绝对 `--project-path`；需要消除多实例歧义时加动态查询得到的 `--pid`，不复用历史 PID。
- 优先通过项目包装入口 `python .codex/scripts/workflow.py doctor` 和 `unity list` 确认连接与能力，再按需读 [操作规程](references/operations.md)。Python需3.11+，缺失时报告阻塞，不自动安装。
- 本项目基线为 Unity 2021.3.45f2、CLI 1.0.0-beta.16、Pipeline 0.10.0-exp.1 本地兼容分支。已实测基础连接、状态查询、场景列表及打开 Console，不代表其他功能全部兼容。

## 操作原则

- 开始新操作前确认目标工程和 Editor 状态。编译、导入、测试和写入串行；忙时只做相关状态查询，不发送新的修改命令。
- 源码用代码编辑工具修改，不用 Pipeline 文本写入、eval 或菜单绕过项目编辑及授权规则。
- 不按名字前缀判断只读。未知命令、任意菜单和 eval 先核对源码、参数和副作用，不能自动执行。
- 修改前说明目标、输入、覆盖范围、场景/资源保存影响与风险，取得明确授权；删除、覆盖、包依赖/设置、平台切换及真实构建须确认具体范围。
- 包装入口支持的修改按 `unity preview` → 用户确认 → `approve` → `unity apply` 执行。审批绑定项目、工作区、参数与预览，一小时有效、一次尝试；审批文件不能代替用户确认。白名单外的操作先核实契约，不用底层CLI绕过包装拒绝。
- 刷新/编译是上述审批规则的明确例外：可直接调用已核对的 `script recompile` 及 `script recompile_status`，无需扩展包装白名单或逐次审批。仅允许正常刷新、导入及脚本编译，不包括强制全量重新导入、清缓存、升级或其他写入；仍须绑定本项目、确认Editor空闲并查询最终结果。
- `dry-run`、`confirm` 和 Undo 不是统一保证。以当前命令描述和实现为准，命令参数不能代替用户授权。
- 请求发送后超时视作结果未知，先检查状态与实际目标，不自动重放写入或回滚用户工作。
- 未获授权不启动/关闭 Editor、不进入 Play Mode、不安装或升级工具、不改用户级配置、不提交或发布。
- 不读取或输出 Pipeline 连接描述文件、令牌及凭据。连接由 CLI 管理；不能用另一个项目的成功代替本项目验证。

## 资料与交付

以当前注册命令、源码和实际结果为准。包内文档位于 `Packages/com.unity.pipeline/Documentation~/`：

| 主题 | 文档 |
|---|---|
| 本地兼容范围 | `MechStorm-2021-Compatibility.md` |
| 连接、Player | `connectivity.md`、`runtime-setup.md` |
| 写入、路径、Undo | `safety-and-mutations.md`、`authoring-commands.md` |
| 状态、编译、测试、构建 | `commands/editor-lifecycle-and-observability.md`、`commands/build-and-compilation.md`、`testing.md` |
| 资源、对象、Prefab、场景 | `commands/assets-and-files.md`、`commands/gameobjects-and-components.md`、`commands/prefabs.md`、`commands/scenes.md` |
| 截图、设置、扩展命令 | `commands/capture.md`、`commands/project-settings.md`、`creating-commands.md` |

测试仅在授权后发现并选择相关用例，核对终态、身份、实际数量和逐项结果；零用例、全跳过或仅传输成功不算通过。
缺连接、命令、依赖或授权时明确报告阻塞。区分未运行、失败和已通过；截图不等于视觉验收。
任务进度仍使用本项目 `CURRENT_TASK.md` 与 `DEVELOPMENT_PROGRESS.md`。工作流在已忽略的 `.codex/runs/` 保存脱敏报告、预览和审批记录，不新增另一套任务历史。

本技能及Unity专用工作流于2026-11-05从TEngine-Variant增量适配；未迁移Luban/HTML领域工作流、自动编译测试、其他技能或唯一技能源规则。
