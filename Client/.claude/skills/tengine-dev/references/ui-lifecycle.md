# UI 生命周期与核心 API

> **适用场景**：UIWindow/UIWidget 生命周期、层级（UILayer）、ShowUI/CloseUI/HideUI API、ScriptGenerator 节点绑定 | **关联文档**：[event-system.md](event-system.md)（AddUIEvent）、[ui-patterns.md](ui-patterns.md)（Widget 模板）、[naming-rules.md](naming-rules.md)（节点前缀）

---

## 一、核心 API

### UILayer 层级

| 值 | 层级 | 用途 |
|----|------|------|
| 0 | Bottom | 底层（世界空间 UI、背景）|
| 1 | UI | 普通 UI 层（主要界面）|
| 2 | Top | 顶层（弹窗、全屏遮罩）|
| 3 | Tips | 提示层（Toast、飘字）|
| 4 | System | 系统层（加载中、异常提示）|

### WindowAttribute 窗口标记

每个 UIWindow 子类必须标记 `[Window]` 特性：

```csharp
[Window(UILayer.UI, "BattleMainUI")]
public class BattleMainUI : UIWindow { }

[Window(layer: UILayer.Top, location: "LoginUI", fullScreen: true, hideTimeToClose: 10f)]
public class LoginUI : UIWindow { }
```

**location** 即 Prefab 资源地址（`AssetRaw/UI/Prefabs/` 下的文件名，不含扩展名）。

### UIWindow 生命周期

```
ShowUI<T>()
    │
    ▼
Inject()               ← 首次：依赖注入扩展点（UIBase.Injector 静态委托）
    │
    ▼
ScriptGenerator()     ← 首次：生成代码绑定节点与控件监听（仅一次）
    │
    ▼
BindMemberProperty()  ← 首次：手写补充成员绑定
    │
    ▼
RegisterEvent()       ← 首次：手写事件注册（仅 AddUIEvent 自动清理）
    │
    ▼
OnCreate()            ← 首次：窗口创建初始化（仅一次）
    │
    ▼
OnRefresh()           ← 每次 ShowUI 都执行（刷新显示数据）
    │
    ▼
OnUpdate()            ← 每帧更新（子类必须 override 才会被框架调用，基类设 _hasOverrideUpdate = false）
    │
    ▼
HideUI() / CloseUI()
    ├── 隐藏：OnSetVisible(false)，超时后触发 Close
    └── 关闭流程：
        RemoveAllUIEvent()     ← 自动清理 AddUIEvent 记录的监听
            │
            ▼
        子Widget.OnDestroy()   ← 先销毁子 Widget
            │
            ▼
        OnDestroy()            ← 窗口自身销毁前清理
            │
            ▼
        Destroy(gameObject)    ← 销毁 GameObject
```

**关键规则**：
- `Inject` / `ScriptGenerator` / `BindMemberProperty` / `RegisterEvent` / `OnCreate` 只执行一次
- `OnRefresh` 每次 Show 都执行
- `OnUpdate` 基类实现设 `_hasOverrideUpdate = false`，子类必须 override 才会被框架调用；尽量避免使用，改用 `GameModule.Timer`
- 窗口销毁方法为 `OnDestroy()`，不存在 `OnClose` 方法
- 完整前缀表见 [naming-rules.md](naming-rules.md#ui-节点命名规范)

### UI 业务代码的职责划分

**UIWindow / UIWidget 的手写业务代码应优先使用基类已有的职责钩子，不把成员查找、事件注册和初始化统一堆到 `OnCreate()`。** 编写或调整 UI 时，先核对基类实际调用顺序，再按职责放置逻辑：

| 钩子 | 职责 |
|------|------|
| `ScriptGenerator()` | 生成代码负责的节点绑定、Widget 创建与控件监听；已有 Gen 实现时不在手写 partial 中重复 override，不为补充业务绑定手改 Gen |
| `BindMemberProperty()` | 补充生成代码未覆盖的成员绑定，如从已绑定节点获取额外 `Button` 组件；不在此注册监听 |
| `RegisterEvent()` | 手写控件监听、`AddUIEvent` 和普通 C# 事件订阅；使用前序绑定好的成员，不重复注册生成代码已处理的监听 |
| `OnCreate()` | 绑定与注册完成后的一次性业务初始化；不要承担已有专用钩子的职责 |
| `OnRefresh()` | 每次显示时根据当前参数刷新数据与表现；不重复执行一次性绑定或监听注册 |
| `OnDestroy()` | 清理当前对象负责的监听与资源；遵守框架自动清理范围及所有权，不重复销毁框架托管的子 Widget |

- `AddUIEvent` 由框架自动移除；手写 `Button.onClick.AddListener`、普通 C# 事件不属于这套自动清理机制，应按发布者/订阅者生命周期成对解绑或由拥有者统一清理。父窗口 `OnDestroy` 执行前子 Widget 已销毁，不在此访问子 Widget 做第二遍清理。
- `RegisterEvent` 先于 `OnCreate`；其中使用的成员应在生成绑定或 `BindMemberProperty` 中就绪。确实依赖后续动态创建对象的监听，随该对象的实际创建/释放点配对处理，不为套用钩子破坏必要时序。
- 只 override 当前确有职责的方法，不添加空钩子，也不额外手动调用生命周期方法或重复执行基类流程。修改范围限于当前需求，不机械迁移无关 UI。

### 扩展虚方法

| 方法 | 签名 | 用途 |
|------|------|------|
| `OnSortDepth` | `protected virtual void OnSortDepth()` | 窗口层级排序回调（Depth 属性变化时触发） |
| `OnSetVisible` | `protected virtual void OnSetVisible(bool visible)` | 窗口显隐回调（Hide/Show 时触发） |

### UI 运行时资源加载入口

- UIWindow / UIWidget 及其业务辅助类加载 UI GameObject 时，使用基类采用的 `UIModule.Resource`（`IUIResourceLoader`）或契约适用的 `CreateWidgetByPathAsync` 等基类创建方法，不直接调用 `GameModule.Resource.LoadGameObjectAsync`。默认加载器虽转发给资源模块，仍须保留这一可替换入口；它不是每个窗口独有的加载器。
- 需要自行处理调用方 token、加载结果或 Widget 注册时，使用 `UIModule.Resource.LoadGameObjectAsync(location, parent, token)`，再通过所属 UI 的 `CreateWidget<T>(go)` 注册父子生命周期。不为切换入口而扩展框架或添加额外取消机制。
- 默认加载器的取消、失败和资源释放语义与底层实现一致；GameObject 销毁时自动归还引用，不额外手动 `UnloadAsset`。不把 UI 加载入口误认为已负责业务取消或迟到实例清理。
- 本约束针对加载实例，不禁止接口未提供的资源地址查询（如 `GameModule.Resource.CheckLocationValid`）。Sprite 沿用 `SetSprite`；其他 Asset 类型按 [resource-api.md](resource-api.md) 选择受支持的接口与成对释放方式，不向 `IUIResourceLoader` 臆造 API。

```csharp
var go = await UIModule.Resource.LoadGameObjectAsync(location, parent, cancellationToken);
var item = CreateWidget<ItemWidget>(go);
```

### 窗口开关与查询

```csharp
// 打开窗口
GameModule.UI.ShowUIAsync<BattleMainUI>();                       // fire and forget
var win = await GameModule.UI.ShowUIAsyncAwait<BattleMainUI>();  // 等待实例
GameModule.UI.ShowUIAsync<ItemDetailUI>(itemId, extraData);      // 携带用户数据

// 关闭 / 隐藏
GameModule.UI.CloseUI<BattleMainUI>();          // 关闭并销毁
GameModule.UI.HideUI<BattleMainUI>();           // 隐藏（超时自动关闭）
GameModule.UI.CloseAll();                       // 关闭所有
GameModule.UI.CloseAllWithOut<BattleMainUI>();  // 保留指定窗口

// 查询
bool exists   = GameModule.UI.HasWindow<BattleMainUI>();
bool loading  = GameModule.UI.IsAnyLoading();
```

### UIWidget 子组件

```csharp
var widget = CreateWidget<ItemWidget>("path/to/node");                     // 路径
var widget = CreateWidgetByPath<ItemWidget>(parent, "Location");           // 动态加载
var widget = await CreateWidgetByPathAsync<ItemWidget>(parent, "Loc");     // 异步
var widget = CreateWidgetByPrefab<ItemWidget>(prefab, parent);             // Prefab 克隆
AdjustIconNum<ItemWidget>(listIcon, count: items.Count, parent, prefab);   // 列表数量
```

UIWidget 生命周期与 UIWindow 相同：`ScriptGenerator → BindMemberProperty → RegisterEvent → OnCreate → OnRefresh → OnDestroy`

---

## 二、使用模式

### UI 内部事件（AddUIEvent）

在 `RegisterEvent()` 中使用，事件监听随窗口销毁**自动清理**：

```csharp
protected override void RegisterEvent()
{
    // 无泛型参数
    AddUIEvent(GameEventDef.OnGoldChanged, OnGoldChanged);
    // 1 个泛型参数
    AddUIEvent<int>(GameEventDef.OnHpChanged, OnHpChanged);
    // 2 个泛型参数
    AddUIEvent<int, string>(GameEventDef.OnItemAcquired, OnItemAcquired);
    // 3 个泛型参数
    AddUIEvent<int, string, float>(GameEventDef.OnAttrModified, OnAttrModified);
    // 4 个泛型参数
    AddUIEvent<int, string, float, bool>(GameEventDef.OnComplexEvent, OnComplexHandler);
}
```

**AddUIEvent 支持 0~4 个泛型参数**：`AddUIEvent`、`AddUIEvent<T>`、`AddUIEvent<T,U>`、`AddUIEvent<T,U,V>`、`AddUIEvent<T,U,V,W>`。

### 典型窗口实现

```csharp
[Window(UILayer.UI, "BagUI")]
public partial class BagUI : UIWindow
{
    private List<ItemWidget> _items = new();

    // ScriptGenerator 由 Gen 文件实现；手写部分只补充绑定。
    protected override void BindMemberProperty()
    {
        // 获取生成绑定未覆盖的组件引用
    }

    protected override void RegisterEvent()
    {
        AddUIEvent<int>(GameEventDef.OnItemChanged, OnItemChanged);
    }

    protected override void OnCreate()
    {
        // 一次性初始化（创建 Widget 等）
    }

    protected override void OnRefresh()
    {
        // 每次 Show 时刷新数据
    }

    // 仅在需要每帧更新时 override；基类设 _hasOverrideUpdate = false，不 override 不会被调用
    // protected override void OnUpdate() { }

    protected override void OnDestroy()
    {
        // 销毁前清理（子 Widget 会先于此方法销毁）
    }
}
```

---

## 三、常见错误

| 错误 | 正确做法 |
|------|---------|
| 把补充成员查找与监听注册都放进 `OnCreate` | 分别使用 `BindMemberProperty` 与 `RegisterEvent`，`OnCreate` 只保留一次性业务初始化 |
| 在 `RegisterEvent` 外使用 `GameEvent.AddEventListener` | 使用 `AddUIEvent`（自动随窗口销毁清理），详见 [event-system.md](event-system.md#常见错误) |
| override `OnUpdate` 但忘记实际需要每帧更新 | 优先使用 `GameModule.Timer`，避免不必要的每帧计算 |
| 使用不存在的 `OnClose` 方法 | 销毁回调为 `OnDestroy()` |
| 在 `OnDestroy` 中访问子 Widget | 子 Widget 的 `OnDestroy` 先于窗口 `OnDestroy` 执行，此时子 Widget 已销毁 |

---

## 四、交叉引用

| 主题 | 文档 |
|------|------|
| 事件系统详细用法与避坑 | [event-system.md](event-system.md) |
| UI 进阶模式（Widget 模板/节点绑定） | [ui-patterns.md](ui-patterns.md) |
| 资源加载与生命周期 | [resource-api.md](resource-api.md) |
| 命名规范与节点前缀 | [naming-rules.md](naming-rules.md) |
| 模块 API（Timer 等） | [modules.md](modules.md) |
