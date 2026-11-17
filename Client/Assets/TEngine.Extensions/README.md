# TEngine Extensions

TEngine Variant 的项目内扩展集合，位于 `Assets/TEngine.Extensions`，提供 UGUI、虚拟列表、对象池和红点功能。

扩展统一分为 `Runtime` 和 `Editor` 两个程序集，Editor 程序集依赖 Runtime 程序集；各功能模块不再单独拆分程序集。

UI 绑定组件不属于主包扩展：`GameLogic.UIBindComponent` 保留在热更 `GameLogic`，
UI 生成器、规则、配置资产和 Inspector 按原版目录放在 `Assets/Editor/UIScriptGenerator`，
设置面板与 Inspector 按钮工具放在 `Assets/Editor/TEngineSettingsProvider`，统一编入 Editor-only
`TEngine.UI.Editor`。本目录保留 HTML 烘焙工具，通过该 Editor 程序集读取共享规则，
不直接引用 `GameLogic`。Extensions 的 UGUI 控件本身仍属于主包。

## UGUI

`TEngine.Extensions.UGUI` 包含：

- `NestedScrollRect`、`LongPressHandler`、`DragHandler`；
- `SafeAreaFitter`、`AspectRatioBackground`；
- `PressScaleFeedback`、`ClickSoundFeedback`；
- `UIAnimationExtension`、`EmptyGraphic`；
- `ButtonEx`，支持点击、双击、长按、拖拽和六类按钮事件。
- `UIStateController`，支持状态组、状态绑定、状态联动和 Inspector 预览。

### UI 音频反馈

`Feedback` 子目录提供可选的音频抽象：

```csharp
UIAudioRouter.AudioPlayer = clip => PlayClip(clip);
UIAudioRouter.DeviceResolver = () => UIAudioDevice.KeyboardMouse;
```

在场景中添加 `UIAudioBinder` 并设置 `UIAudioProfile`，即可按事件和输入设备选择音效。单个控件可以添加 `UIAudioOverride`，支持 `Fallback`、`Exclusive` 和 `Silent`。

`ButtonEx` 和 `ClickSoundFeedback` 的音效事件直接进入 `UIFeedback` 路由，不再维护音效 ID 事件映射。

## VirtualizedList

统一业务 API：

- `VirtualizedListView` / `VirtualizedListItem` / `IVirtualizedListAdapter`
- `VirtualizedGridView` / `VirtualizedGridItem` / `IVirtualizedGridAdapter`

底层是独立的 `VirtualizedListCore` / `VirtualizedGridCore`，负责 ScrollRect viewport、可见区间、位置计算、动态尺寸（线性列表）和回收。旧 `Loop*View` 实现已移除，不再维护两套滚动内核。

Adapter 的 `CreateItem` 通常调用 `listView.AllocOrNewItem("PrefabName")`，Prefab 上应挂对应的 `VirtualizedListItem` 或 `VirtualizedGridItem` 子类。绑定回调带有当前数据索引和 binding version，异步回调可用 `IsBindingCurrent` 丢弃过期结果；回收回调负责清理视图状态。

实例创建由 `IVirtualizedListPrefabSource` / `IVirtualizedGridPrefabSource` 注入，默认实现使用 `VirtualizedListServices`。因此新内核对应 LoopScrollRect 的 PrefabSource/DataSource 分离，不把资源加载逻辑写入滚动计算。

`TEngine.Extensions.VirtualizedList` 包含：

- 独立 `VirtualizedListCore` / `VirtualizedGridCore`；
- `LinearVirtualizedLayout`、`WaterfallVirtualizedLayout`、`SectionedLinearVirtualizedLayout`；
- `CustomRectVirtualizedLayout`，用于异形卡牌、弧形、扇形和棋盘等业务布局；
- `Start` / `Center` / `End` 三种 Snap 对齐；
- ScrollRect viewport、可见区间和 Item 回收；
- 线性列表动态尺寸与尺寸索引；
- `SetItemCount(count, resetPosition)`；
- `VirtualizedListItem.PrepareBinding` 和 `IsBindingCurrent`，用于异步绑定结果校验；
- `VirtualizedListScrollAnchor`，用于插入、删除数据后保存和恢复滚动锚点。

适配器示例：

```csharp
listView.Initialize(adapter, prefabSource);
```

资源加载仍通过注入完成：

```csharp
VirtualizedListServices.PrefabLoader = (key, parent) => ...;
```

也可以显式注入 `IVirtualizedListPrefabSource` / `IVirtualizedGridPrefabSource`，把实例池或资源模块接到 Core 外部。

业务布局通过 `VirtualizedListOptions.Layout` 注入。低频特殊布局优先实现 `IVirtualizedListLayout`，只处理 Item Rect、ContentSize 和可见范围，不修改 Core 的复用流程。

Snap 通过 `VirtualizedSnapOptions` 配置，支持拖拽结束自动吸附和 `SnapToIndex` / `SnapToNearest` 主动吸附。线性列表支持动态尺寸、自定义 Rect 和三种对齐方式；固定网格按行进行吸附。

`Runtime/VirtualizedList/Adapters` 提供 TEngine 资源模块适配：

```csharp
VirtualizedListResourceAdapter.Install();
```

## ObjectPool

`TEngine.Extensions.GameObjectPool` 是资源无关的通用 GameObject 实例池，适合：

- UI 面板、列表项和弹窗实例；
- 子弹、特效、飘字等高频短生命周期对象；
- 需要预热、容量上限和空闲过期清理的场景。

完整架构、生命周期、容量策略和资源所有权说明见：
[GameObjectPool 设计文档](../../../Books/TEngine.Extensions-架构设计.md)。

基本用法：

```csharp
var pool = new GameObjectPool(source, poolRoot, new[]
{
    new GameObjectPoolConfig(
        name: "Toast",
        key: "UI/Toast",
        capacity: 32,
        preloadCount: 8,
        expireTime: 60f,
        matchMode: GameObjectPoolKeyMatchMode.Exact)
});
pool.Preload("UI/Toast");

var instance = pool.Rent("UI/Toast", parent);
pool.Return(instance);
pool.Cleanup();
```

需要 TEngine 模块调用风格时，可注册 `TEngineGameObjectPoolModule`，使用：

```csharp
var module = new TEngineGameObjectPoolModule();
ModuleSystem.RegisterModule<IGameObjectPoolModule>(module);

var instance = await module.SpawnAsync("UI/Toast", parent);
module.Recycle(instance);
```

`IGameObjectPoolResourceProvider` 负责实例的同步/异步创建和销毁，因此核心池不绑定 `Resources`、YooAsset 或 TEngine。`GameObjectPoolConfig` 支持精确 key 和带边界的前缀匹配（`UI/` 不会误匹配 `UIOther/`），并提供容量、预热、空闲过期、最早空闲淘汰和池满时临时超容策略。异步同 key 创建会合并，单个等待者取消不会取消共享创建。

该设计借鉴对象池资料中的配置、预热、容量、空闲清理和池满时临时超容策略，但不强制全局单例。实例租约和资源租约保持分离，避免错误卸载仍被其他系统使用的资源。

TEngine 适配通过 `TEngineGameObjectPoolResourceProvider` 调用 `LoadGameObject` / `LoadGameObjectAsync`；实例销毁后由 TEngine 的 `AssetsReference` 释放资源，不要对这些实例额外调用 `UnloadAsset`。

`Preload` / `PreloadAsync` 会直接创建并登记多个空闲实例，不会反复借出和归还同一个实例。`AutoDestroyTime` 用于清理长时间完全空闲的配置组，`ExpireTime` 只用于清理组内空闲实例。

UGUI 扩展还提供 `UIText` 和 `UIImage`：

- `UIText`：字符间距、整体/逐字四角渐变、顶点颜色混合、可见行数；
- `UIImage`：水平、垂直和双向 UV 镜像、简单 Sprite 圆角；
- 阴影和描边直接兼容 Unity 内置 `Shadow` / `Outline`，不重复实现；
- 两者均不依赖 DGame 的材质、资源地址或业务程序集。

示例：

```csharp
var label = GetComponent<UIText>();
label.CharacterSpacing = 2f;
label.UseGradient = true;
label.SplitTextGradient = false;
label.GradientTopColor = Color.white;
label.GradientBottomColor = Color.cyan;
label.UseVertexColor = true;
label.VertexTopLeftColor = Color.white;
label.VertexBottomRightColor = new Color(1f, 1f, 1f, 0.8f);

var icon = GetComponent<UIImage>();
icon.Mirror = UIImage.MirrorMode.Horizontal;
icon.UseRoundedCorners = true;
icon.CornerRadius = 12f;
icon.CornerSegments = 6;
```

### UIStateController

`UIStateController` 负责 UI 的表现状态，不替代 TEngine 的游戏流程状态机。一个状态组可以同时驱动多个 UI 属性：

```text
ButtonState
├── Normal
├── Selected
└── Disabled
```

在 Inspector 中添加状态组、状态项和绑定后，业务代码只需要切换状态：

```csharp
var controller = GetComponent<UIStateController>();
controller.SetState("ButtonState", "Selected");
controller.SetState("ButtonState", "Disabled");
controller.ResetState("ButtonState");
```

内置绑定类型包括：

- GameObject 激活；
- Graphic 颜色；
- Image Sprite；
- Image Sprite 和颜色；
- Text 文本；
- Selectable 可交互状态；
- CanvasGroup 透明度；
- RectTransform 锚点位置。

状态联动可将一个状态组映射到另一个状态组，例如 `TabState=Equipment` 时自动切换 `PanelState=Equipment`。Inspector 使用 Unity 原生 IMGUI，不依赖 Odin。

## RedDot

### 核心

`TEngine.Extensions.RedDot` 与 Unity 无关，支持：

- 状态和数量；
- `AnyActive` / `SumCount` 父子聚合；
- `Dot` / `Number` / `New` / `Custom` 显示类型；
- `IDisposable` 订阅；
- 参数化 Key。
- `BeginBatch()` 批量更新，合并重复的父节点聚合和通知。

### Unity UI 与代码生成

`TEngine.Extensions.RedDot.UnityUI` 提供：

- `RedDotRegistryComponent`；
- `RedDotView`；
- `RedDotTreeConfig` ScriptableObject。

编辑器菜单：

```text
TEngine Extensions/RedDot/Generate Selected Config
TEngine Extensions/RedDot/Generate All Configs
```

生成器会输出稳定 ID、路径常量、显示类型和 `RegisterAll` 方法。生成文件位于配置指定的 `Assets` 子目录中。

## TEngine 适配边界

该包不包含旧版 `UIWidget` 泛型包装和 `SwitchPageMgr`。这些类型依赖 TEngine UI 生命周期，应在单独适配层中设计。

## 来源与许可证

本包中的历史 TEngine 提取代码遵循仓库根目录的 MIT License，并保留 TEngine 来源说明。新增代码由本包维护者编写。

本包没有复制 DGame 或 AlicizaX UI Extension 的具体实现，只借鉴了配置生成、反馈路由、Adapter、ViewHolder 和滚动锚点等设计。

虚拟列表的设计来源也保持分层：LoopScrollRect 提供底层 ScrollRect 复用、Prefab/DataSource 分离思路；AlicizaX RecyclerView 提供 Adapter、ViewHolder、动态尺寸、刷新策略和滚动锚点思路。SuperScrollView 未作为新的通用架构来源。

当前 VirtualizedList 还提供以下可组合扩展：

- `MaxCachedItemsPerPrefab` 限制每种 Prefab 的回收缓存；
- `IVirtualizedItemAnimator` 和 `VirtualizedFadeAnimator` 管理显示/回收动画；
- `IVirtualizedItemDecoration`、`DelegateVirtualizedDecoration` 修改单项 Rect；
- `VirtualizedPagingController` 在接近数据尾部时请求下一页，并同步 ItemCount；
- `VirtualizedItemInteraction` 提供点击/长按事件；
- `VirtualizedItemReorderHandle` 对接业务排序处理器；
- `VirtualizedGridOptions.Vertical = false` 支持横向主轴网格；
- `BufferPixels` 提供可见区前后预取范围。

正式分发时请同时保留包内 `LICENSE.md` 和 TEngine 仓库许可证。

## 验证边界

- JSON、asmdef 和 Git 空白检查可在无 Unity Editor 时执行；
- RedDot 核心可单独进行 .NET 编译和行为测试；
- Unity Editor 编译、AssetDatabase 生成和 UGUI/滚动运行时行为需要本项目 Unity CLI。
