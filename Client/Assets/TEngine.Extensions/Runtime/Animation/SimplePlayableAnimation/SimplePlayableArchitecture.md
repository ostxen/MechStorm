# SimplePlayableAnimation 架构与使用约定

更新日期：2026-10-07。

本文是正式轻量动画模块的架构入口，合并原审查记录中仍适用的设计决策、使用约束和验证边界。
正式实现位于 `Assets/TEngine.Extensions/Runtime/Animation/SimplePlayableAnimation`，
公开命名空间保持 `TEngine.Extensions.SimpleAnimation`。下文 `Assets/`、`Packages/` 路径均相对于 Unity 工程根。
教学代码只作为学习对照，不作为正式 API 或功能完成度的依据。

## 1. 定位与参考来源

目标是通过稳定的动画 Key 驱动 Clip、层内过渡和小规模参数混合，
让业务不必直接管理 Playable 连接、事件游标和资源释放。
不复制完整 Animancer，不实现 Animator Controller 兼容层、Timeline、FSM、DOTS 后端、
完整 BlendTree Inspector 或 3D 四面体化工具链。

### 1.1 参考链接

| 来源 | 参考范围 |
|---|---|
| [gmhevinci/UniFramework](https://github.com/gmhevinci/UniFramework) | UniAnimation 的宿主、状态门面、Layer 和节点契约；不整体引入框架 |
| [【咕咕のUnity学习】Playable 动画系统](https://www.bilibili.com/video/BV1SP4y177YQ/) | 用户指定的动画系统教学参考 |
| [【Unity学习之路】一小时速通Playable](https://www.bilibili.com/video/BV1Er421t7vT/) | 用户指定的 Playable 基础教学参考 |
| [Unity PlayableGraph Monitor](https://github.com/SolarianZ/UnityPlayableGraphMonitorTool) | 已接入的 Editor Graph 可视化工具，不自研第二套拓扑画布 |

2026-10-07 核对了 UniFramework 仓库首页及两段视频的页面标题、简介。
视频使用去除追踪参数的固定 BV 链接；页面读取不等于完整观看或逐段核验视频代码。
UniFramework 首页声明仅供源码级学习、不做长期版本维护，许可证标识为 Apache-2.0。
外部参考不能替代本项目源码和测试，也不构成复制或再分发视频、插件源码的授权。

二维算法另参考项目内已购买的 Animancer 8.4.0：

- `Packages/com.kybernetik.animancer/Runtime/Mixer States/CartesianMixerState.cs`
- `Packages/com.kybernetik.animancer/Runtime/Mixer States/DirectionalMixerState.cs`

这里只适配 Gradient Band / Polar Gradient Band 数学核心，不依赖 Animancer 程序集，
不移植其状态、同步、参数绑定和编辑器体系。原插件采用 Unity Asset Store EULA；
购买和读取源码不等同于允许任意再分发，提交范围不包含原插件包。

### 1.2 借鉴与取舍

- 借鉴 UniAnimation 的“宿主 → 状态门面 → Layer Mixer → Clip/Mixer 节点”分层。
- 保留自己的 Provider、Lease、FadeGroup、Selection 和 Catalog；不替换 TEngine 的资源、事件、对象池或更新系统。
- 使用稳定的 `AnimationKey`，不以 `clip.name`、资源路径或 GUID 作为业务播放标识。
- Loop/Once、速度和重播配置保存在运行节点中，不通过修改共享 Clip 的 `wrapMode` 实现。
- 初始化不使用 `float.MaxValue` 推进时间；采样前更新混合权重，采样后处理事件。
- 普通节点直接持有 Clip/Mixer Playable；仅确实需要 Graph 回调的扩展使用 ScriptPlayable。

## 2. 目录、程序集与职责

```text
Assets/TEngine.Extensions/
├── Runtime/Animation/SimplePlayableAnimation/
│   ├── Core/
│   │   ├── PlayableAnimationGraph.cs
│   │   ├── AnimationResetContext.cs
│   │   └── Node/                 Playable、Clip、Mixer、LayerMixer 节点
│   ├── Playback/                 Controller、Clip/Tree Handle、PlaybackOptions、LayerSettings
│   ├── Blending/
│   │   ├── AnimationFadeGroup.cs
│   │   ├── Trees/                1D/2D 树、Definition 与成对 Sample
│   │   └── Weights/              Cartesian/Directional 算法、校验与内部缓存
│   ├── Selection/                ISelectionStrategy、Strategies、Nodes
│   ├── Sequences/                AnimationSequenceNode
│   ├── Events/                   Marker、Dispatcher、EventQueue
│   ├── Providers/                Library、Catalog、Provider、Lease、YooAsset 实现
│   ├── Extensions/
│   │   ├── Behaviours/           可选 ScriptPlayable 回调节点
│   │   ├── Jobs/                 Animation Job 与骨骼偏移示例
│   │   └── Compute/              可选 GPU 权重计算器
│   └── Diagnostics/              AnimationSnapshot
├── Editor/Animation/
│   ├── AnimationClipCollectionEditors.cs
│   ├── AnimationClipCollectionValidator.cs
│   └── PlayableGraphMonitorAdapter.cs
└── Tests/PlaybackRegression/
    ├── PlaybackContractTests.cs
    └── BlendWeightCalculatorTests.cs
```

运行时和编辑器沿用 `TEngine.Extensions.Runtime`、`TEngine.Extensions.Editor`；
测试使用独立的 `TEngine.Extensions.PlaybackRegression.Tests` 程序集。
Core 的节点和 Graph 不直接加载资源，但当前 Runtime 程序集包含 YooAsset Provider，
不能把“Core 职责资源无关”理解成整个程序集没有 YooAsset 依赖。
Monitor 依赖只在 Editor 侧，`TEngine.Runtime` 不反向依赖本模块。

## 3. Graph、节点与宿主

```text
PlayableAnimationController
├── Library / Catalog → Provider → Lease
├── Clip Handle / BlendTree Handle
├── EventQueue
└── PlayableAnimationGraph → Animator
    └── AnimationLayerMixerNode
        ├── Layer 0: AnimationMixerNode
        ├── Layer 1: AnimationMixerNode
        └── ...（最多 4 层）
            ├── AnimationClipNode
            └── AnimationBlendTree → 独立 Clip 叶子或嵌套树
```

- Controller 管理绑定、层配置、状态查询、生命周期和事件派发，通过 `[RequireComponent(typeof(Animator))]` 声明必需的 Animator 组件。
- Graph 拥有根节点和 AnimationPlayableOutput，替换根节点先释放旧树；Dispose 先释放节点再销毁 Graph。
- 父节点拥有子节点；外部不能擅自 Dispose 或断开 Controller 拥有的 Root/ChildNodes。
- Clip 节点包装 `AnimationClipPlayable`；Mixer 和树最终由 `AnimationMixerPlayable` 混合姿势。
- 树的继承关系是 `AnimationPlayableNode → AnimationMixerNode → AnimationBlendTree → 1D/2D`，
  不是继承 Clip，也不是包装 Animator BlendTree 资产。

Controller 使用 Manual Graph，在自身 `Update` 中以 `Time.deltaTime` 驱动：

```text
节点 Update：推进 Fade、权重和序列
    ↓
PlayableGraph.Evaluate
    ↓
节点 PostUpdate：检查 Marker 和完成状态
    ↓
EventQueue.Flush：执行业务回调
```

不要在保留 Controller 自动 Update 的同时，再从业务重复驱动 Evaluate。
`AnimationPlayableBehaviourNode<TBehaviour>` 仅用于必须参与 Graph 回调顺序的场景，
不为普通 Clip、Fade 或 Selector 增加通用 Adapter。

## 4. 播放契约

### 4.1 Key、配置与句柄

- Clip Key 和树 Key 在同一 Controller 内共用唯一注册空间，不支持同 Key 跨层克隆。
- `AnimationPlaybackOptions` / `AnimationPlaybackOptionsSet` 是可复用 Clip 播放配置，不是状态机连线。
- 配置含 Key、FadeDuration、Speed、WrapMode、RestartOnPlay 和可选 NormalizedStartTime。
  非有限参数和非法模式会被拒绝；当前速度只接受有限非负值。
- `Play(key, fadeDuration)` 支持 Clip 和树；未知 Key 返回 false，负过渡时长使用默认值。
- `Play(AnimationPlaybackOptions)` 仅支持 Clip；树 Key 抛出 `NotSupportedException`。
- `States` / `GetState` 获取 Clip，`BlendTrees` / `GetBlendTree` 获取树。
  重绑、Reset、禁用或销毁后旧句柄失效，使用前检查 `IsValid`。
  失效 Clip 句柄的时间、速度、长度和权重访问抛出 `ObjectDisposedException`。
- `TryGetCurrentState`、`TryGetCurrentBlendTree` 和 `TryGetCurrentKey` 查询层的当前目标，
  不表示淡出节点已经消失。

### 4.2 层和生命周期

- Inspector 可设 1–4 层，默认 4 层。Clip 的资源条目 Layer 决定所属层；树在注册时指定层。
- `_layerSettings` 按层配置 Weight、AvatarMask 和 Additive。未配置时 Base 权重 1，上层权重 0。
- `Play` 只在目标层切换，不自动启用上层权重。层配置在初始化时应用，运行时设置不写回资产。
- `Pause` 冻结层和 Fade，`Resume` 续播；`Stop` 重置全部层，`StopLayer` 只停止指定层。
- WrapMode 仅接受 Once/Loop。Once 完成后暂停时间、保留姿势和权重，不自动淡出或退出上层。
- Loop 的运行时事件契约不能替代 Clip 的正确循环导入设置，真实姿势仍需 Unity 验证。
- `OnDisable` 调用 `ResetController`，清理绑定和 Graph；重新启用按配置初始化，
  树必须重新注册，Catalog 需要业务重新绑定。

以下示例假定 `Attack` 已绑定到 Layer 1，且业务已定义完成事件处理器：

```csharp
controller.SetLayerMask(1, upperBodyMask);
controller.SetLayerWeight(1, 1f);
controller.GetState("Attack").AddMarker(new AnimationEventMarker("Hit", 0.3f));
controller.EventQueue.Completed += OnAnimationCompleted;
controller.Play(new AnimationPlaybackOptions("Attack")
{
    FadeDuration = 0.1f,
    WrapMode = WrapMode.Once,
    RestartOnPlay = true
});
```

完成后回待机、停止上层或切换下一动作由业务决定；处理器需在业务生命周期结束时退订。

### 4.3 事件、选择策略与序列

Marker 通过 `state.AddMarker(...)` / `ClearMarkers()` 配置。
事件区间按累计时间覆盖同圈、跨圈和跨多圈；Seek、Stop、重播同步游标。
Once 完成通知入队，在采样后派发。队列先派发 Marker，再派发 Completed，
并检查句柄有效性和播放代次，避免旧通知污染重播或新绑定。

`EventQueue.Clear()` 只清待派发内容，不清订阅；Reset/禁用保留监听，销毁清除。
对象池交还时业务应成对退订，必要时显式 `ClearSubscribers()`。
业务回调异常记录日志后隔离，不中止其余通知。

Selection 的四个策略为 Random、Sequential、Weighted、NoRepeatRandom。
策略选择索引，不持有 Graph 或资源；业务可以用其选择播放配置。
SelectorNode 是 Graph 内组合入口，不另建同用途业务框架。

Sequential Selection 每次请求只挑下一项；Sequence 管理一段播放流程。
`AnimationSequenceNode` 默认手动推进，设置 `AutoAdvanceOnComplete = true` 后，
在完成通知之后的下一次采样前推进。Loop 项不会自动推进；
不提供序列级资源绑定、任意时间跳转、条件状态机或自动淡入衔接。

## 5. 资源模型与所有权

| 模式 | 行为与适用范围 |
|---|---|
| AnimationClipLibrary | SO 直接引用 Clip，适合编辑器和固定小集合；加载 SO 仍形成 Clip 硬引用依赖 |
| AnimationClipCatalog | 保存 Key、Location、PackageName 等条目信息，由 Provider 加载 Clip；Catalog 本身不直接引用 Clip |
| IAnimationClipProvider | 异步返回 AnimationClipLease，隔离项目资源系统 |
| AnimationClipLease | 保存 AnimationKey、Clip 和释放回调，Release/Dispose 至多执行一次释放 |

Catalog 的地址模型允许资源层独立组织动画，但当前 `BindCatalogAsync` 会依次绑定整个集合，
不是首次 Play 时才加载，也没有单 Clip 动态卸载 API。不要把配置间接引用等同于已实现流式加载。
Library 到 Catalog 的自动烘焙、完整预览和资源缓存策略也不是当前能力。

所有权规则：

1. Provider 在成功创建 Lease 后才移交底层资源句柄；失败或取消释放尚未移交的句柄。
2. Controller 收到 Lease 后先登记待清理集合，再检查取消，避免返回成功与取消之间的泄漏。
3. 绑定代次和生命周期取消令牌防止旧异步请求污染新集合；过期请求只清理自己拥有的资源。
4. 树创建独立叶子节点，但共享 Controller 已持有的 Clip 资源，不抢占源状态连接或重复加载。
5. 清理绑定时先释放层内节点和树，再释放 Lease；节点不得在资源已归还后继续采样。
6. Reset/禁用先停止 Graph、取消绑定，再清理节点、Lease 和 Graph。
   对象池不能只隐藏对象而忽略订阅及外部计算器、NativeArray 等资源。

核心参数/所有权违约保留异常；Library/Catalog 绑定失败清理后 `Log.Error` 并重抛。
正常取消不记 Error。YooAsset Provider 使用带取消令牌的 `UniTask.WaitUntil` 等待句柄完成，
不假设其他版本的 `ToUniTask` 重载在本项目存在。

## 6. 轻量 BlendTree

### 6.1 配置与使用

先绑定 Library，或 await Catalog 绑定结束，再按 Clip Key 和阈值注册树：

```csharp
var locomotion = new AnimationBlendTree1DDefinition(new[]
{
    new AnimationBlendTreeSample1D("Idle", 0f),
    new AnimationBlendTreeSample1D("Walk", 1f),
    new AnimationBlendTreeSample1D("Run", 3f)
});
var state = controller.RegisterBlendTree("Locomotion", locomotion, layer: 0);
controller.SetBlendTreeParameter("Locomotion", 1.6f);
controller.Play("Locomotion", fadeDuration: 0.2f);
state.SetParameter(2f);
controller.Pause();
controller.Resume();
controller.StopLayer(0);
controller.Play("Locomotion", 0f); // 参数保留，叶子从零重新播放。
```

二维方向树可嵌套上述一维 Definition：

```csharp
var directional = new AnimationBlendTree2DDefinition(new[]
{
    new AnimationBlendTreeSample2D(locomotion, Vector2.up),
    new AnimationBlendTreeSample2D("Back", Vector2.down),
    new AnimationBlendTreeSample2D("Left", Vector2.left),
    new AnimationBlendTreeSample2D("Right", Vector2.right)
}, parameter: Vector2.up, calculator: new DirectionalBlendWeightCalculator2D());
var directionState = controller.RegisterBlendTree("DirectionalMove", directional);
controller.SetBlendTreeParameter("DirectionalMove", new Vector2(0.5f, 1f));
((AnimationBlendTree1D)directionState.Root.ChildNodes[0]).Parameter = 1.6f;
controller.Play("DirectionalMove", 0.15f);
```

Definition/Sample 是不可变代码配置，配对 Clip Key 或嵌套 Definition 与阈值，
不是 ScriptableObject 编辑器资产。配置可复用，每次注册创建独立节点和时间状态；
部分创建失败递归释放已创建节点。源 Clip 的 Layer 不约束树的 Layer。

- 样本只引用 Clip Key 或嵌套 Definition，不引用另一棵已注册树的节点。
- 叶子固定 Loop、默认速度 1，不因权重重新激活而重启；零权重暂停，恢复权重时续播。
- 不继承源 Clip 的时间、播放配置或 Marker；树没有唯一 Time、Length、Completed。
- 树内禁止 CrossFade 单个样本，由父层 CrossFade 整棵树。
- Stop 后保留参数，再 Play 重建权重；重绑、Reset、禁用使句柄失效。
- 没有独立 Unregister、运行时样本表编辑、步态相位同步或统一树时间轴。
- 手工 Graph 可用低层构造器和 AddChild；普通业务使用成对 Sample 配置。

### 6.2 Fade 与一维算法

FadeGroup 采用“一进多出”，统一进度为 `elapsed / duration`：

```text
目标权重 = Lerp(起始权重, 1, progress)
其他权重 = Lerp(起始权重, 0, progress)
```

新切换从当前权重建立新 FadeGroup；完成后旧节点权重为零并暂停。
一维树要求有限且严格递增的阈值，在相邻样本间线性插值，超出范围钳制到首尾：

```text
t = (parameter - left) / (right - left)
leftWeight = 1 - t
rightWeight = t
```

### 6.3 二维算法与边界

默认 `CartesianBlendWeightCalculator2D` 使用 Gradient Band，不再使用距离倒数：

```text
factor[i,j] = (sample[j] - sample[i]) / squaredLength(sample[j] - sample[i])
candidate[i,j] = 1 - dot(parameter - sample[i], factor[i,j])
weight[i] = max(0, min(1, 所有 candidate[i,j]))
```

最后统一归一化。同一直线上的样本可得到相邻区间线性混合。

`DirectionalBlendWeightCalculator2D` 使用 Polar Gradient Band，
同时考虑方向角和长度：样本对的径向差按平均长度归一化，角差乘 2 后参与投影。
因此 `(0,1)` Walk 和 `(0,3)` Run 可以区分，允许一个原点 Idle：

```text
Idle = (0,0), Walk = (0,1), Run = (0,3)
参数 (0,0.25) → Idle 0.75、Walk 0.25、Run 0
参数 (0,2)    → Idle 0、Walk 0.5、Run 0.5
```

两个 CPU 计算器共享内部 `GradientBandWeightCalculator2D`：

- 样本非空、有限且位置不可重复；重复原点也拒绝，非法输入在写输出前校验。
- 缓存按内容、数量和顺序失效，不只比较集合引用；同数量更新复用数组。
- 一般参数计算、缓存重建均为 $O(n^2)$，缓存空间为 $O(n^2)$，适合小样本表，不支持并发调用。
- 使用 double 中间值；精确命中对应项为 1，全零退化选欧氏最近项，等距选首项。
- 相反方向使用绝对角差消除正负 PI 偏置，不采用 Animancer 的 1% 小权重截断。
- 不引入速度外推、同步、参数绑定、全局缓存、Graph 脏调度或三角剖分，
  不承诺与 Animator/Animancer 逐点或逐帧等价。

## 7. Job 与 Compute 扩展

`AnimationJobNode<TJob>` 通过 `AnimationScriptPlayable` 接入，默认输入数为 0，
首次 AddInput 使用端口 0。骨骼偏移示例不等于完整 IK 系统；
Job 的 NativeArray 必须有明确所有者，在节点销毁时释放。

`ComputeBlendWeightCalculator2D` 是需调用方提供兼容 Shader 的实验入口：

- Kernel 为 `CSMain`；输入 `_Parameter`、`_Count`、`_Thresholds` 和 `_Weights`。
- 两个缓冲元素均为双 float，读取权重结果的 x 分量；按 16 线程计算 Dispatch 组数。
- Shader 必须匹配线程组并自行检查 `_Count` 越界，CPU 计算器不会替 Shader 提供保护。
- 构造时上传样本，计算时要求样本内容和顺序一致，Dispatch 后 GetData 同步读回。
- 调用方持有并 Dispose 外部计算器；树不会代为释放，所有使用者结束后才能释放。

当前不能宣称已经提供可直接替换 CPU Gradient Band 的 GPU 版本。
工作区旧 `BlendTree2D.compute` 示例使用不同 Kernel/缓冲协议，不能直接与该计算器配套；
正式提交中也没有 `DistanceWeight2D.compute`。平台支持、线程越界和性能仍需专项验证。
小规模动画样本通常优先使用 CPU，只有 Profiling 证明收益后才考虑 GPU。

## 8. 校验与诊断

Editor 提供 Library、Catalog、PlaybackOptionsSet 的 Inspector/校验入口，
检查 Key、层、播放模式等基础配置；不等同于完整资源烘焙或 BlendTree 编辑器。

`CaptureSnapshot()` 返回 `AnimationSnapshot`，提供 Graph 状态、Clip/Lease/事件数量，
以及 `AnimationStateSnapshot` / `AnimationBlendTreeSnapshot` 两种快照。
`StateCount` 仅统计 Clip，树另有 `BlendTreeCount`。
快照不是资源追踪器，不宣称已提供逐个 AssetHandle 状态、最近事件历史或 FadeProgress。
按需采集，不把字符串和列表快照放进默认每帧业务路径。

`PlayableGraphMonitorAdapter` 在 Editor 按当前选中对象每 0.25 秒刷新标签，
展示 Clip Key 和嵌套树路径，重载前退订。底层 Graph 拓扑由 Monitor 提供，
不能替代 Lease 泄漏、YooAsset 失败或对象池生命周期验证。
真机 `IDebuggerWindow` 适配、自研预览、资源追踪面板仍属后续需求，不列为已实现。

## 9. 验证状态与后续优先级

### 9.1 已有测试源码

- `Assets/TEngine.Extensions/Tests/PlaybackRegression/PlaybackContractTests.cs`：
  26 项播放契约测试，覆盖层、暂停恢复、完成通知、失效句柄、取消 Lease、序列、
  非法配置、树注册/嵌套/创建失败和整体生命周期。
- 同目录 `BlendWeightCalculatorTests.cs`：12 个方法，按属性静态计数 18 个用例，
  覆盖二维插值、原点/速度、角度跨界、相反方向、缓存变化、极值和非法输入。

这些是测试源码数量，不是测试通过数量。此前源码扫描、括号检查、程序集 JSON 核对
不等于 C# 编译、Graph 姿势验证或性能测试；历史构建结果也不作为最新版本证据。
按用户约定，编译、构建、Unity Test Runner 和真实角色/YooAsset 验证由用户执行。
脚本重命名后的资源引用和 GUID 连续性仍需 Unity 确认，不把文件存在等同于迁移已验证。

### 9.2 优先验证

1. **播放与资源闭环**：快速重绑、返回 Lease 后立即取消、场景卸载、禁用再启用、
   对象池复用和多实例；确认旧通知隔离及资源只释放一次。
2. **真实姿势**：Idle/Walk/Run、Attack/Hit/Death、快速打断、跨圈 Marker、
   Once 保持姿势、上层 AvatarMask/Additive 和 BlendTree 停止重播。
3. **权重与性能**：边界参数、不同样本布局、缓存变化；按需对照参考算法，
   不预先宣称逐点等价或无 GC。
4. **可选扩展**：Job/Behaviour 输入端口、NativeArray 释放、Monitor 标签；
   Compute 单独验证协议、平台、越界和同步读回成本。

先稳定上述闭环，再按真实需求评估步态同步、配置关联校验或运行时诊断。
不因为参考框架具备某项功能就扩展当前节点体系。

### 9.3 本次文档整合范围

本次仅更新迁移后的架构文档及纳入已有 `.meta`，合并独立审查记录后移除该记录文件。
不修改运行时代码、场景、包设置或教学样例，不重写之前的 Git 提交历史。
已有的源码、场景、插件和暂存区改动均不属于本次文档提交。
