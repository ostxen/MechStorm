# MechStorm 战斗架构参考落地指南

分析对象：

- GAS 简化系统参考：<https://github.com/LiGameAcademy/godot_ability_system>
- 回合制战斗系统参考：<https://github.com/LiGameAcademy/godot4_turn_based_combat_system>
- Buff / 持续状态专题：[`ScalableBuffSystemArchitecture.md`](../BuffSystems/ScalableBuffSystemArchitecture.md)

前两个参考实现恰好使用 Godot，Buff 专题来自 AbilityKit 作者对持续状态边界的进一步拆解。本文只提炼可迁移到 MechStorm 纯 C# 无头核心的架构职责和落地顺序，不以引擎类型或外部框架组织结论。

当前 MechStorm 阶段：P1 / Sprint 2，任务入口是 `BattleController` / `BattleSession`，目标是多单位、移动、普通攻击、扣血、死亡、回合推进与表现同步。结论是：两个 Godot 项目都可以作为设计思路参考，但都不能直接照搬。MechStorm 的主线仍应保持无头核心、显式流程、整数化、结果对象、逐步数据驱动。

## A. 阅读方式与总评

### A.1 结论拆分方式

下面的结论分成两类：

1. **整体评价**：判断这两个仓库自身的架构设计水平、扩展性、边界问题和可作为参考的可信度。
2. **对项目的借鉴**：只抽取适合 MechStorm 当前阶段和长期路线的部分，转换成纯 C#、无头核心、可测试、可快照的设计建议。

这两个层次必须分开看：一个仓库“架构方向值得参考”，不等于“可以直接接入项目”；一个实现“当前有瑕疵”，也不等于“它的设计拆分没有学习价值”。

### A.2 整体评价

#### A.2.1 godot4_turn_based_combat_system

这个仓库的架构设计属于“有清晰拓展意识的教学型战斗系统”。它把战斗流程拆成了 `BattleManager`、`BattleStateManager`、`TurnOrderManager`、`BattleCharacterRegistryManager`、`CombatRuleManager` 等模块，也用 `SkillData + SkillEffect + SkillStatusData` 表达技能、效果和状态。

优点：

- 战斗流程没有全部堆在一个类里。
- 角色注册表和阵营查询职责清楚。
- 回合队列独立，后续能扩展速度排序、插队、再行动。
- 技能效果使用组合方式，后续可扩展伤害、治疗、状态、反击、驱散。
- 状态系统有事件触发雏形，例如受伤、回合开始、反击等。
- 使用上下文对象承载技能和伤害流程数据。

不足：

- 逻辑和表现耦合明显，技能执行里有移动、动画、等待和特效。
- `SkillSystem` 是全局单例，不适合多战斗实例、测试、回放和服务端复用。
- `SkillStatusData` 既保存静态配置又保存运行时状态，不利于快照和悔棋。
- 事件、行动限制等用自由字符串，缺少集中注册和层级匹配。
- 非确定性随机和浮点计算不适合 MechStorm 的确定性战斗核心。
- 部分高级效果代码像教学迭代残留，不是完整生产闭环。

评价：**适合作为 Sprint 2 战斗流程拆分的施工参考，不适合作为长期底座直接搬入 MechStorm。**

#### A.2.2 godot_ability_system

这个仓库的架构抽象更完整，更接近轻量 GAS。它把 Ability、Attribute、Status、Effect、Tag、Cue、Targeting、Behavior Tree 和调试 UI 都拆出来了，整体设计比回合制教学库更系统。

优点：

- 静态定义和运行时实例分离较明确。
- Ability Feature 可以组合冷却、消耗、输入、被动等能力。
- Effect 原子化，能复用到技能、状态和事件触发。
- Status 管生命周期，Attribute/Modifier 管数值，方向正确。
- Tag 支持引用计数、继承、互斥，长期价值高。
- 调试工具意识强，有 Tag、Status、BT 调试面板。

不足：

- 偏实时动作游戏和 Godot 插件，不是回合制 SRPG 核心。
- 依赖 Node、Resource、Autoload、`_process(delta)` 和 Godot 信号。
- 行为树技能系统对 MechStorm 当前阶段过重。
- 自然时间持续状态不适合回合制 Timing Anchor。
- 全局单例不利于纯 C# BattleSession、测试和服务端复用。

评价：**适合作为 P2/P3 轻量 GAS 的架构词典，不适合作为 Sprint 2 的落地模板。**

### A.3 对 MechStorm 的借鉴

#### A.3.1 当前阶段采用哪个仓库作为落地参考

当前 P1/Sprint 2 更应该参考 `godot4_turn_based_combat_system`，因为它解决的是战斗流程如何拆分，而 MechStorm 现在正要做 `BattleSession`、多单位、当前行动单位、移动、普通攻击、死亡和回合推进。

`godot_ability_system` 暂时只作为中长期校准，不参与当前落地。等 MechStorm 进入技能、Buff、Tag、Modifier、Trigger 阶段，再回头参考它的模块边界。

#### A.3.2 MechStorm 应吸收的结构

从 `godot4_turn_based_combat_system` 吸收职责拆分，而不是代码实现：

```text
BattleManager
-> BattleSession / BattleController

BattleStateManager + TurnOrderManager
-> TurnCoordinator

BattleCharacterRegistryManager
-> CombatUnitRegistry

CombatRuleManager
-> BattleRuleChecker

SkillSystem
-> 后续 AbilityResolver

SkillEffect
-> 后续 BattleEffect
```

#### A.3.3 MechStorm 必须保留自己的边界

接入参考架构时，必须坚持：

- `MechStorm.Battle` 纯 C#，不依赖 Unity。
- 不把动画、UI、输入、特效放进核心。
- 不使用全局单例承载战斗核心。
- 静态配置和运行时状态分离。
- 行动返回结构化 `BattleActionResult`。
- 随机数由确定性 `BattleRNG` 管理。
- 浮点只用于表现层，核心结算用整数。
- 状态、Tag、Modifier、Trigger 后置，不抢 Sprint 2 主线。

#### A.3.4 推荐落地顺序

当前只落地：

```text
BattleSession
CombatUnitRegistry
TurnCoordinator
BattleRuleChecker
BattleActionResult
BattleSnapshot / BattleActionLog 雏形
```

暂缓落地：

```text
AbilityDefinition
BattleEffect
GameplayTagContainer
BattleStateRuntime
AttributeModifier
Trigger
Behavior Tree Ability
```

一句话：**先用回合制教学库校准“战斗流程怎么拆”，再用 Ability System 仓库校准“复杂技能和状态后续怎么扩”。**

## B. 两个代码库的定位

### B.1 godot_ability_system

它解决的是“复杂技能系统如何模块化”的问题。

核心思想：

```text
AbilityDefinition 静态定义
-> AbilityInstance 运行时实例
-> AbilityComponent 持有和调度
-> Feature 扩展消耗、冷却、输入、被动
-> Behavior Tree 描述技能时序
-> Effect 执行原子效果
-> Status 管 Buff/Debuff 生命周期
-> Attribute / Modifier 管数值
-> Tag 管分类、过滤、互斥
-> Cue 管表现反馈
```

它适合参考 MechStorm 的长期轻量 GAS 变种。

### B.2 godot4_turn_based_combat_system

它解决的是“回合制战斗流程如何拆分”的问题。

核心思想：

```text
BattleManager 总协调
-> BattleStateManager 管流程状态
-> TurnOrderManager 管行动队列
-> BattleCharacterRegistryManager 管阵营和存活单位
-> CombatRuleManager 管胜负规则
-> SkillSystem 管技能执行
-> SkillEffect 管效果组合
-> SkillStatusData 管状态和触发效果
```

它适合参考 MechStorm 的 Sprint 2 战斗流程拆分。

## C. 可吸收的设计原则

### C.1 静态定义和运行时实例必须分离

两个仓库都体现了这个方向，只是第二个仓库有混用问题。

MechStorm 应坚持：

```text
PilotData / PilotRuntime
MechData / MechRuntime
AbilityData / AbilityRuntime
StateDefinition / StateRuntime
AttributeDefinition / AttributeRuntime
```

收益：

- 便于测试。
- 便于快照。
- 便于悔棋。
- 便于回放。
- 便于服务端复用。
- 便于 Luban 配表接入。

### C.2 战斗流程要拆出局部服务，不要让入口类继续膨胀

参考 `godot4_turn_based_combat_system` 的拆分，MechStorm Sprint 2 可演进为：

```text
BattleSession
  战斗上下文，持有棋盘、单位、回合状态和规则服务

CombatUnitRegistry
  管理全部单位、阵营、存活、死亡、敌我关系

TurnCoordinator
  管理当前行动单位、行动结束、死亡跳过、阵营切换

BattleRuleChecker
  管理胜负判断和基础合法性

BattleActionResult
  统一承载逻辑结果，表现层按结果刷新
```

这正好对应当前 `DEVELOPMENT_PROGRESS.md` 中 Task 2.1 到 Task 2.6。

### C.3 Effect 原子化，但不要急着上完整 GAS

`godot_ability_system` 的 `GameplayEffect` 和 `godot4_turn_based_combat_system` 的 `SkillEffect` 都说明：技能最终应拆成可组合的原子效果。

MechStorm 可在 Sprint 3 引入：

```text
IBattleEffect
DamageEffect
ApplyStateEffect
ModifyAttributeEffect
GrantActionQuotaEffect
```

但 Sprint 2 只需要移动、普通攻击、死亡和回合推进，不要提前实现完整 Effect 框架。

### C.4 Context 是复杂战斗的必需品

两个仓库都使用上下文承载流程临时数据。MechStorm 后续应逐步引入：

```text
MoveContext
AttackContext
DamageContext
AbilityExecutionContext
TurnContext
```

规则：

- Context 只承载一次流程的数据。
- Context 不保存 Unity 对象。
- Context 不依赖帧时间。
- Context 可序列化或可转为日志。

### C.5 DamageInfo / DamageContext 很值得保留

第二个仓库的 `DamageInfo` 思路适合 MechStorm 后续部位破坏、护盾、反击、减伤、溢出转移。

MechStorm 可以设计为：

```text
DamageContext
  Attacker
  Defender
  TargetPart
  BaseDamage
  DamageTypeTags
  AttackModeTags
  Timing
  FinalDamage
  AppliedResults
```

但当前 Sprint 2 的普通攻击可以先保持简单，等部位破坏和命中率进入 P1/Sprint 3 后再扩展。

### C.6 Tag 引用计数和互斥关系值得提前记住

`godot_ability_system` 的 TagManager 支持引用计数和互斥。MechStorm 后期也会需要：

```text
State.Broken.Leg
State.Control.Stun
Locomotion.Hover
Forbidden.Move
Forbidden.Attack
Attack.Mode.Active
Timing.Turn.Start
BattleResource.ActionPoint
```

推荐长期设计：

```text
AddTag(sourceId, tag)
RemoveTagsBySource(sourceId)
HasTag(tag)
HasAllTags(tags)
HasAnyTag(tags)
GetTagSources(tag)
```

### C.7 调试工具要和复杂度同步建设

两个仓库都体现了调试价值，尤其是第一个仓库的 Tag、Status、Behavior Tree 调试面板。

MechStorm 当前 Task 2.7 的轻量调试导出很重要。建议至少输出：

```text
BattleSnapshot
  Round
  CurrentTeam
  CurrentUnitId
  Units
  Positions
  HP
  IsDead
  HasActed

BattleActionLog
  ActionType
  ActorId
  Targets
  BeforeState
  Results
```

后续 GAS 阶段再扩展：

```text
ActiveTags
ActiveStates
AttributeBreakdown
Modifiers
RecentTriggers
RecentContexts
```

## D. 必须保留的 MechStorm 边界

### D.1 不照搬 Godot Node / Resource 架构

MechStorm 的 `MechStorm.Battle` 必须是纯 C#，不能依赖：

- UnityEngine
- Godot Node
- Godot Resource
- 场景树
- 信号生命周期
- `_process(delta)`
- 全局 Autoload

### D.2 不把表现和逻辑混在一起

两个 Godot 项目都有不同程度的表现耦合，例如动画、移动、伤害数字、Cue、粒子、等待计时。

MechStorm 必须保持：

```text
Battle 核心：只结算，返回结果
Presentation：读取结果，播放动画、特效、血条、日志
```

### D.3 不用全局单例做战斗核心

Godot 的 `SkillSystem`、`TagManager`、`AbilityEventBus` 很方便，但 MechStorm 应避免核心全局单例。

推荐：

```text
BattleSession
  UnitRegistry
  TurnCoordinator
  MovementResolver
  AttackResolver
  RuleChecker
  BattleRng
```

所有依赖由构造函数或初始化参数注入，方便测试和多战斗实例。

### D.4 不在 Sprint 2 引入行为树技能系统

行为树技能系统适合复杂实时技能，不适合当前最小战斗流程。

Sprint 2 应坚持：

```text
显式 Move
显式 Attack
显式 EndAction
显式 TurnAdvance
结构化 Result
```

### D.5 不用浮点和非确定性随机参与核心结算

MechStorm 已决策：

- 坐标、HP、AP、伤害用整数。
- 概率和倍率用万分比或整数缩放。
- 随机由 `BattleRNG` 管理。
- 浮点只用于表现层。

Godot 仓库里的 `randf()`、`randf_range()`、自然时间持续、浮点倍率都不能直接搬到核心。

### D.6 不让配置对象持有运行时状态

尤其要避免第二个仓库 `SkillStatusData` 这类模板和运行时混合。

MechStorm 应拆清：

```text
Definition / Data：静态配置
Runtime：战斗内状态
Result：一次行动的输出
Snapshot：可恢复状态
Log：可读历史记录
```

## E. 分阶段落地建议

### E.1 当前 Sprint 2

#### E.1.1 BattleSession 最小职责

建议职责：

```text
BattleSession
  SquareGrid Grid
  CombatUnitRegistry Units
  TurnCoordinator Turn
  MovementResolver Movement
  AttackResolver Attack

  MoveUnit(unitId, targetPosition) -> BattleActionResult
  AttackUnit(attackerId, targetId) -> BattleActionResult
  EndCurrentAction() -> BattleActionResult
```

不要让 `TempGameEntry` 继续直接持有全部战斗规则。

#### E.1.2 CombatUnitRegistry

可参考 Godot 注册表，MechStorm 纯 C# 版本建议：

```text
RegisterUnit(unit, team)
UnregisterUnit(unitId)
GetAllUnits()
GetAliveUnits()
GetUnitsByTeam(team, aliveOnly)
GetOpposingUnits(unit)
IsEnemy(unitA, unitB)
AreAllUnitsDead(team)
```

注意：死亡单位不一定要从列表物理删除，保留在列表中更利于快照、日志、回放和 UI 灰显。可以通过 `IsDead` 过滤。

#### E.1.3 TurnCoordinator

当前阶段可先做阵营顺序，不急着速度队列：

```text
Player team units
-> Enemy team units
-> Skip dead units
-> Mark acted units
-> All acted then switch team
-> New team turn reset action state
```

后续再扩展：

- 速度排序
- 再行动
- 插队
- 行动配额
- 眩晕跳过

#### E.1.4 BattleActionResult

建议 Sprint 2 就做结果对象，为表现层同步做准备：

```text
BattleActionResult
  IsSuccess
  FailureReason
  ActorId
  Events[]

BattleEvent
  UnitMoved
  DamageApplied
  UnitDied
  TurnChanged
  ActionEnded
```

这比直接让表现层到处读逻辑对象更稳定。

#### E.1.5 BattleSnapshot / ActionLog

Task 2.7 可以先做轻量：

```text
BattleSnapshot
  GridSize
  CurrentTeam
  CurrentUnitId
  Units[]

UnitSnapshot
  UnitId
  Team
  Position
  CurrentDurability
  MaxDurability
  IsDead
  HasActed
```

后续再加：

- Tags
- States
- Attributes
- Modifiers
- RNG 游标
- Command 序列

### E.2 Sprint 3

#### E.2.1 最小主动技能，不上完整 GAS

参考两个仓库，但只做最小闭环：

```text
AbilityDefinition
  AbilityId
  Cost[]
  Range
  TargetRule
  Effects[]

AbilityResolver
  CanUseAbility()
  ResolveAbility()
  Return AbilityResult
```

#### E.2.2 目标选择和过滤拆开

参考 `TargetingStrategy` 和 Filter：

```text
ITargetSelector
  根据范围、格子、武器类型找候选目标

ITargetFilter
  根据阵营、死亡、Tag、LOS、地形过滤目标
```

#### E.2.3 普通攻击可逐步迁移成 Ability

先让 `AttackResolver` 稳定，再抽象为：

```text
BasicAttackAbility
  Cost: AP
  TargetRule: Enemy in range
  Effects: DamageEffect
```

不要为了抽象而提前重构。

### E.3 P2/P3 轻量 GAS

推荐路线：

```text
P1: BattleSession / Result / Snapshot
P2: AttributeSet / StateRuntime / GameplayTagContainer
P3: Modifier / Trigger / Timing / AbilityEffect
P4: Luban 全量数据驱动和调试面板
```

核心原则：

```text
主流程显式
扩展点事件化
状态管生命周期
Modifier 只改值
Tag 只做标识和匹配
Context 承载一次流程
Result 驱动表现
Snapshot 服务悔棋和回放
```

### E.4 优先级建议表

| 参考点 | 当前是否采用 | 建议阶段 | 原因 |
|---|---:|---|---|
| 单位注册表 | 是 | Sprint 2 | 多单位和阵营管理马上需要 |
| 回合协调器 | 是 | Sprint 2 | 当前任务核心 |
| 结构化 Result | 是 | Sprint 2 | 表现同步和日志需要 |
| 轻量 Snapshot | 是 | Sprint 2 后段 | 调试和后续回放基础 |
| SkillEffect 组合 | 暂缓 | Sprint 3 | 技能雏形再引入 |
| TargetSelector / Filter | 暂缓 | Sprint 3 | 攻击范围和技能范围再引入 |
| GameplayTagContainer | 暂缓 | P1 后段或 P2 | 部位破坏、移动能力、状态变多后引入 |
| Attribute / Modifier | 暂缓 | P2 | 过早引入会拖慢 Sprint 2 |
| Trigger / EventBus | 暂缓 | P2/P3 | 等反击、警戒、Buff 需求出现 |
| 行为树技能 | 不采用 | 暂无 | 当前战棋核心不需要 |
| Godot 式全局单例 | 不采用 | 永不进核心 | 不利于测试、回放、服务端 |

### E.5 一句话结论

`godot4_turn_based_combat_system` 更适合参考 MechStorm 当前 Sprint 2 的战斗流程拆分，`godot_ability_system` 更适合参考中后期轻量 GAS 的模块边界和调试思路。当前最稳妥的路线是：先做纯 C# `BattleSession + Registry + TurnCoordinator + Result`，等普通移动、攻击、回合推进稳定后，再逐步吸收 Ability、Effect、State、Tag、Modifier、Trigger。

## F. 深度复查后的参考价值重估

经过更深入的代码级复查后，两个仓库对 MechStorm 的价值可以重新排序如下。

### F.1 当前最有价值的不是“系统完整性”，而是“职责拆分样本”

`godot4_turn_based_combat_system` 的代码不是生产级框架，但它给了一个很清晰的早期拆分样本：

```text
BattleManager
BattleStateManager
TurnOrderManager
BattleCharacterRegistryManager
CombatRuleManager
CharacterCombatComponent
CharacterSkillComponent
SkillSystem
SkillEffect
SkillStatusData
```

对 MechStorm 来说，不要复制这些类的实现，而要复制它们试图解决的问题：

```text
谁持有战斗状态？
谁管理单位名单？
谁决定当前行动者？
谁判断胜负？
谁执行行动？
谁表达技能效果？
谁表达状态持续和触发？
谁把逻辑结果交给 UI？
```

这些问题比具体代码更重要。

### F.2 当前 Sprint 2 应落地的纯 C# 对照

建议把回合制教学库的类组转换成 MechStorm 的纯 C# 版本：

| Godot 类组 | MechStorm 建议类 | 是否当前落地 | 说明 |
|---|---|---:|---|
| `BattleManager` | `BattleSession` / `BattleController` | 是 | 战斗核心入口，不能承担表现职责 |
| `BattleCharacterRegistryManager` | `CombatUnitRegistry` | 是 | 多单位、阵营、存活、敌我查询马上需要 |
| `TurnOrderManager` | `TurnCoordinator` | 是 | 当前行动单位和回合推进马上需要 |
| `CombatRuleManager` | `BattleRuleChecker` | 是 | 胜负、全灭、异常输入判断 |
| Godot 信号结果 | `BattleActionResult` / `BattleEvent` | 是 | 表现同步、测试、日志和快照都依赖 |
| `SkillSystem` | `AbilityResolver` | 暂缓 | Sprint 3 再引入 |
| `SkillEffect` | `BattleEffect` | 暂缓 | 技能和状态复杂后再引入 |
| `SkillStatusData` | `StateDefinition` + `StateRuntime` | 暂缓 | 必须拆配置和运行时 |
| `DamageInfo` | `DamageContext` | Sprint 3 可引入 | 部位破坏、护盾、减伤和反击需要 |

当前推荐最小骨架：

```text
BattleSession
  SquareGrid Grid
  CombatUnitRegistry UnitRegistry
  TurnCoordinator TurnCoordinator
  BattleRuleChecker RuleChecker
  MovementResolver MovementResolver
  AttackResolver AttackResolver

  MoveUnit(...)
  AttackUnit(...)
  EndCurrentUnitAction(...)
```

### F.3 运行时调用流程给 MechStorm 的启发

#### F.3.1 Godot 教学库的实际调用

```text
BattleScene.initialize_battle()
-> BattleManager.add_character()
-> BattleCharacterRegistryManager.register_character()
-> Character.initialize()
-> BattleManager.start_battle()
-> BattleStateManager.change_state(START)
-> ROUND_START 构建 turn queue
-> TURN_START 取 current character
-> PLAYER_TURN 等 UI
-> player_select_action()
-> CharacterCombatComponent.execute_action()
-> SkillSystem.attempt_execute_skill()
-> SkillEffect.process_effect()
-> CharacterCombatComponent.take_damage()
-> SkillSystem.trigger_game_event()
-> TURN_END / ROUND_END / VICTORY / DEFEAT
```

#### F.3.2 MechStorm 应改成的调用

```text
TempGameEntry / Presentation
-> BattleSessionFactory.Create(...)
-> BattleSession.Start()
-> BattleSession.GetSnapshot()

玩家输入
-> Presentation 转成纯逻辑参数
-> BattleSession.MoveUnit(...) / AttackUnit(...) / EndCurrentUnitAction(...)
-> 返回 BattleActionResult
-> Presentation 按 Result 播动画、刷新血条、日志

敌方 AI
-> Ai.Decide(BattleSnapshot)
-> BattleSession.Execute(command)
-> 返回 BattleActionResult
```

差异重点：

- Godot 是信号 + await + 节点对象。
- MechStorm 应是纯 C# 方法调用 + Result。
- Godot 逻辑里播放动画。
- MechStorm 逻辑只返回数据，表现层播放动画。

### F.4 Ability System 仓库的高价值部分

#### F.4.1 Ability 生命周期

```text
CanActivate
-> BuildContext
-> SelectTargets
-> CommitCost
-> ResolveEffects
-> CommitCooldownOrActionCost
-> EmitResult
```

#### F.4.2 Feature 降级为强类型 Rule

Godot 的 `GameplayAbilityFeature` 很灵活，但对 MechStorm 应拆成：

```text
IAbilityAvailabilityRule
IAbilityCostRule
IAbilityTargetRule
IAbilityCommitRule
IAbilityResultModifier
```

这样比 Dictionary + Hook 更容易测试、序列化和调试。

#### F.4.3 Effect 原子化

可吸收为：

```text
BattleEffect
DamageEffect
ModifyAttributeEffect
ApplyStateEffect
RemoveStateEffect
TransformStateEffect
```

要求所有 Effect：

- 纯 C#。
- 无 Unity 依赖。
- 无表现调用。
- 使用整数规则。
- 返回 `BattleEffectResult`。

#### F.4.4 TargetingStrategy 转成 SRPG 目标管线

```text
TargetShape
TargetFilter
TargetSorter
TargetPicker
```

对应战棋需求：

```text
范围形状
阵营过滤
死亡过滤
部位过滤
LOS 过滤
Tag 条件
随机目标，但必须使用 BattleRNG
```

#### F.4.5 State / Modifier / Tag 组合

后期建议：

```text
StateRuntime 持有生命周期
StateRuntime 提供 Tags / Modifiers / Triggers
TagContainer 记录来源
Modifier 按来源清理
Trigger 绑定 Timing Anchor
```

这是 MechStorm 轻量 GAS 的主干。

### F.5 复查后更明确不能照搬的点

#### F.5.1 不能照搬全局事件总线

Ability System 仓库里所有 `GameplayStatusComponent` 都可能监听全局 `AbilityEventBus`。如果事件没有 target/source 过滤，就可能出现跨实体误触发。

MechStorm 应采用：

```text
BattleEventDispatcher
  Scope: Battle / Unit / Action
  Timing: Turn.Start / Action.Before / Hit.After ...
  SourceUnitId
  TargetUnitId
  ContextId
```

#### F.5.2 不能照搬 Godot TagManager

Godot TagManager 是 tag 级引用计数，不是 source-aware。MechStorm 需要：

```text
AddTag(sourceId, tag)
RemoveTagsBySource(sourceId)
GetTagSources(tag)
```

否则装备、地形、Buff、部位破坏同时提供同一 Tag 时会很难清理。

#### F.5.3 不能照搬 Resource 运行时状态

两个仓库都有 Resource 复制或运行时字段混用问题。MechStorm 必须保持：

```text
Definition / Data: 静态配置
Runtime: 当前战斗状态
Context: 一次流程临时数据
Result: 对外输出结果
Snapshot: 可恢复状态
Log: 可读历史
```

#### F.5.4 不能照搬动画等待流程

Godot 教学库在行动、技能、伤害中大量 `await` 移动、动画、Timer。MechStorm 核心必须避免：

```text
逻辑层等待动画完成
逻辑层创建表现对象
逻辑层播放特效
逻辑层使用 Unity 时间
```

### F.6 对 MechStorm 的最终参考路线

#### F.6.1 Sprint 2

只做：

```text
BattleSession
CombatUnitRegistry
TurnCoordinator
BattleRuleChecker
BattleActionResult
BattleSnapshot
BattleActionLog
```

保持：

```text
移动显式
攻击显式
死亡显式
回合推进显式
表现层按 Result 同步
```

#### F.6.2 Sprint 3

引入：

```text
AbilityDefinition
AbilityResolver
TargetRule
BattleEffect
DamageContext
```

普通攻击可以逐步变成一种 Ability，但不要为了抽象提前打乱 Sprint 2。

#### F.6.3 P2 / P3

引入：

```text
GameplayTagContainer
BattleStateRuntime
BattleAttributeSet
AttributeModifier
Trigger
Timing Anchor
Debug Breakdown
```

#### F.6.4 长期准则

```text
主流程显式
扩展点事件化
状态管生命周期
Modifier 只改值
Tag 只做标识和匹配
Context 承载一次流程
Result 驱动表现
Snapshot 服务悔棋和回放
Log 服务调试和复盘
```

### F.7 从代码级风险提炼出的落地约束

以下内容来自两个分析文档中的代码级风险，应用文档中此前没有完整展开，因此迁移到这里作为实现约束。

#### F.7.1 资源提交必须事务化

`godot_ability_system` 的 `AbilityNodeCommitCost` 存在“调用扣费但忽略失败结果”的风险。MechStorm 的 AP / PP / 弹药 / 耐久消耗必须按事务处理：

```text
ValidateCost
-> CommitCost
-> CommitCost 失败则停止后续结算
-> 返回失败 Result
```

不要只在 `CanUse` 阶段检查资源。实际提交时仍要再次确认，并把失败原因写入 `BattleActionResult`。

#### F.7.2 State 应用顺序要固定

Ability System 仓库的状态应用顺序存在“先执行效果，再移除互斥状态”的副作用风险。MechStorm 的 State 应固定为：

```text
PreCheck
-> RemoveConflicts
-> AttachState
-> ApplyTags
-> ApplyModifiers
-> FireOnApplied
-> EmitResult
```

这样可以避免互斥状态短暂共存、Modifier 重复叠加、Tag 清理顺序不稳定等问题。

#### F.7.3 Cue / Projectile / MagicField 只能作为表现或载具概念

Godot 的 Cue、Projectile、MagicField 都依赖场景树、物理、Timer、粒子和随机数。MechStorm 只能吸收“表现请求”和“载具投递 Payload”的概念：

```text
BattleEffectResult
-> BattleCueRequest
-> Presentation 播动画 / 弹道 / 特效 / 飘字
```

不要让 `MechStorm.Battle` 创建弹道物体、等待碰撞或播放 Cue。

#### F.7.4 Timing 事件只能有一个权威派发点

回合制教学库里存在状态持续处理和 `on_turn_start` 事件的双轨残留。MechStorm 后续做 Timing 时，必须保证每个时序节点只有一个权威派发点：

```text
Turn.Start
Turn.End
Action.Before
Action.After
Hit.Before
Hit.After
Battle.Before
Battle.After
```

否则 Buff 持续时间、触发次数、反击、DOT、回合开始效果很容易重复触发或漏触发。

#### F.7.5 关键上下文要用强类型并补测试

回合制教学库里有 `DamageInfo` 参数错位、`SkillEffect.process_effect()` 目标覆盖疑点、绑定信号解绑不一致等问题。MechStorm 应避免弱字典和位置参数堆叠：

```text
DamageContext 使用命名属性
TargetContext 使用明确 TargetIds
BattleEventSubscription 返回可释放 Handle
EffectResolver 对 target override 补测试
```

尤其是 `DamageContext`，后续应能记录：

```text
BaseDamage
FinalDamage
SourceUnitId
TargetUnitId
TargetPart
AttackModeTags
ModificationLog
```

## G. Buff / 持续状态专题的补充指导

详细分析见：[`ScalableBuffSystemArchitecture.md`](../BuffSystems/ScalableBuffSystemArchitecture.md)。

### G.1 对当前 Task 2.6 / Task 2.7 的直接借鉴

当前阶段不实现 Buff 系统，只吸收将来所有状态流程都会依赖的结果边界：

```text
BattleActionResult
  Success
  FailureReason
  Sequence
  ActorId
  TargetId
  Ordered ChangeTypes

BattleSnapshot / BattleActionLog
  只保存稳定 ID、整数数值和客观战斗事实
  不保存 Unity 对象、Visual 引用或短生命周期 Context
```

这样做的价值是先稳定“请求为什么成功或失败”和“按什么顺序发生了什么”，而不是提前实现状态生命周期。

当前明确不引入：

- `BuffRuntime`。
- `ContinuousManager`。
- 通用叠层策略。
- Modifier 和 Trigger。
- 状态恢复 Provider。
- 用全局事件总线驱动主流程。

### G.2 P2 状态主干的最小边界

当部位破坏、眩晕、DOT、护盾和警戒进入真实需求后，再建立：

```text
StateDefinition
StateRuntime
StateApplyRequest
StateApplyResult
StateRemoveReason
StateService
TurnBasedStateScheduler
Source-aware TagContainer
GameplayAttribute
AttributeModifier
StateChangeResult
```

必须保持：

```text
配置描述默认规则
运行时保存本次事实
Rule 决定阶段行为
Result 驱动表现
Snapshot 保存可恢复数据
```

状态实例不能只按名称匹配。至少要明确 `DefinitionId`、`TargetId`、`SourceId` 是否共同参与身份，否则多来源叠层、按来源撤销、光环离开和击杀归因都会不稳定。

### G.3 回合制 Duration / Interval 规则

AbilityKit 文章使用秒级时间，MechStorm 必须转换为强类型回合时序：

```text
Round.Start
FactionTurn.Start
UnitAction.Before
Attack.Before
Hit.Before
Hit.After
Attack.After
UnitAction.End
FactionTurn.End
Round.End
```

`Duration` 表示状态在哪个时序点结束，`Interval` 表示状态在哪个时序点执行周期规则。二者必须独立定义，不能统一隐藏在 `Tick()` 中。

例如“三回合中毒”至少要额外回答：

```text
施加当回合是否立即生效
在哪一方回合开始或结束时扣除持续回合
周期伤害发生在扣除前还是扣除后
刷新 Duration 是否重置下一次 Interval
多个来源共享还是独立计时
```

### G.4 来源感知的 Tag 与 Modifier

MechStorm 的状态、装备、天赋、光环、地形和部位破坏都会提供 Tag 或 Modifier，因此所有投影必须按来源计数或保存来源句柄：

```text
AddTag(tag, sourceId)
RemoveTag(tag, sourceId)

AddModifier(modifier, sourceId)
RemoveModifier(modifierId, sourceId)
```

禁止把 Tag 只实现为无来源的布尔集合后，再由调用方猜测是否可以删除。一个来源结束时只能撤销自己贡献的事实。

### G.5 P3 复杂规则的进入条件

只有出现真实验收需求后，才扩展：

- 多来源叠层。
- 刷新、延长、替换和拒绝策略。
- 计次型护盾。
- 驱散、免疫和互斥。
- 光环 Enter / Leave。
- Trigger 条件链。
- 追击、反击、协力和元件联动。

同时必须提供：

```text
稳定 RuntimeId / SourceId
固定生命周期顺序
结构化 StackDecision / RemoveReason
Trigger 深度和次数上限
可查询的状态来源与属性拆解
Headless 回归样例
```

### G.6 恢复边界

后续 Snapshot 导入状态数据，不等于战斗世界已经恢复可运行。恢复流程还要显式重建：

- State 索引。
- 回合调度。
- Tag 来源。
- Modifier 投影。
- Trigger 订阅。
- Owner / Source 关联。

恢复过程默认不重放伤害、不重复触发 Apply 规则，也不自动播放历史 Cue。数据导入、行为重绑定和表现刷新应是三个明确阶段。

### G.7 不照搬的部分

下列设计不直接采用：

1. 不把 Buff、技能、移动、投射物和区域全部塞入统一 `ContinuousManager`。
2. 不让 Buff 自己依赖 `Update`、Unity Timer 或场景对象。
3. 不用一张 Buff 表承担完整玩法脚本。
4. 不依赖全局事件广播决定状态主流程顺序。
5. 不为 PVP 预先引入客户端预测回滚式 Buff 恢复。

MechStorm 只共享确定存在的生命周期语义，是否共享运行时容器要等真实复用和性能数据出现后再决定。
