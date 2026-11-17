# MechStorm Unity 2022 Pipeline 适配

当前工程为Unity 2022.3.62f3；2026-11-17从TEngine-Variant/UnityProject e1bd8715迁入Pipeline 1.0.0与CLI 1.0.1，并硬切新版调用协议，不提供旧CLI备用入口。
包最低版本2022.3是本地移植，不是上游官方支持（上游最低6000.0）。2021不再维护，`MechStorm-2021-Compatibility.md`仅保留历史证据。
repository revision/fingerprint仅表示来源；本地适配使旧签名不再有效，已按授权移除.signature与.attestation.p7m，不宣称官方签名认证。

## 迁移内容

- ConsoleWindowUtility与Material.rawRenderQueue仅在Unity6分支使用；2022使用已有LogEntries计数和序列化材质队列，解决升级后的CS0103/CS1061。
- 2022采用原生FindObjectsByType、IL2CPP生成选项、BuildReport.GetFiles、Memory及FrameTiming接口，移除仅2021需要的回退。
- 测试发现释放TestRunnerApi；取消命令绕过普通队列门控，正确取消实际运行、等待与清理，避免旧结果冒充本次测试结果。
- 命令采用CommandContext入参、ExitCode及Result.Write/WriteRecord；取消令牌在执行上下文传递并恢复，防止下一命令泄漏。同步取消先停止已记录run，再清理waiter。
- AutoTick保留显式用户选择，只释放服务器隐式请求。
- 单元素截图直接读取窗口像素；已有Panel目标只读取不重绘，保留DPI、渲染目标和窗口恢复。
- 不支持的Build Profile返回feature_unavailable，不静默忽略或排队构建。
- 源工程新版27项专项与本地8项截图/模态回归共35项；既有meta/GUID不覆盖，新增文件使用经用户授权的源meta，退役文件及meta成对删除。
- 源新版未包含本项目Windows原生模态观察分支，保留目标原实现（仅观察本进程owned窗口，不读输入控件）；Unity6.7 EditorDialogEvents分支不变。专项只自动关闭自身GUID标题的测试对话框。
- PanelSettings测试复用临时主题创建，避免默认主题资产写入磁盘；不扩大测试程序集的依赖。

## 工作流与边界

`.codex/scripts/workflow.py`增量同步连接、分页发现、编译及测试终态/身份验证，保留MechStorm项目锁、严格写入白名单和一次审批。
`verify --profile docs`运行Python隔离契约测试；`verify --profile unity --test-filter Unity.Pipeline.Tests.Editor --filter-type assembly`运行本工程编译和指定EditMode测试。
不会自动运行验证、安装、启动/关闭Editor或保存场景/设置；不迁移源Luban/HTML领域或code/full profile，目标已有HTML离线入口保留。
Unity技能及操作规程继续使用本项目授权与进度来源，不覆盖AGENTS/CLAUDE或其他开发技能。

`test list --format json`为严格NDJSON，单条/空stdout规范化数组；异步启动退出0、空stdout仅表示接受，必须检查最终逐项结果。旧失败终态只允许在启动前观察，不可冒充本次成功。同步零匹配退出2，异步零匹配启动退出0、终态status退出2。空闲cancel返回no_tests或cancelled取决于有无保留状态文件。eval直接返回实际值、失败退出6，不再使用旧success/result包装。

## 验证范围

源工程结果只作迁移依据，`local-2022-compatibility.md`是来源说明，不是目标证据。本项目2026-11-17实测：离线55/55，Unity专项35/35（0失败/跳过/不确定），编译completed且compilationFailed=false；完整Client.sln构建0错误54警告53.55秒。实际发现186条命令、性能/Console/场景查询及零匹配拒绝通过；最终ready/stopped、main未脏。详细报告见Client/DEVELOPMENT_PROGRESS.md及本地.codex/runs。
保留2022仍需要的工具栏、Analytics本地记录、panel_crop与Windows模态感知。Unity6、Player、真实构建、实时Profiler采集、活跃HTTP取消、其他图形后端及完整游戏回归未验收；Profiler命令注册/编译不等于真实采集通过。
