# MechStorm 战斗系统长期拓展规划

## 记录目的

本文记录从“战斗系统：GameObject / State / Attribute / Modifier / Trigger / Bullet”系列文章中提炼出的长期架构借鉴方向。

当前项目已进入 P1 / Sprint 3。本文不是近期必须全部实现的任务清单，而是后续扩展战斗系统时的技术路线约束；具体 Sprint、任务状态与验收边界以 `DEVELOPMENT_PROGRESS.md` 为唯一执行来源。

本文属于前期规划文档，不是不可变更的架构承诺。后续应根据玩法验证结果、代码复杂度、测试成本、配置需求和团队工具链成熟度持续调整。

## 与轻量 GAS 变种的关系

项目主计划中已经明确采用“轻量级 GAS 变种”作为实体与状态管线方向，包括：

- `GameplayTags` 简版
- `GameplayAttribute`
- `GameplayModifier`
- `BattleResource.*`
- `State.Broken.*`
- `Locomotion.*`
- `Attack.Mode.*`
- `Timing.*`

因此，本文记录的文章思路不用于替代项目原有 GAS 变种规划，而是作为补充参考。

推荐关系如下：

| 职责 | 项目主干采用 | 文章思路提供的补充 |
|------|--------------|--------------------|
| 状态标识 | `GameplayTag` | 用层级标签表达状态、攻击方式、移动能力和时序 |
| 数值中心 | `GameplayAttribute` | 属性集中读取，避免各系统到处查询状态来源 |
| 数值修改 | `GameplayModifier` | Modifier 不拥有生命周期，只修改流程中的值 |
| 持续状态 | 轻量 `BattleState` / `StateComponent` | 借鉴“一切皆 State”，但只覆盖有生命周期的状态 |
| 主动行为 | `Command` / `Ability` / `ActionContext` | 不强行把所有主动技能都塞进 State |
| 条件触发 | `Trigger` / `EventBus` | Trigger 作为扩展点，不替代核心流程 |

当前建议是：

```text
主架构：轻量 GAS 变种
补充思想：State 生命周期统一、Modifier 流程化、Attribute 集中读取、Trigger 扩展点
不采用：完整 UE GAS、不采用完整“一切皆 State”框架
```

也就是说：

- 有生命周期的控制、破坏、过热、护盾、警戒等效果，可以逐步进入轻量 `State`。
- 主动行动仍优先用 `Command / Ability / ActionContext` 表达。
- 最终数值和功能开关通过 `GameplayAttribute` 读取。
- 分类、条件和状态标识通过 `GameplayTag` 表达。
- Modifier 作为属性或流程修改器，不直接承担生命周期。
- Trigger 用于反击、警戒、地形、回合开始等扩展点，但核心移动、攻击、回合推进仍保持显式流程。

## 核心决策

MechStorm 后续战斗系统应保持以下方向：

1. **纯逻辑优先**：战斗规则继续放在 `MechStorm.Battle`，不依赖 Unity。
2. **显式流程优先**：移动、攻击、回合推进等核心流程保持显式调用，不用事件系统替代主流程。
3. **逐步组件化**：当 `CombatUnit` 职责膨胀时，再逐步拆出属性、状态、行动等组件。
4. **状态统一建模**：眩晕、过热、护盾、死亡、行动结束、警戒等持续性效果，后续优先进入轻量 `State` 系统。
5. **属性集中读取**：HP、AP、移动力、攻击、防御、热量、护盾、功能禁用等最终结果，后续逐步收敛到统一属性中心。
6. **上下文承载流程**：伤害、命中、移动、技能释放等一次性流程，后续用 Context 承载输入、中间值和结果。
7. **Modifier / Trigger 后置**：修改器和触发器作为中后期扩展能力，不在早期原型阶段提前完整实现。
8. **数据驱动渐进引入**：先用简单配置满足玩法验证，技能数量和规则复杂度上来后，再引入更强的数据驱动或编辑器工具。

### 基础战斗镜头边界（2026-10-22 批准，待实现）

- 镜头属于客户端 Presentation；Runtime 装配并管理本场镜头依赖，Procedure 管阶段启停与释放，HUD 只提交操作意图。Battle 不引用镜头，不因观察变化修改角色逻辑朝向、位置、行动或胜负。
- T1.8 使用8个离散观察方位、相邻45°，基于配置初始方位循环。默认45°俯角仅由配置修改，玩家运行时不能调俯角；T1.13 的缩放也不改变俯角。此范围覆盖旧四档方案及讨论中的四档90°方案。
- 每个角色开始行动时居中，包括同阵营内换角色和阵营交接；居中保留玩家选择的角度与缩放。手动平移后不每帧拉回，下一个角色开始行动再居中；选中敌方目标不改变行动者焦点。角色移动后的跟随方式在镜头任务开工时明确。
- T1.13 增加棋盘平面拖拽平移、焦点边界和限幅滚轮缩放。边缘行动者居中优先，不强求画面永不露出棋盘外；拖拽键位、阈值、缩放和边界参数在实现前确认。
- 镜头手势与棋盘动作必须互斥。当前棋盘按下左键即处理点击，若采用左键拖动，需要先完成点击/拖拽判定再提交动作；不能在拖动已发生后撤销误提交动作。UI 上手势不穿透，提示/结果/离场启停和重战恢复纳入同一验收，覆盖8档、缩放与平移后的射线、高亮、血条朝向及目标确认。
- 不引入自由轨道相机、运行时俯角调整、镜头碰撞或技能演出导演。镜头拖拽不是角色部署，角色库、上下阵和布阵仍归 Sprint 9。

### T1 收尾顺序与数据边界（2026-10-22）

正式任务/估时/验收状态只在 DEVELOPMENT_PROGRESS.md 维护：结果页 → 全新重战/返回 → 只读统计 → 最小 Luban 迁移 → 八档镜头/平移/缩放 → T1 补漏和完整回归 → Sprint 5 地形寻路 → 后续机制。排期批准不代表既有异常退出或完整回归已验收，不自动开始源码实现。

重战由本场生命周期所有者释放旧场并创建全新 Session，不局部重填旧场属性；正常失败读取 Outcome，程序异常不伪装成战败。正常返回是否切回 main 场景在对应任务开工时确认；当前异常返回仅打开 MainUI 的实现事实不变。统计从权威日志构建只读数据，不回写战斗。

最小 Luban 迁移放在可重复闭环之后、地形和新机制扩展之前，只替换当前数据来源。生成代码归 GameProto，二进制由 YooAsset 管理并异步加载、明确资源释放；GameLogic 单向转换为既有 Battle Data，Battle 不依赖 Luban、GameProto 或 Unity。场景引用、材质与镜头调试参数仍为表现配置；不直接采用同步懒加载模板。迁移必须证明初始快照、数值、技能和重战恢复等价，不同时改玩法或预建完整地形/属性/装备/Buff Schema；后续地形规则确定后再扩表。

## 分阶段路线

### 当前执行排期映射（2026-07-18）

实际 Sprint 2 已优先完成 `BattleSession`、多单位、回合、结构化 Result、Snapshot 和 ActionLog，与主计划旧 Sprint 2 的属性、部位、资源和武器任务不同。未实施内容没有取消，而是按真实依赖重新排期：

| Sprint | 当前执行主题 | 关键边界 |
|---|---|---|
| Sprint 3 | 占格、攻击范围、目标规则与高亮 | 继续使用 BFS 和瞬移，不做技能、地形 Cost 或 A* |
| Sprint 4 | 最小主动技能闭环 | 单体直接伤害、最小 AP、目标选择与日志；不做 Buff / Modifier / Trigger |
| Sprint 5 | Terrain Cost、Dijkstra 与 A* | Dijkstra 负责带权范围，A* 负责单目标路径；不做路径动画 |
| Sprint 6 | 属性合成、多部位、Tag、部位破坏和最小武器关系 | 补回主计划旧 Sprint 2 的机甲核心机制 |
| Sprint 7 | AP / PP、技能成本、命中率、确定性 RNG 与伤害管线 | Context 仅在参数和共享中间状态真实膨胀时引入 |
| Sprint 8 | Elevation、Locomotion、ZOC、LOS、掩体与最小 PVE AI | `IMapTopology` 仅在第二种真实拓扑出现时评估 |
| Sprint 9～12 | 正式流程、State / Attribute / Modifier / Trigger、Luban、Command / 恢复 / 回放基础 | 主计划旧 P2 工程化任务顺延，不重复已完成的 TEngine 和 asmdef 接入 |

动态召唤、Hex、正式路径动画、复杂 Buff、追加攻击、PVP 和编辑器工具继续按真实玩法与前置条件进入 Backlog，不为填满路线图提前实现。

### 架构演进 A：最小可玩原型

目标是验证战棋机甲基础循环：

- 方格拓扑
- 移动范围
- 单位显示
- 点击移动
- 基础攻击
- 回合切换

此阶段应避免引入完整 `State / Modifier / Trigger / Bullet` 框架。

允许保留简单直接的实现，只要核心逻辑可测试、表现层与逻辑层边界清晰。

### 架构演进 B：轻量战斗行动与空间规则

当基础闭环跑通后，可以引入：

- 动态占格、攻击范围、候选目标与目标合法性查询
- 最小 `AbilityDefinition` / `TargetRule` / `AbilityResolver`
- Terrain Cost、Dijkstra 可达范围和 A* 单目标路径
- 多部位、资源、武器、命中率和确定性 `BattleRNG`
- `BattleActionResult`：统一承载移动、攻击、结束回合和使用技能的结果，供表现层刷新
- `CombatUnit.UnitId`：提供本场战斗内稳定的实体身份，供 Snapshot、日志和后续回放引用
- 轻量 `BattleSnapshot` / `BattleActionLog`：先支撑调试导出，不提前实现完整回放

重点是先让范围、技能、空间和机甲核心规则形成可玩闭环。正式 Command、通用 State 生命周期、Modifier 和 Trigger 仍后置到对应执行任务，不因概念已经命名就提前搭建完整框架。

### 架构演进 C：属性中心与流程上下文

当单位数据和规则开始变多后，逐步引入：

- `BattleAttributeSet`
- `DamageContext`
- `AttackContext`
- `MoveContext`
- 属性变化结果记录
- 战斗日志或调试输出

此阶段目标是减少规则散落，方便后续 AI、UI、回放、调试读取同一套结果。

### 架构演进 D：Modifier 与 Trigger

当装备、机甲部件、天赋、地形、状态开始影响战斗流程时，再引入：

- `AttributeModifier`
- `DamageModifier`
- `HitModifier`
- `MoveCostModifier`
- 回合触发器
- 进入格子触发器
- 受击触发器
- 警戒/反击触发器

核心原则：

- Modifier 只修改流程中的值，不拥有生命周期。
- State / 装备 / 地形可以提供 Modifier。
- Trigger 作为扩展点，不替代核心流程。

### 架构演进 E：复杂技能、子弹与复杂数据驱动

Sprint 4 只建立最小主动技能闭环；当项目后续需要复杂技能和投射物时，再考虑：

- 技能配置表
- 技能释放上下文
- 技能状态或技能脚本
- 独立生命周期的 Bullet
- 可拦截、延迟落点、追踪导弹、无人机等实体化效果
- 更强的数据驱动或可视化编辑器

普通瞬发攻击不应提前实体化为 Bullet。只有具备独立生命周期和交互需求的效果才需要 Bullet。

## 不应提前照搬的内容

早期原型与核心机制阶段不建议提前实现：

- 完整“一切皆 State”框架
- 完整 Modifier 表体系
- 复杂技能编辑器
- 行为树技能系统
- 大型互斥图
- 完整子弹框架
- 所有流程事件化
- 复杂配置 DSL

这些能力适合在玩法复杂度真实出现后再扩展。

## 当前开发约束

1. `MechStorm.Battle` 不引用 `UnityEngine`。
2. `MechStorm.Presentation` 只负责 Unity 表现、输入适配和 View 同步。
3. 新增战斗规则优先补 EditMode 测试。
4. 当前 P1 仍不为了架构完整性牺牲可玩闭环推进速度。
5. 每次引入新抽象前，应先确认已有代码出现重复、职责膨胀或测试困难。

### 当前代码组织边界

`MechStorm.Battle` 保持单一纯 C# 程序集，目录与命名空间按职责对应：

- 根命名空间：`BattleSession` 等战斗统一入口。
- `Foundation`：基础值类型。
- `Spatial`：棋盘拓扑与空间规则基础。
- `Units`：战斗单位、阵营、Pilot / Mech 数据和运行时数据、Registry。
- `Turns`：行动顺序与回合推进。
- `Rules`：移动、攻击等显式规则解析。
- `Actions`：行动类型、失败原因、变化类型、结果与行动日志。
- `Snapshots`：战场和单位快照。
- `Diagnostics`：JSON 等调试导出。
- `Numeric`：尚未接入主流程的数值组件候选。

`MechStorm.Presentation` 保持单一 Unity 表现程序集，使用 `Board / Controllers / Input / Units` 归类棋盘适配、流程编排、输入和单位表现。测试目录镜像生产职责；当前不为每个目录拆分 asmdef，也不建立语义宽泛的 `Core / Models / Services`。

## 后续必须补齐的设计约束

以下内容不一定在当前 Sprint 实现，但在进入对应后续架构任务前应逐步明确，否则轻量 GAS 变种会在后期变得难以维护。

### 术语边界

需要明确项目内这些概念的职责边界：

| 术语 | 建议含义 |
|------|----------|
| `GameplayTag` | 状态标识、分类、条件匹配，不直接承载生命周期 |
| `BattleState` | 有生命周期的状态对象，例如眩晕、过热、护盾、破坏、警戒 |
| `GameplayAttribute` | 可查询的最终数值或功能开关 |
| `GameplayModifier` | 对属性或流程值的修改器，不拥有生命周期 |
| `Trigger` | 条件满足后的扩展执行点 |
| `Command / Ability` | 主动行动入口，例如移动、攻击、技能、结束回合 |
| `Context` | 一次流程的上下文，例如攻击、伤害、移动、技能释放 |

尤其要避免混淆：

- `Tag` 不是 `State`，Tag 只是标识或条件。
- `Modifier` 不是 `Buff`，Modifier 只修改值。
- `State` 可以提供 Tag 或 Modifier，但 State 本身负责生命周期。
- 主动技能不强制等于 State，复杂技能可以由 Ability / Command / Context 驱动。

### Tag 注册与命名规范

`GameplayTag` 应尽早形成命名规则，避免随手字符串扩散。

建议采用层级命名：

```text
State.Broken.Leg
State.Broken.Arm
State.Control.Stun
State.Control.Root
Locomotion.Walk
Locomotion.Hover
Locomotion.Jump
Attack.Mode.Active
Attack.Mode.Reactive
Attack.Mode.Duel
Timing.Turn.Start
Timing.Action.Before
Timing.Hit.After
BattleResource.ActionPoint
BattleResource.PotentialPoint
```

后续应考虑集中注册或生成，避免运行时拼接未知 Tag。

### Attribute 重算策略

进入 `GameplayAttribute` 阶段前，需要明确属性值如何计算：

- 增量修改：添加 Modifier 时修改，移除时回滚。
- 脏标记重算：Modifier 变化时标脏，查询时重新计算。
- 分层缓存：局外静态层 + 局内动态层。

当前更推荐预留脏标记接口，Sprint 10 先做简单重算，Sprint 13+ 在 Utility AI 大量读取属性时再做两层缓存。

还需要保留属性来源追踪能力，例如：

```text
MoveRange = 4
  Base: 3
  LegModule: +1
  State.Broken.Leg: Override 1
```

否则后期很难解释“为什么这个单位当前移动力是 1”。

### Modifier 计算顺序

Modifier 必须有明确计算顺序，否则数值不可预测。

建议先采用简单层级：

```text
Base
Add
Multiply
Override
Clamp
FinalModify
```

Sprint 3～8 可以不实现完整 Modifier 系统，但一旦进入 Sprint 10，就要明确：

- 同层 Modifier 的合并规则
- Override 之间的优先级
- Clamp 的上下限来源
- Modifier 来源记录
- Modifier 生效时机

### 调试工具需求

GAS 类架构没有调试工具会很难排查问题。后期至少需要能查看：

- 单位当前 `GameplayTag` 列表
- 单位当前 `BattleState` 列表
- 属性当前值和来源
- 生效中的 Modifier
- 最近触发的 Trigger
- 最近一次 Attack / Damage / Move Context
- 回合内 Command 记录

这些可以先从日志开始，不必一开始做完整 Editor 面板。

### 确定性、悔棋与回放边界

如果未来要支持悔棋、续战、录像或战斗复现，需要尽早保证：

- 行动通过 `Command` 表达。
- 随机数通过 `BattleRNG` 管理。
- 每次行动的输入、结果、影响目标可记录。
- 关键流程不依赖 Unity 时间、帧率或场景对象。
- `Context` 不保存不可序列化对象。

这部分不要求早期原型完成，但其后新增系统时要避免破坏确定性。

### 战斗实体身份与动态单位生命周期

`CombatUnit.UnitId` 表示某一场战斗中的稳定实体身份，不等同于驾驶员配置 ID、机甲配置 ID、对象哈希或 Unity Instance ID。Snapshot、ActionLog、Command 和回放都只通过该数字身份引用单位，不保存跨运行时对象引用。

当前约束：

- 初始单位由战斗初始数据显式提供大于零的 `UnitId`，`CombatUnitFactory` 负责传入，`CombatUnitRegistry` 负责校验唯一性并提供按 ID 查询。
- 不使用静态全局自增，也不让注册顺序隐式决定 ID；同一份初始战斗数据应能在测试、服务端和回放中创建出相同的数字身份。
- 回放恢复时创建的是新的 `CombatUnit` 对象，但复用 Snapshot 或日志中的相同 `UnitId` 与数据，因此无需保留原对象引用。

动态召唤按真实玩法需求分阶段引入：

1. **Task 2.7**：只完成初始单位稳定 ID、注册唯一性、Snapshot 和 ActionLog，不实现动态召唤。
2. **Sprint 4 或其后**：仅当首个真实主动召唤技能进入开发，并且 Sprint 3 的目标 / 占格规则与最小技能流程已经稳定后，将召唤作为独立垂直切片实现。
3. **Sprint 13+ 天赋 / Trigger 阶段**：天赋触发召唤通过 Trigger 或 Ability 复用统一召唤入口，不直接修改单位注册表；若此前没有真实召唤玩法，则到首个天赋召唤需求出现时再补齐。

动态召唤进入开发时，必须同时明确并验证：

- `BattleSession` 的本场 ID 分配器及 Snapshot 中的下一可用 ID。
- 出生位置、占格、阵营和注册校验。
- 新单位加入行动队列以及当回合能否行动的规则。
- `UnitSpawned` 结果、日志和回放中的稳定 ID。
- 表现层单位、模型、血条的创建与销毁。
- 召唤物死亡、离场及召唤者失效后的生命周期规则。

召唤能力不能只实现为“创建一个 `CombatUnit`”，也不应由具体技能或天赋各自维护一套注册、ID 和回放逻辑。

### 阵营、行动角色与本地视角

阵营、流程阶段、单次行动角色和本地表现关系属于不同维度，不能共用同一个枚举或字段。

讨论中排除了直接使用 `Player / Enemy / Neutral` 作为长期阵营定义的方案。`Player` 与 `Enemy` 是客户端观察者的相对称呼：同一个 PVP 单位在一方客户端是己方，在另一方客户端则是敌方，无法作为服务端权威状态、录像或重连数据中的稳定身份。

当前采用四层模型：

| 层级 | 推荐表达 | 职责 |
|------|----------|------|
| 持久阵营 | `CombatFaction.TeamA / TeamB / Neutral` | 表示战斗内稳定归属，服务端、快照和回放共同使用 |
| 流程阶段 | `TurnPhase` | 表示战斗流程当前所处阶段，不作为阵营参数 |
| 单次行动角色 | `AttackerUnitId / DefenderUnitId` | 表示一次攻击中的进攻方与防守方，下一次行动可以互换 |
| 本地表现关系 | `Ally / Enemy / Neutral` | 由客户端根据本地玩家所属阵营映射，只影响 UI、颜色、镜头和动画 |

使用规则：

- PVE 中可映射为 `TeamA = 玩家阵营`、`TeamB = AI 阵营`、`Neutral = 中立单位`。
- PVP 中由服务端固定分配 `TeamA / TeamB`，两个客户端接收完全相同的阵营和结算数据。
- `Neutral` 不默认等于敌对，其可攻击性、AI 行为和胜负归属应由独立规则决定。
- `Attacker / Defender` 只描述单次行动角色，不能用于推断单位的持久阵营。
- `Ally / Enemy` 不写入服务端权威状态、快照或回放日志，由观看端根据观察者阵营计算。
- 如果未来需要三方以上混战或动态结盟，将 `CombatFaction` 枚举升级为稳定的 `FactionId`，上层接口保持按阵营标识查询。

### 回放技术选型

回放要区分两种目标：

```text
复现战斗结果：重新跑战斗逻辑，要求确定性。
再现战斗表现：按权威结果播放，不要求客户端重新算出同样结果。
```

因此后续回放建议分层设计：

| 类型 | 数据内容 | 适用场景 | 特点 |
|------|----------|----------|------|
| Command Replay | 初始快照 + Command 序列 + RNG 种子/游标 | 本地回放、开发调试、确定性验证 | 数据小，但要求逻辑、配置和 RNG 完全一致 |
| Result Replay | 初始快照 + 每步权威结果日志 | PVP 回放、分享回放、排行榜、防作弊 | 更稳，不依赖观看端重新结算，数据更大 |
| 混合日志 | Command + 权威 Result + 关键表现事件 | 长期推荐方案 | 既能追踪玩家意图，又能按权威结果稳定播放 |

#### Command 意图与 Action 事实分层

正式进入 Command Replay、回放或 PVP 前，应增加独立 `BattleCommandLog`，避免只记录结果而丢失失败操作的原始请求，也避免把本地 UI 点击混入权威战斗历史。

```text
BattleCommandLog
  Sequence
  CommandType
  ActorUnitId
  RequestedTargetPosition?
  RequestedTargetUnitId?
  Accepted
  FailureReason

BattleActionLog
  Sequence
  ActionType
  ActorUnitId / TargetUnitId / NextUnitId
  FailureReason
  ChangeTypes
  Before / After

BattleSnapshot
  指定时刻的完整世界状态
```

三者职责：

- `BattleCommandLog` 记录玩家、AI 或服务端“想做什么”以及 Session 是否接受，用于解释失败移动原本点击了哪个格子、失败攻击原本选择了哪个目标。
- `BattleActionLog` 记录战斗核心“实际发生了什么”，保持客观阵营、稳定单位 ID、失败原因和有序状态变化。
- `BattleSnapshot` 记录指定时刻的完整状态，用于诊断、恢复和回放检查点。

本地点击、选中单位、未选中拒绝和无效 Collider 等尚未提交到 `BattleSession` 的交互只属于非权威 `InputTrace`，不进入 BattleCommandLog 或权威 BattleLog。空引用、未知运行时对象等调用契约错误进入异常日志，不伪装成玩家指令失败。

阶段边界：

- Task 2.7 已只实现轻量 `BattleActionLog + BattleSnapshot`，没有提前建立 Command 体系。
- Sprint 12 引入 `BattleCommand` 时同步增加 `BattleCommandLog`，并用相同 `Sequence` 关联 Command、ActionResult 和关键 Snapshot。
- PVP 分享回放使用 Command + 权威 ActionResult 的混合日志，本地 `InputTrace` 不上传为战斗事实。

PVP 和可分享回放更推荐使用“Command + 权威 Result 混合日志”：

```text
客户端提交 CommandIntent
-> 服务端校验并结算
-> 服务端记录 Command + CommandResult / BattleResult
-> 分享时其他客户端下载权威 BattleLog
-> 本地按 Result 播放表现
```

权威 BattleLog 应记录客观事实，而不是任一客户端的本地敌我关系。攻击结果至少包含稳定的单位与阵营标识、单次行动角色和服务端结算结果，例如：

```text
AttackerUnitId
DefenderUnitId
AttackerFactionId
DefenderFactionId
Damage
StateChanges
```

所有客户端和回放观看端读取同一份日志；客户端仅根据本地玩家或当前观察者所属阵营，将稳定阵营映射为 `Ally / Enemy / Neutral` 并播放对应表现。命中、伤害、死亡和状态变化等权威结果不得按本地视角重新结算。

PVE 如果需要排行榜、防作弊、分享回放，也应尽量上传服务器认可的 BattleLog，而不是只分享本地录像文件。

本地调试和单机复盘可以继续使用 Command Replay，但它不能替代权威分享回放。

### 整数化战斗逻辑

PVP、悔棋、续战、录像和服务端权威结算都会要求战斗结果可复现、可校验。

因此，战斗逻辑层应优先使用整数表达规则：

- 坐标：整数格子坐标。
- HP / AP / 护盾 / 热量：整数。
- 概率：整数万分比，例如 `7500 = 75%`。
- 倍率：整数缩放值，例如 `15000 = 1.5 倍`。
- 减免：整数万分比，例如 `6600 = 66%`。
- 固伤率：整数万分比，例如 `1020 = 10.2%`。

浮点数只用于表现层动画、插值、相机、特效和 UI 过渡，不参与核心战斗结算。

使用整数逻辑的原因：

1. **结果确定**：不同平台、不同运行环境不容易出现浮点误差。
2. **便于服务端校验**：PVP 中客户端只提交指令，服务端用同一套整数规则校验并结算。
3. **便于回放复现**：Command + RNG + 整数公式更容易重放出完全一致的结果。
4. **便于配置解释**：策划配置 `40%`、`66%`、`10.2%` 时，可以精确映射为整数比例。
5. **便于取整约束**：所有出现非整数结果的地方都必须声明取整策略，避免隐藏规则。

示例：

```text
基础子弹数 = 18
子弹数加成 = 40% = 4000
最终子弹数 = Ceil(18 * (10000 + 4000) / 10000) = 26
```

```text
基础固伤率 = 30% = 3000
Boss 固伤减免 = 66% = 6600
最终固伤率 = 3000 * (10000 - 6600) / 10000 = 1020
1020 = 10.2%
最终伤害 = Floor(目标最大 HP * 1020 / 10000)
```

需要明确的规则：

- 每个公式的取整时机。
- 每个公式的取整方式：`Floor`、`Ceil`、`Round`、`Trunc`。
- 是否存在最低值，例如最低 1 点伤害、最低 1 点 AP 消耗。
- 中间比例是否保留缩放整数，通常推荐中间不提前取整，最终落到离散值时再取整。

完整定点数库暂时不是当前回合制核心必需项。只有当后期出现实时 Lockstep、连续坐标、弹道物理、复杂曲线结果参与战斗逻辑时，才重新评估是否引入定点数库。

### 事件边界

事件系统只作为扩展点，不替代核心流程。

应保持显式流程：

```text
Validate -> Execute -> ApplyResult -> Notify
```

不推荐把移动、攻击、扣血、死亡这类主流程拆成一串无序事件监听。

推荐事件用途：

- UI 刷新通知
- 战斗日志
- 触发器扩展点
- 成就/任务统计
- 表现层播放提示

核心规则：

```text
主流程显式，扩展点事件化。
```
