# TEV：Pipeline 1.0 的 Unity 2022 移植

这是 TEV 维护的本地修改，不是 Unity 官方支持声明。上游版本为 1.0.0，最低 Unity 6000.0；本地最低版本为 Unity 2022.3，当前目标是 2022.3.62f3。初始移植使用 CLI beta.16，后按用户单独授权升级为 1.0.1 stable；未升级引擎、Pipeline 或其他包。升级证据见仓库 `UnityProject/.codex/tasks/20261117-cli-1-0-1.md`。

## 来源与完整性

来源是本机 UnityShaderLearning-URP 已安装的正式版包，revision `ce87150fe76742893213dac8aa3fac1a3fadf577`，上游 fingerprint `178b93997022dac70383e98be82e92f4cd42de6b`，发布日 2026-11-11。这些标识仅记录上游来源，不是本地修改内容的哈希或签名。已移除旧版本的 `.signature` 和 `.attestation.p7m`，不宣称本地移植具有上游有效签名。

仅合入包源码、文档和必要 meta；没有迁入来源工程的场景、项目设置、鉴权描述文件、Library 状态或其他依赖。既有资源 meta 保留，新上游文件保留其随包 meta。

## 兼容边界

- 保留 1.0 的 `CommandContext`、结果流、退出码及命令 effect。项目示例测试快捷命令通过窄接口 `PipelineTestWorkflow` 调用正式测试实现，不公开全部内置命令。
- Console 原生计数在 2022 通过反射采样；材质继承队列读 `m_CustomRenderQueue`；物理材质使用 `PhysicMaterial`；2022 不发送新的 Editor analytics API 事件。
- 保留旧工具栏、自动 tick 的显式选择与服务器归属、测试 API 资源释放、跨 await 取消传播和旧测试框架取消适配。
- Scene Preview 的内容归档和 Build Profile 需要 Unity 6。2022 不提供这些能力；指定 Build Profile 的构建请求失败且不会排队，不能退化为普通构建。
- 2022 的单元素截图为 `panel_crop`，不是独立子树渲染。运行时从现有 panel target 裁剪，不重绘或改写业务 target；Editor 从窗口像素裁剪，恢复之前的 dock tab。窗口必须可捕获，可能包含遮挡、邻近内容或透明背景合成结果。Unity 6.7+ 保留上游独立捕获路径。
- TEV 没有安装 Input System，不增加该包依赖；模拟输入不可用。Unity 6、其他平台、真实 Player 构建与可选依赖组合不在本轮验收范围。

## 调用方迁移

`test list` 输出测试记录流，CLI beta.16 的 `--format json` 实际可能输出每行一个 JSON 对象（NDJSON），工作流将其规范化为数组，单条和空列表也单独处理。异步 `test run --async-tests` 可不输出结果，只以退出码确认已接受；必须轮询 `test status` 并核对测试身份和数量。零用例、失败、取消均不能作为通过。`script eval` 返回实际值，失败以退出码判断，不再依赖旧响应对象。

## 验证状态

最新执行证据与未完成项记录在仓库 `UnityProject/.codex/tasks/20261116-pipeline-1-2022.md`。源码编译成功不能替代 Editor 在线专项测试；上游 Unity 6 分支保留不代表已验证。测试使用隔离场景，不保存用户场景，不执行真实 Player 构建。
