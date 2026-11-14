# MechStorm Unity 2022 Pipeline 适配

当前工程为Unity 2022.3.62f3；2026-11-14从TEngine-Variant/UnityProject增量迁移。
Pipeline仍为0.10.0-exp.1，CLI仍为1.0.0-beta.16；未执行工具安装、升级或更换二进制。
包最低版本2022.3，2021不再维护。`MechStorm-2021-Compatibility.md`仅保留历史证据。

## 迁移内容

- ConsoleWindowUtility与Material.rawRenderQueue仅在Unity6分支使用；2022使用已有LogEntries计数和序列化材质队列，解决升级后的CS0103/CS1061。
- 2022采用原生FindObjectsByType、IL2CPP生成选项、BuildReport.GetFiles、Memory及FrameTiming接口，移除仅2021需要的回退。
- 测试发现释放TestRunnerApi；取消命令绕过普通队列门控，正确取消实际运行、等待与清理，避免旧结果冒充本次测试结果。
- 异步命令取消令牌在CommandContext作用域传递并恢复，防止下一命令泄漏。
- AutoTick保留显式用户选择，只释放服务器隐式请求。
- 单元素截图直接读取窗口像素；已有Panel目标只读取不重绘，保留DPI、渲染目标和窗口恢复。
- 不支持的Build Profile返回feature_unavailable，不静默忽略或排队构建。
- 新增源工程26项专项用例，保留本项目原8项截图/模态回归。新源码meta交由Editor生成，既有meta/GUID不覆盖。

## 工作流与边界

`.codex/scripts/workflow.py`增量同步连接、分页发现、编译及测试终态/身份验证，保留MechStorm项目锁、严格写入白名单和一次审批。
`verify --profile docs`运行Python隔离契约测试；`verify --profile unity --test-filter Unity.Pipeline.Tests.Editor --filter-type assembly`运行本工程编译和指定EditMode测试。
不会自动运行验证、安装、启动/关闭Editor或保存场景/设置；不迁移Luban/HTML领域或code/full profile。
Unity技能及操作规程继续使用本项目授权与进度来源，不覆盖AGENTS/CLAUDE或其他开发技能。

## 验证范围

源工程结果只作迁移依据，不代表MechStorm通过。本项目实际验证见Client/DEVELOPMENT_PROGRESS.md。
保留2022仍需要的工具栏、Analytics本地记录、panel_crop与Windows模态感知。Unity6、Player、真实构建、其他图形后端及完整游戏回归不在本次验收范围。
