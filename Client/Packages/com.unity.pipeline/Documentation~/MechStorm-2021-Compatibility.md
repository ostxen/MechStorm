# MechStorm Unity 2021.3 兼容说明

## 来源

- 官方包：com.unity.pipeline 0.10.0-exp.1，revision 8c52ede3f9f3514111961927c9688a70a6322309。
- 下载：https://download.packages.unity.com/com.unity.pipeline/-/com.unity.pipeline-0.10.0-exp.1.tgz。
- 原始包 SHA-1：303cb578aa664fd6f4c220c3d85bb2ba6ca78d0b，与官方 registry dist.shasum 一致。
- 官方要求 Unity 6000.0；本地兼容目标为2021.3.45f2，不属于官方支持版本。保留原始许可证和包内资源GUID。

## 本地修改

- 兼容目标是尽量保留上游正常版本的功能，优先使用旧版等价API或局部适配，不因编辑器版本较旧直接裁剪。真正依赖新版引擎或未安装扩展包的能力明确报告限制；源码存在不代表已完成实际运行验收。
- package.json 最低版本改为2021.3，显示名保持Unity Pipeline，版本保留上游0.10.0-exp.1。
- 主工具栏在2021.3通过包内PipelineToolbar接入ToolbarZoneRightAlign，显示Pipeline Server、红绿状态图标和运行端口提示；点击复用现有Start/Stop逻辑，与Window/Pipeline菜单同步。Unity6.3+保留原生MainToolbar入口。布局重建时重新挂接，域重载/退出时移除控件和事件；不依赖项目Assets下的ToolbarExtender，不修改现有工具栏源码。
- 旧工具栏使用UnityEditor.Toolbar.m_Root内部接口，版本变化造成接口缺失时只警告一次并保留菜单和服务，不持续抛错；不提供Unity6.3原生工具栏的元素选择器/拖动定制能力。具体显示位置和窄窗口布局仍需实际Editor检查。
- 旧版对象查询使用FindObjectsOfType；其结果按Unity旧接口排序。
- 内存快照使用UnityEngine.Profiling.Memory.Experimental。
- PhysicsMaterial映射到旧名PhysicMaterial；材质原始队列从序列化字段读取。
- IL2CPP代码生成设置使用EditorUserBuildSettings；构建文件使用BuildReport.files。
- Console计数通过2021内部LogEntries.GetCountsByType取得，保留定时采样，不依赖新版Console事件。
- Build Profile Undo事件仅Unity6启用，原有Build Profile命令在旧版仍明确报告feature_unavailable。
- 缺失的主线程帧耗时返回null，不用0冒充有效数据。
- 新IAnalytic接口在旧版使用包内本地记录契约，不向Unity发送分析事件。
- 测试回调注销采用TestRunnerApi.UnregisterCallbacks，明确测试程序集、NUnit和插件引用。
- CompilationPipeline使用全限定名，避免与Unity.CompilationPipeline命名空间冲突。
- 经开发者授权，将Runtime/Plugins/CodeAnalysis下五份DLL meta的PluginImporter v3字典格式转换为v2平台列表格式，兼容2021导入器；保留GUID、宏约束与平台状态，明确保留WindowsStoreApps禁用。DLL内容与解释器代码不变。实际导入前的Bee编译响应缺少这些插件，造成ILOpCode/MetadataReader等连锁缺失。
- 当前项目未安装Input System，移除无条件程序集引用，不改变项目输入模式。输入注入仍由上游条件编译返回缺少Input System的信息；将来安装该包时须恢复Unity.InputSystem引用。
- 单元素截图命令已开放：复用上游选择器，Editor通过窗口像素裁剪，Runtime通过UIDocument现有面板绘制后裁剪；返回captureMode=panel_crop，路径与base64契约保留。包含同一区域的面板背景、重叠兄弟节点与裁剪，不是独立子树渲染，不显示滚动区域外内容，不包含其他Runtime面板或最终游戏合成。处理DPI及纹理坐标方向，恢复活动RenderTexture/Camera/矩阵，不改PanelSettings资产或移动元素。Unity6000.7+保留原生element_render，PanelRenderer仅在对应新版编译。
- 窗口截图不再强制加载2021不存在的BlitSceneViewCapture.mat；资源不存在时用旧版Blit并按图形API处理上下方向。单元素截图复用明确窗口实例，避免同类型多窗口重新解析到别处。
- Windows旧版弹窗感知通过250ms后台轮询本Editor进程的可见模态窗口，筛选同进程且已禁用的owner，写入现有Dialogs/busy gate，source=WindowsNative；不依赖被阻塞的Editor.update，不自动关闭用户弹窗。只限时读取标题、Static文本和Button标签，不读输入框。文字可能为空，level未知，短弹窗可能漏报；状态来自最近空闲快照，非引擎显示前事件。文件选择器/自绘模态及其他操作系统不保证支持，不能宣称等价EditorDialogEvents。重载/退出停止轮询并清理该观察器登记状态。

## 安装与验证边界

- 包作为Packages/com.unity.pipeline内嵌包安装，Unity直接发现该目录，无需改manifest为registry版本。
- com.unity.nuget.mono-cecil 1.11.6由包依赖声明；其最低Unity2019.4，支持当前编辑器。Test Framework1.1.33与Newtonsoft3.2.1项目已有，不降级。
- CLI pipeline install会在包处理前拒绝2021.3；不要伪造ProjectVersion或再次用install/upgrade覆盖本地适配。
- 已用Unity自带Roslyn、本机程序集、项目.NET4.8引用与脚本宏进行五个包程序集离线编译，退出码均0。仅包级检查，未启动Editor、刷新项目、进入Play Mode、构建游戏或运行游戏测试。
- 初次连接曾不可达；插件导入修复后，开发者确认Console不再报错，CLI已对本项目实际通过pipeline editors、editor status、scene list_open和打开Console菜单。后续Python工作流的项目/PID绑定、预览、用户审批和一次Console执行也通过。此处覆盖首次安装的不可达状态，不代表全部命令验收。新增源码的meta由Unity生成，不手写。
- 首次导入失败后，使用实际Unity编译响应文件的临时副本，补齐Metadata、Immutable与Unsafe插件引用，IlInterpreter编译退出0；五份meta随后按授权修复并由开发者确认无错误。Cecil1.11.6已在实际UPM缓存与Bee引用中确认。
- 旧版工具栏最初仅做静态检查，后续已随本项目正常刷新编译，无编译失败；按钮显示/端口提示、启停与菜单同步、布局及重载无重复等真实交互仍未专门验收。Unity6.3+分支未做实际编译或运行验证。
- 2026-11-05弹窗/单元素截图适配实际编译完成，failed=false、errors=[]、compilationFailed=false。新增Assets/Tests/EditorMode/Pipeline程序集，仅运行该程序集：最终8/8通过，零失败/跳过；覆盖选择器、DPI/越界坐标、脱离面板、Editor/Runtime PNG尺寸和颜色、非对称方向标记、渲染目标恢复、Play Mode捕获，以及真实阻塞模态弹窗的后台观察与关闭后清理。首轮7项中1项因窗口材质缺失失败，修复后通过，再扩展为8项；未屏蔽错误或放宽断言。
- 测试截图保存在Temp/pipeline-screenshots，测试只自动关闭唯一标题的自有弹窗，临时窗口/对象已清理。最终Editor ready、Play Mode停止，main.unity仍isDirty=false、rootCount=3；未保存场景、构建、安装包或开启Player服务。meta由Unity正常生成。独立Player、其他图形API、高DPI多屏实机及Unity6原生分支仍未验证。
- 已检查相邻版本分支：预览导入使用旧版instance ID/文件输出，Runtime构建处理器使用IPreprocessBuildWithReport，Profiler保存使用旧版void返回适配，均保留原功能路径；Build Profiles和MPPM场景依赖新版引擎/扩展，不伪造旧版实现。本轮没有逐条验证其他命令。
- 上游默认在Editor加载时启动本地Pipeline服务，保留回环地址绑定、令牌校验和浏览器来源限制；未开启远程网络访问。
- 当前项目未设置ENABLE_RUNTIME_PIPELINE或ENABLE_RUNTIME_PIPELINE_DEV，本任务未开启Player服务。上游Runtime相关功能未做Player验证；Editor接入不代表热重载、截图、测试、构建等所有命令已运行验收。
