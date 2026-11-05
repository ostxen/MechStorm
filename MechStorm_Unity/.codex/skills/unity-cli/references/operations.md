# MechStorm Unity CLI 操作规程

## 1. 发现目标与能力

项目包装入口需要Python3.11+，只使用标准库，不自动安装依赖。从本项目执行：

```powershell
python .codex/scripts/workflow.py --help
python .codex/scripts/workflow.py doctor
python .codex/scripts/workflow.py unity list
python .codex/scripts/workflow.py unity query "editor status"
python .codex/scripts/workflow.py unity query "scene list_open"
```

脚本从自身位置定位工程，`--project`只允许同一项目，不借用其他工程；项目多实例直接阻塞，需人工明确目标后再扩展支持。
`doctor`和查询会连接Editor并写本地报告，仅在当前任务允许查询时运行；不是登录、安装或自动修复。
`unity list`完整发现注册命令，但查询/写入只接受已审核白名单，并非任意透传。

以下为手动参考命令，不是自动启动任务。先确认当前用户请求允许对应操作。
从 MechStorm Unity 项目根运行；在其他目录运行时，将程序路径和项目路径都替换为本项目绝对路径。

```powershell
$project = (Resolve-Path -LiteralPath '.').Path
& './Tools/UnityCLI/unity.exe' pipeline editors --format json --non-interactive
& './Tools/UnityCLI/unity.exe' command editor status --project-path $project --format json --non-interactive --timeout 20
& './Tools/UnityCLI/unity.exe' command --query scene --limit 20 --detail compact --project-path $project --format json --non-interactive
& './Tools/UnityCLI/unity.exe' command scene list_open --project-path $project --format json --non-interactive --timeout 20
```

- 检查返回的 `projectPath`，目标必须是当前 MechStorm 工程，不能只看端口可达。
- 有多个本项目实例时先明确目标，再添加 `--pid <当前PID>`。PID由系统分配，重启后可能变化；不要把占位符原样执行。
- 先确认 `status=ready`、`compiling=false`、无域重载，再开始新操作；Play Mode状态应符合请求。
- 命令使用分组格式，例如 `editor status`、`scene list_open`。不要照搬来源技能中的 `editor_status`、`list_open_scenes` 等旧名。
- `command --help` 查询本地CLI参数；`command --query <主题>` 查询当前Editor注册能力。需要参数细节时去掉 `--detail compact`，必要时分页。

## 2. 编辑器修改与授权

任务内按需刷新/编译按第3节直接执行，不需预览/审批；以下审批流程适用于其他编辑器写入。

说明具体项目、对象/资源路径、参数、覆盖及保存影响，确认后只执行该范围。包装入口已迁入，操作参数通过JSON文件提供，避免PowerShell转义差异。
以下以打开Console演示；先准备参数文件 `{"path":"Window/General/Console"}`。路径占位符需替换为实际输出，不可原样执行：

```powershell
python .codex/scripts/workflow.py unity preview "editor invoke_menu" --params-file console-request.json
# 向用户展示preview.json，取得针对本次目标和参数的真实确认后才执行下一步。
python .codex/scripts/workflow.py approve --plan <preview.json> --by <确认者> --reason <真实确认引用> --high-risk
python .codex/scripts/workflow.py unity apply --plan <preview.json> --approval <approval.json>
python .codex/scripts/workflow.py report --run <运行目录>
```

预览不会生成用户授权；代理不得自行编造确认者或确认依据。Console没有dry-run，包装只记录静态窗口影响并明确`staticOnly=true`，不会在预览时打开窗口。
其余已审核修改使用命令真实的`dry-run`。apply前重新检查Editor PID、预览、工作区与输入参数，变化或过期需重新预览确认；审批在实际发送前消耗，失败/超时也不自动重放，同一预览重复审批不能绕过一次尝试限制。
本入口的修改均要求`--high-risk`作为保守的单独确认标记，不表示所有操作具有相同实际风险。记录防误操作，不是防恶意本地用户篡改的权限系统。

| 类型 | 当前支持 |
|---|---|
| 查询 | `editor status`、`scene list_open`、`scene get_hierarchy`、`asset find`、`build get_settings` |
| 预览/审批/执行 | `asset delete`、`asset move`、`build set_settings`、仅打开Console的`editor invoke_menu` |

白名单基于当前包源码契约适配，不代表每个写入已真实运行验收。构建、测试、包设置、任意菜单、eval和自定义烘焙不透传；需要时先按任务审核实现与结果契约，再补策略和测试，不用底层CLI绕过拒绝。
预览/审批只绑定磁盘工作区与该命令的预览结果，不检测所有未保存Editor状态，也不能恢复用户数据。设置操作前仍应检查潜在的全局保存影响。

下面的菜单命令已在本项目实测，但只在用户要求对应窗口操作时执行：

```powershell
& './Tools/UnityCLI/unity.exe' command editor invoke_menu --path 'Window/General/Console' --project-path $project --format json --non-interactive --timeout 20
```

打开Console不代表任意菜单都安全。禁止未经授权调用保存、退出、重新导入、清理等菜单，也不通过eval绕过权限或代码编辑规则。
某些资源/设置命令可能保存其他脏资源；执行前检查真实实现与工作区状态，不能把单个字段修改扩大成全局保存。
场景与Prefab序列化操作优先通过Editor能力，不能直接编辑YAML、GUID或meta代替正常资源操作。meta例外按项目AGENTS执行。

## 3. 验证选择

- 修改后可按需主动刷新和编译，无需逐次确认。包装尚未开放此入口时，可直接调用已核对的 `script recompile`，再查询 `script recompile_status` 和 `editor status`；这是明确允许的例外，不扩大到其他白名单外写入。
- `script recompile` 会调用AssetDatabase.Refresh，导入待处理磁盘改动并按需编译/域重载。先绑定本项目当前PID，确认Editor空闲且Play Mode停止，串行发送一次请求；重载期间允许短暂连接中断，只查询状态，不重复发送。最终核对status、failed、errors及compilationFailed；up_to_date表示本次未触发新编译，不能当作强制重编通过。
- 可用的命令格式为 `unity command script recompile --project-path <本项目绝对路径> --pid <当前PID>`，随后将 `script recompile` 替换为 `script recompile_status` 查询。程序须使用本项目Tools/UnityCLI/unity.exe，遵守第1节路径、JSON及非交互参数规范。无需主动聚焦Editor；不自动启动/关闭Editor，不强制全量重新导入或清缓存。
- 测试、Play Mode、截图和构建仍仅在明确授权范围内执行，不因新增/修改代码自动运行。
- 执行前查当前命令及包内文档，不假定来源项目自定义的 `tengine_validate_assets` 或 `tengine_bake_ugui` 已存在。
- 测试先发现相关用例，再分别执行授权的EditMode/PlayMode范围；若异步返回，确认启动响应并查询最终状态，不重复发送启动命令。
- 核对测试名称/程序集、实际执行数、失败、跳过与汇总；零用例或全部跳过不能算通过。
- 记录真实退出码和业务结果。CLI成功并不自动代表构建、测试或生成成功；未运行项写清原因。
- 截图可能写文件或改变窗口状态，先确认输出路径和影响；按包内当前capture文档检查Overlay UI支持，不复制旧版渲染限制当作事实。
- 浏览器图片、Unity截图与设备视觉验收分别记录；一种不能代替另一种。

## 4. 连接失败与结果未知

- Editor不可达时报告阻塞，请开发者确认本项目与Pipeline服务，不自动安装、升级或连接其他项目兜底。
- 忙操作只查询状态；读取可能有限重试，写入超时先查实际结果，禁止无条件重放。
- 不读取或打印 `Library/Pipeline` 连接描述文件，其内容可能包含鉴权信息。
- Player使用明确的 `--runtime-path` 并核对目标，不能以Player验证替代Editor验收。本项目Player功能未实测。
- Unity6专有能力可能明确返回不可用，不能伪造结果或修改ProjectVersion来绕过。

## 5. 证据与维护

当前已验证基础连接、`editor status`、`scene list_open`、打开Console及一次`script recompile`/`script recompile_status`调用；该次返回up_to_date、无编译失败，未触发新编译。其他命令需按任务验证。
工作流输出位于已忽略的 `.codex/runs/<run-id>/`，包含`report.json`、`report.md`，预览与审批分别输出`preview.json`、`approval.json`。`0=passed`、`1=failed`、`2=blocked`；中断或缺证据不能靠report升级为通过。
同项目操作通过锁串行，异常退出若遗留`editor.lock`先确认进程与在途请求，不自动删除锁。Python隔离测试命令为`python -B -m unittest discover -s .codex/tests -p "test_*.py" -v`，不启动Unity，不在技能触发时自动运行。
新增验证结果写入现有任务工作台，正式验收同步开发进度，不提交令牌、凭据或敏感业务数据。
CLI/Pipeline版本变化后重新发现能力；本地兼容包不能直接用install/upgrade覆盖。
