# AbilityKit 复杂战斗系统职责拆分分析

文章来源：

- 知乎文章：`游戏开发-战斗系统架构拆解：开源实践`
- 本地归档：用户提供的知乎 HTML 快照
- 文章提到的开源仓库：<https://github.com/HOBOBO/AbilityKit>
- 文章作者：梁祝好
- 发布时间：2026-07-08

解析范围：本文以本地保存的知乎正文为主要依据，分析文章表达的设计意图、模块边界、调用链和适用条件；没有把文章中仍在开发期的包名和 API 当作稳定契约，也没有据此验证 AbilityKit 仓库后续版本的实际实现。

本文只分析文章中的架构思想和对 MechStorm 的参考价值，不代表建议直接接入 AbilityKit。

Buff 生命周期、实例身份、叠层刷新、Duration / Interval、来源快照、光环和恢复边界已拆为独立专题，见 [`ScalableBuffSystemArchitecture.md`](../BuffSystems/ScalableBuffSystemArchitecture.md)。本文继续负责 AbilityKit 整体职责、逻辑与表现、伤害、同步和回放边界，避免在两篇文档中重复维护完整 Buff 分析。

## A. 文章写了什么

### A.1 核心主题

这篇文章讨论的是大型战斗系统在复杂度上升后，应该如何把早期 `SkillManager`、`BuffManager`、`ProjectileManager`、`DamageService` 里混在一起的职责重新拆开。

文章的核心判断是：

```text
简单项目不一定需要大框架。
复杂项目真正需要拆的是时间、状态、触发、效果、伤害、表现、同步和回放边界。
```

也就是说，它不是在讲“怎么写一个更大的技能类”，而是在讲：

```text
技能系统为什么会膨胀？
SkillManager 为什么会变成怪物类？
哪些职责必须从技能系统里拆出去？
战斗逻辑如何离开 Unity 场景和表现对象运行？
线上问题如何通过 Trace / Record / Replay 复现？
```

### A.2 文章反复强调的前提

文章明确说，AbilityKit 更像一个“源码工具箱”，不是开箱即用的成品玩法框架。

它不替项目决定：

- 角色必须长什么样。
- 技能必须怎么配。
- Buff 必须怎么落地。
- 同步必须走哪条路线。
- 哪些规则进配置，哪些规则留代码。

项目仍然必须先定义自己的战斗语言，例如：

```text
什么是角色
什么是技能
什么是一次攻击
什么是持续效果
什么是表现事件
什么是配置节点
什么是运行时动作
```

这点对 MechStorm 很重要，因为 MechStorm 不能直接接受任何外部框架的默认范式，而要保留自己的 SRPG、部位破坏、AP、Tag、State、Result、Snapshot 路线。

## B. 设计意图和要解决的问题

### B.1 避免 SkillManager 膨胀

文章指出，很多项目早期的战斗逻辑会这样堆：

```text
SkillManager 管释放
BuffManager 管状态
ProjectileManager 管子弹
DamageService 管伤害
```

在技能少、单机、短周期时，这种方式并不差，甚至更快。

但项目复杂后，`SkillManager` 往往会开始知道太多东西：

```text
输入
冷却
目标
伤害
表现
Buff
网络同步
特殊角色逻辑
```

它每次都能继续塞需求，但每次都会让排查和扩展更困难。

文章的设计意图就是把这些职责拆回各自边界，让每个模块只解决一类问题。

### B.2 让战斗逻辑脱离表现层

文章强调同一套核心逻辑要能被多个宿主使用：

```text
Unity
Console Demo
纯 C# 测试
服务端
回放系统
```

这背后的目标是：

- 战斗逻辑不依赖 MonoBehaviour。
- 测试不依赖 Unity 场景。
- 服务器可以复用同一套规则。
- 线上问题可以通过记录和回放复现。
- 表现层只消费逻辑输出，不反向修改逻辑。

这与 MechStorm 的 `MechStorm.Battle` 无头核心原则高度一致。

### B.3 给复杂战斗机制找到归位点

文章列举的复杂度包括：

- 前摇、吟唱、蓄力、引导。
- 多段 Timeline。
- Buff Tick、刷新、叠层。
- 命中、受击、击杀触发。
- 投射物穿透、返回、命中后继续触发。
- 被动、装备、天赋、区域效果接入统一规则。
- 客户端预测、回滚、服务端权威。
- Trace、Replay、StateHash、自动化验收。

这些机制如果都塞在技能类里，会迅速失控。

文章希望通过能力分层，让它们分别归到：

```text
Pipeline / Timeline
Triggering
Effect Lifecycle
Damage
Targeting
Projectile
Area
Motion
Record / Replay
Snapshot / StateSync
```

## C. 架构模型

### C.1 能力分层

文章中的 AbilityKit 大致可以拆成以下层次：

| 层级 | 代表内容 | 解决问题 |
|---|---|---|
| Foundation | 事件、DI、Timer、Diagnostics、对象池 | 提供干净运行时底座 |
| World / Host | 逻辑世界、服务装配、会话生命周期 | 让战斗逻辑脱离 MonoBehaviour |
| SkillCore | Pipeline、Triggering、Attribute、Ability、Modifier | 处理技能流程、属性、触发、效果生命周期 |
| BattleRuntime | Targeting、Damage、Projectile、Area、Motion、EntityManager | 把目标、伤害、投射物、区域、运动拆成战斗原语 |
| SyncRuntime | Snapshot、Record、Replay、StateSync、输入帧 | 支撑同步、复现、回滚、重连 |
| ServerRuntime | BattleHost、Room、Gateway、Orleans | 服务端承载和运行面实验 |
| Demo | MOBA、Shooter、Console、ET | 展示不同宿主如何接入同一套规则 |

这个分层的核心价值不是“目录很多”，而是提醒项目不要把所有战斗问题都塞进技能系统。

### C.2 一次战斗行为的抽象链路

文章隐含的执行链路可以理解为：

```text
Input / AI / Command
-> World / Session
-> Pipeline / Timeline
-> Triggering / TriggerPlan
-> Effect Lifecycle
-> Targeting / Damage / Projectile / Area / Motion
-> Trace / Record
-> Snapshot / ViewEvent
-> Presentation / Server / Replay
```

其中关键边界是：

- 输入只发起意图，不直接改表现。
- Pipeline 管流程阶段，不承载所有业务规则。
- Triggering 管事件、条件和动作。
- Effect 管生命周期，不只是一次性数值修改。
- Damage 需要上下文和归因，不只是一个函数。
- Projectile / Area / Motion 是战斗原语，不应该成为技能私有逻辑。
- ViewEvent / Cue 是给表现层消费的输出，不应回写核心规则。
- Record / Replay 是调试和复现能力，不应等线上出问题后再补。

## D. 文章方案的优点

### D.1 边界意识强

文章最有价值的是边界意识。

它把常见的“技能系统”拆成多个问题：

```text
时间怎么推进
规则怎么触发
状态怎么维护
效果怎么生命周期化
伤害怎么归因
目标怎么选择
投射物怎么运动
表现怎么同步
问题怎么复现
服务器怎么复用
```

这比只讨论 `Skill`、`Buff`、`Damage` 三个类更接近长期项目的真实复杂度。

### D.2 适合长期复杂机制增长

当项目有大量被动、装备、天赋、状态、区域效果、追加打击、反击、追击时，文章的 Triggering / Effect Lifecycle 思路很有价值。

它能避免这些机制各自写一套监听和执行逻辑。

### D.3 重视 Trace / Record / Replay

文章把 Trace 和 Replay 放到架构层讨论，而不是当作后期工具。

这点非常值得 MechStorm 借鉴，因为复杂战斗最大的问题往往不是“算不出来”，而是：

```text
为什么这次伤害是这个数？
为什么这个 Trigger 触发了？
为什么这个 Buff 没刷新？
为什么某个目标不合法？
为什么线上和本地结果不一致？
```

没有日志、快照、回放和状态哈希，后期只能靠猜。

### D.4 与纯 C# 战斗核心兼容

文章强调同一套规则可以运行在 Unity、Console、Server、Demo 中。

这与 MechStorm 现有方向一致：

```text
MechStorm.Battle
-> 纯 C# 核心
-> 不依赖 UnityEngine
-> 可测试
-> 可快照
-> 可回放
-> 未来可服务端复用
```

## E. 局限和风险

### E.1 当前阶段照搬会过度设计

MechStorm 当前阶段仍是 P1 / Sprint 2，主要目标是：

```text
BattleSession
CombatUnitRegistry
TurnCoordinator
移动
普通攻击
扣血
死亡
回合推进
BattleActionResult
BattleSnapshot / BattleActionLog 雏形
```

如果现在直接引入 Pipeline、TriggerPlan、ActionRegistry、SyncRuntime、ServerRuntime，会严重拖慢最小战斗闭环。

文章本身也承认：少量技能、单机、短周期项目不适合上重框架。

### E.2 概念成本高

文章里的概念很多：

```text
World
Host
Pipeline
Timeline
TriggerPlan
RulePlan
EffectSpec
ActionRegistry
FunctionRegistry
Snapshot
StateSync
Replay
ServerRuntime
```

如果团队没有统一术语、配置治理和测试约束，这些概念可能变成新的复杂度来源。

### E.3 配置化不是万能答案

文章提醒项目必须先决定：

```text
哪些规则可以配置组合？
哪些动作必须写代码扩展？
哪些只是底层积木？
哪些是项目自己的业务语言？
```

如果过早把复杂规则都塞进配置，可能得到一个难调试的 DSL 黑盒。

### E.4 Server / Sync 不应提前压入当前主线

AbilityKit 的 SyncRuntime 和 ServerRuntime 对长期很有启发，但 MechStorm 当前不应该提前做：

- 客户端预测。
- 回滚。
- 权威服。
- Orleans。
- 状态同步协议。

目前只需要保留接口边界和快照意识，避免后续完全重写。

## F. 对 MechStorm 的借鉴参考意义

### F.1 当前 Sprint 2 只借鉴边界，不借复杂度

当前阶段最应该吸收的是文章的“边界拆分”思想：

```text
BattleSession 是唯一战斗逻辑入口。
TempGameEntry 不继续承载战斗规则。
表现层只消费 BattleActionResult。
战斗核心不依赖 Unity。
每次行动输出可读结果和失败原因。
Task 2.7 尽早做轻量 Snapshot / ActionLog。
```

当前不应引入：

```text
完整 AbilityKit
Pipeline / Timeline
TriggerPlan
ActionRegistry
FunctionRegistry
SyncRuntime
ServerRuntime
完整 Replay
```

### F.2 Sprint 3 / P2 可逐步吸收战斗原语

等普通移动、普通攻击、死亡和回合推进稳定后，可以逐步吸收：

```text
AbilityDefinition
TargetRule
BattleEffect
DamageContext
AbilityResult
GameplayTag
StateRuntime
AttributeModifier
TriggerRule
Timing Anchor
```

这些可以对应文章里的：

```text
Pipeline
Triggering
Effect Lifecycle
Damage
Targeting
Attribute / Modifier
```

但 MechStorm 应做轻量 SRPG 版本，不需要完整实时 Timeline 或动作游戏式 Pipeline。

### F.3 Task 2.7 可以作为 Trace / Record 的最小种子

文章里的 Trace / Record / Replay 对 MechStorm 很重要，但不能一步到位。

建议当前只做：

```text
BattleSnapshot
BattleActionLog
BattleActionResult
FailureReason
UnitMoved
DamageApplied
UnitDied
TurnChanged
```

后续再扩展：

```text
DamageBreakdown
TargetValidationReport
StateChangeLog
TriggerExecutionLog
ReactionLog
Command Replay
Result Replay
StateHash
```

### F.4 P4 / P5 编辑器路线可以参考文章的项目语言思想

文章强调接入前要先定义项目语言。

这与 MechStorm 的编辑器规划一致：

```text
P4：编辑器前置基建
  Schema
  Validator
  TargetValidationReport
  DamageBreakdown
  TriggerExecutionLog
  标准复杂样例

P5：战斗内容编辑器 MVP
  Ability 编辑
  State 编辑
  Trigger / Condition 编辑
  目标范围预览
  战斗沙盒模拟

P6：内容生产工业化
  回归 Runner
  平衡模拟
  配置 Diff
  内容 Review
```

这篇文章可以作为这些阶段的理论支撑：先定义 Runtime 和 Schema，再做编辑器，而不是先做一个漂亮 UI。

### F.5 对 MechStorm 的定位总结

这篇文章对 MechStorm 的定位是：

```text
长期战斗框架拆分参考
长期调试 / 回放 / 服务器复用参考
长期编辑器前置基建参考
不是当前 Sprint 2 实现模板
不是要直接接入的完整框架
```

一句话总结：

> 这篇文章证明了 MechStorm 现在坚持的路线是正确的：先做纯 C# 战斗核心、Result、Snapshot、Log，再逐步引入 Ability、Effect、State、Trigger、Damage Pipeline 和编辑器，而不是一开始就上完整大框架。

## G. 按文章章节进一步解析

### G.1 “最后到底在拆什么”：拆的是变化原因，不是类数量

文章开头并没有否定 `SkillManager / BuffManager / ProjectileManager / DamageService`。相反，它先承认这种直接结构在技能少、单机、短周期项目里通常更舒服。

真正触发拆分的不是“类不够优雅”，而是多个变化原因开始同时进入同一个管理器：

| 变化来源 | 典型需求 | 如果不拆会污染哪里 |
|---|---|---|
| 时间 | 前摇、蓄力、引导、多段、等待、取消 | `Skill` 内部状态机 |
| 状态 | Buff Tick、刷新、叠层、驱散、免疫 | `BuffManager` 特殊分支 |
| 规则 | 命中、受击、击杀、装备、天赋、被动 | 各系统独立监听器 |
| 空间 | 目标搜索、投射物、区域、碰撞、位移 | 每个技能私有几何逻辑 |
| 结算 | 暴击、防御、护盾、归因、死亡 | 巨型 `DamageService` |
| 表现 | 动画、特效、飘字、插值、恢复 | 逻辑依赖 Prefab |
| 运行面 | 服务端、预测、回滚、重连 | UI 或网络层补丁 |
| 诊断 | Trace、Record、Replay、StateHash | 零散日志和人工复现 |

因此文章实际使用的是“按变化原因分离”的思路。模块多不是目的，目的是让时间编排、规则判断、持续状态、空间查询和结算公式可以分别演进。

这也解释了为什么不能只把 `SkillManager` 拆成更多小 Manager。如果拆出来的对象仍然彼此随意调用、共同依赖表现对象、没有统一上下文和结果记录，那么只是把一个大泥团改成多个小泥团。

### G.2 “源码地图”：包边界同时表达依赖方向

文章把仓库分成 Foundation、World / Host、SkillCore、BattleRuntime、SyncRuntime、ServerRuntime 和 Demo。更深一层看，这个分层包含两条方向：

```text
玩法执行方向：
Command -> Pipeline -> Triggering -> Effect / Combat -> Result

运行环境方向：
Foundation -> World / Host -> Unity / Console / ET / Server
```

第一条决定“一次行为怎么发生”，第二条决定“同一套规则在哪里运行”。这两个方向不能混在一个层次里：

- `Pipeline` 不应该知道 Orleans。
- `Damage` 不应该知道 Unity Prefab。
- `ServerRuntime` 不应该重新实现一套伤害规则。
- Demo 不应该成为业务架构的唯一标准答案。

文章还提到轻量 ECS、Entitas 和 Svelto 并存。它表达的不是同时维护三套业务，而是将 ECS 当作状态存储、批量查询和 Tick 调度后端。技能怎么造成伤害、效果怎么刷新，应该尽量留在 Service 或规则节点中，避免更换 ECS 后端时重写业务。

对 MechStorm 而言，这个思想可转换为：

```text
CombatUnit / CombatUnitRegistry：状态和单位索引
BattleSession / 后续 TurnCoordinator：流程协调
MovementResolver / AttackResolver：明确规则服务
BattleActionResult：逻辑输出契约
Presentation Adapter：Unity 宿主适配
```

当前并不需要 ECS，但应避免把战斗语义绑定到某个 Unity 组件或未来的数据存储方式。

### G.3 “一次战斗行为”：文章真正强调的是主时间轴

文章给出的行为链并不是简单的调用栈，而是一条可被 Tick、Round 或服务端帧驱动的主时间轴：

```text
输入或 AI 产生意图
-> Session / World 接受并调度
-> Pipeline 决定阶段和时间点
-> Triggering 根据事件执行条件与动作
-> Effect / Combat 修改逻辑状态
-> 延迟移除与死亡清理
-> Trace / Record 记录证据
-> Snapshot / ViewEvent 输出给表现或同步
```

文章特别指出，战斗循环不应退化成一个巨型 `Update`。一个稳定推进循环至少要明确：

1. Tick 或 Round 开始时推进哪些计时和阶段事件。
2. 持续效果、区域和投射物按什么顺序结算。
3. 输入、AI 或服务端命令何时进入。
4. 行为执行期间是否允许插入反应规则。
5. 死亡、过期对象和延迟移除何时统一清理。
6. 哪个时间点生成 Trace、Snapshot 和 StateHash。

文章讨论的是实时和回合制通用问题。MechStorm 不需要照搬实时 Tick，但仍要定义稳定的回合时间轴，例如：

```text
RoundStarted
-> FactionTurnStarted
-> UnitActionStarted
-> CommandValidated
-> CommandResolved
-> ReactionsResolved
-> UnitActionEnded
-> FactionTurnEnded
-> RoundEnded
```

当前 Sprint 2 只需要显式移动、攻击和结束行动。反应、状态 Tick 和技能阶段应该等对应任务出现后，再插入已经命名好的锚点。

### G.4 “角色先是逻辑容器”：分离事实、投影和手感

文章对逻辑与表现的判断标准很清楚：

> 会影响结算、同步、回放和测试的内容属于逻辑事实；只影响玩家看到、听到和手感补偿的内容属于表现投影。

可以进一步拆成三层：

| 层次 | 示例 | 是否可改变权威结果 |
|---|---|---|
| 逻辑事实 | 阵营、位置、HP、标签、命中、伤害、死亡 | 是，必须由核心结算 |
| 表现投影 | GameObject、血条、挂点、特效、飘字 | 否，只消费逻辑输出 |
| 手感补偿 | 插值、镜头震动、拖尾、音效、预测平滑 | 否，不应成为命中事实来源 |

文章提出 `TriggerEvent` 与 `Snapshot` 两类输出：

- 即时事件适合播放命中、伤害、Cue 等一次性表现。
- 快照适合状态恢复、重连、旁观、批量刷新和纠偏。
- 多人客户端可使用 Hybrid 模式，快照保证正确，事件补充手感。

这与 MechStorm 已确定的阵营模型还能组合：

- 权威逻辑记录 `TeamA / TeamB / Neutral`。
- 行动结果记录 `AttackerUnitId / DefenderUnitId`。
- 客户端根据观察者阵营映射 `Ally / Enemy / Neutral`。
- 表现层不能把本地 `Enemy` 重新写回权威状态或回放日志。

### G.5 “技能只是发射器”：把时间编排和业务动作分开

文章的“技能只是发射器”不是说技能对象不重要，而是限制它的职责：

```text
技能入口负责：
- 选择使用哪个技能定义和等级
- 检查基础释放条件
- 提交资源或创建流程
- 注入本次释放参数

技能入口不负责：
- 独占伤害公式
- 独占目标查询
- 独占投射物飞行
- 独占 Buff 生命周期
- 直接操作表现对象
```

文章中的 Pipeline 只回答“什么时候继续”，TriggerPlan / Action 回答“时间点到了做什么”。Timeline 也不是 Unity Timeline 资产，而是逻辑事件脚本，包含持续时间、时间点、动态参数、等待、取消和时间缩放。

这种拆分的收益在技能强化场景最明显：

- 技能等级修改倍率、段数或持续时间。
- 装备和天赋修改 Timeline 节点、TriggerPlan 或 Projectile 参数。
- 被动监听释放前后事件。
- AI 和机关可以直接创建逻辑 Timeline，不必伪装成玩家按键。

MechStorm 是回合制 SRPG，技能通常不需要每帧 Timeline。更合适的轻量映射是：

```text
AbilityDefinition：静态技能定义
AbilityCommand：本次使用意图
AbilityContext：来源、目标、参数和流程状态
HitSegment：多段结算顺序
TimingAnchor：BeforeCast / BeforeHit / AfterHit / AfterResolve
EffectList：每个结算点应用的效果
```

只有当蓄力、延迟爆炸、持续引导或复杂多段表现确实出现时，才需要把 `HitSegment / TimingAnchor` 演进为更通用的 Timeline。

### G.6 “装备、天赋、被动”：统一的是执行结构，不是业务含义

文章观察到主动技能、装备、天赋、被动、Buff、投射物和区域虽然来源不同，但都可以表达为：

```text
Event
-> Context
-> Conditions
-> Ordered Actions
-> Execution Control
-> Trace
```

`TriggerRunner` 负责订阅事件、排序、创建执行上下文、判断条件、执行动作、处理中断并记录 Trace。文章提到的 `ExecCtx` 汇集 EventBus、FunctionRegistry、ActionRegistry、Blackboard、PayloadAccessor 和 NumericDomains，说明它试图把规则执行所需依赖收口，而不是让节点任意访问全局对象。

这里必须保留文章强调的边界：

- Triggering 只管理事件、条件、动作和执行顺序。
- Damage 决定伤害如何结算。
- Effect 决定状态如何持续和移除。
- Projectile 决定飞行、命中和生命周期。
- Trigger 不能变成新的万能脚本容器。

MechStorm 长期有追击、反击、协力、元件、天赋、地形和状态反应，确实需要统一 Trigger 结构。但项目路线图已经明确：核心移动、攻击、回合推进保持显式流程，Trigger 只是扩展点。这比把所有行为都解释成 Trigger 更适合确定性的 SRPG。

### G.7 “效果系统”：核心不是 Buff 名称，而是生命周期契约

文章把持续行为拆成五个问题：

1. 什么时候开始。
2. 谁拥有它。
3. 多久检查一次。
4. 监听哪些事件。
5. 以什么原因结束。

这五个问题分别映射到 Effect 的所有者、Duration、Period、Trigger 订阅和 RemoveReason。文章还强调叠层、刷新、互斥、优先级和移除原因必须提前形成策略，否则特殊分支会重新堆回 `BuffManager`。

| 策略 | 必须明确的问题 |
|---|---|
| Stack | 层数上限、每层独立计时还是共享计时 |
| Refresh | 刷新时长、刷新数值、增加层数或拒绝 |
| Priority | 多个控制和 Modifier 的结算顺序 |
| Exclusivity | 同类效果替换、互斥或共存 |
| RemoveReason | 过期、驱散、打破、死亡、离开区域 |

文章进一步指出，Buff、光环、护盾、印记、领域只是业务名称。底层通常是 Effect 生命周期、Tag、Modifier、Scheduler、Condition 和 Action 的不同组合。这个观点有助于避免为每个玩法名词重新发明一套系统。

但 MechStorm 不应因此取消领域术语。配置、编辑器和策划沟通仍可保留 `State / Effect / Aura / Shield` 等清楚名称，只需让底层生命周期和日志结构一致。

### G.8 “伤害公式”：需要请求、快照、管线、结果和归因

文章认为伤害不是一个孤立函数，因为一次攻击需要回答：

- 谁发起，命中谁。
- 属于哪种攻击和元素。
- 是否直伤、暴击、吸血、反伤、绕过护盾。
- 使用哪一刻的属性值。
- 哪些增减伤节点参与。
- 最终如何修改护盾和 HP。
- 是否导致死亡，击杀归谁。

因此它把 Damage 拆成 Dataflow processor 链：

```text
DamageRequest
-> Validate
-> Attribute Snapshot
-> Base Formula
-> Modifiers
-> Shield / HP Apply
-> Death / Kill Attribution
-> DamageResult + Trace
```

这里“快照”有两个不同含义，不能混淆：

- `Attribute Snapshot` 是一次结算内部的稳定数值视图，避免结算中途反复读取可变状态。
- `BattleSnapshot` 是整个战斗或关键实体的状态导出，用于恢复、诊断或同步。

MechStorm 当前固定伤害不需要完整管线，但未来部位破坏、命中、护盾、暴击、攻击方式 Tag、追击和元件都会要求 `AttackContext / DamageContext / DamageResult / DamageBreakdown`。文章支持路线图中“先 Context 和 Result，后 Modifier 和 Trigger”的顺序。

### G.9 “投射物、区域和运动”：按问题域拆，不按视觉名称拆

文章给出的核心区分是：

```text
Projectile：我发出去的东西沿什么轨迹移动，命中了谁。
Area：这个范围当前包含谁，谁进入、停留或离开。
Motion：这一时刻对象应该如何移动。
Collision：一次几何查询命中了什么。
Targeting：从候选集合中如何过滤、评分和选择。
```

即使投射物和区域都能移动、存在一段时间并造成伤害，它们维护的关键状态仍不同：

- Projectile 维护速度、持续时间、命中记录、穿透和返回状态。
- Area 维护形状、捕获集合、Enter / Tick / Leave。
- Motion 处理位移求解。
- Collision 只提供引擎无关查询结果。

MechStorm 当前没有连续物理弹道，不应预建实时 ProjectileWorld。战棋中的子弹大多可以先表示为：

```text
TargetValidation
-> 逻辑命中与伤害结算
-> BattleActionResult 记录弹道表现提示
-> Presentation 播放弹道动画
```

只有弹道本身会影响规则，例如可被拦截、沿途命中、占用格子或延迟到达时，才需要把 Projectile 提升为逻辑实体。

### G.10 “配置表不是万能”：配置的是组合，不是任意代码

文章没有主张把所有规则搬到 JSON。它建议：

- 原子逻辑节点由代码实现和测试。
- 配置负责选择节点、连接顺序和提供参数。
- 运行时负责 Schema 校验、依赖校验和证据记录。
- 复杂算法、空间查询和平台适配继续留在代码。

这可以形成三个边界：

| 层次 | 负责内容 | 典型错误 |
|---|---|---|
| Schema | 配置能表达哪些字段和引用 | 字段含义不清、单位不统一 |
| Node / Action | 原子行为和条件的代码实现 | 一个 Action 吞掉完整技能 |
| Composition | 用配置组合流程、条件和动作 | 把配置做成不可调试脚本语言 |

文章还提醒百分比单位、枚举或字符串、策划名到运行时 ID 的映射必须尽早统一。MechStorm 已决定核心数值整数化和万分比表达，这正是配置边界的一部分。

### G.11 “Trace 和 Replay”：解释因果与复现状态是两类问题

文章将 Trace、Record、Replay 和 StateHash 放在一起讨论，但它们解决的问题不同：

| 能力 | 主要问题 | 最小数据 |
|---|---|---|
| Trace | 为什么发生 | root、parent、source、target、config |
| Record | 发生过什么 | 按时间或帧记录事件和 payload |
| Replay | 能否重现 | 初始状态、输入或权威结果 |
| StateHash | 何时分叉 | 指定帧或行动后的状态摘要 |
| Minimized Replay | 最小触发条件是什么 | 缩减后的输入序列 |

文章特别反对直接保存“运行时对象现场”。更稳定的是保存可验证契约，例如 frame、playerId、opCode、payload、snapshot 和 state hash，再由 codec 和 handler 还原。

这对 MechStorm 的直接启发是：

- `BattleActionLog` 记录客观事实和稳定 ID，不保存 Unity 对象。
- `BattleSnapshot` 只包含可序列化逻辑状态。
- 轻量日志先服务失败测试和调试，不宣称已经支持正式 Replay。
- 正式分享回放采用 Command + 服务端权威 Result 混合日志。
- 本地 `Ally / Enemy` 关系不能进入权威 BattleLog。

### G.12 “同步不是后补层”：真正前置的是边界，不是网络代码

文章强调同步必须提前考虑，容易被误解为“项目一开始就实现网络”。它更合理的含义是：

- 逻辑状态必须能脱离表现对象存在。
- 输入必须通过明确 Command 或 Intent 进入。
- 结算结果必须可序列化。
- 时间推进必须有稳定编号。
- 状态恢复不能依赖 Unity 场景残留。
- 客户端表现不能成为权威事实来源。

这些边界应前置，具体的 FrameSync、StateSync、预测、回滚、重连和 Orleans 可以后置。

文章区分入场流程和持续同步：

```text
入场：账号 / 房间 / 准备 / 开战 / battleId / worldId / start anchor
持续同步：输入提交 / 接受帧 / 权威快照 / 状态哈希 / 表现消费
```

这能防止 Room、Battle 和 Gateway 职责互相侵入。MechStorm 已确定回合制 PVP 采用服务端权威，不采用帧同步，因此只借鉴输入、权威结果、快照和恢复边界，不采用文章 Shooter 示例的完整预测回滚路线。

### G.13 “Server / Orleans”：验证可承载性，不等于生产方案

文章把服务端模块定位为运行面实验：

- Gateway 负责入口、鉴权和协议路由。
- Room 负责成员、准备、选人、开战和重连。
- Battle 负责输入缓冲、权威 Tick、逻辑世界和快照。
- Smoke Runner 负责端到端验收和回放产物。

它没有覆盖账号、支付、数据库高可用、全球加速和完整生产运维。因此不能因为仓库含 Orleans，就推导出 Orleans 是 MechStorm 的既定服务器选型。

真正值得保留的结论只有一个：如果纯 C# 战斗核心能在 Console 和服务端 Smoke 中运行，并输出可验证 artifact，说明逻辑与宿主边界基本成立。

## H. 文章最有价值的架构原则

### H.1 先定义项目语言，再选框架积木

文章最核心的原则不是 Pipeline 或 Trigger，而是：

```text
项目先定义什么叫角色、技能、攻击、持续效果和表现事件，
再决定使用哪些底层能力承载这些语义。
```

这能避免 Demo 反向塑造业务。MechStorm 的项目语言已经比通用框架更具体：

- 单位由 Pilot、Mech、部位、武器和局内 Runtime 组成。
- 行动消耗 AP，移动暂不消耗 AP。
- 攻击区分主动/非主动与对战/非对战。
- 稳定阵营使用 TeamA / TeamB / Neutral。
- 状态、属性、Modifier 和 Trigger 按阶段引入。
- PVP 使用服务端权威结果。

任何 AbilityKit 概念进入项目，都必须先映射到这些既有语义，而不是反过来修改项目语言。

### H.2 复杂度应按需购买

文章隐含一条成熟度阶梯：

```text
直接管理器
-> Pipeline / Triggering / Effect
-> Targeting / Damage / Projectile / Area / Motion
-> Trace / Record / Replay
-> Snapshot / StateSync
-> Server Runtime
```

每上升一级都增加适配、测试和配置治理成本。正确做法不是一次接全，而是在当前复杂度已经让旧边界失效时，再引入下一层。

### H.3 调试证据属于架构产物

文章中的 Trace、summary、replay 和 minimized replay 不是附属日志，而是模块验收产物。一个复杂战斗模块如果只返回“成功或失败”，却不能说明目标过滤、伤害修正、状态刷新和触发顺序，就很难长期维护。

MechStorm 路线图中的 `TargetValidationReport`、`DamageBreakdown`、`StateChangeLog`、`TriggerExecutionLog` 和 `ReactionLog` 与这条原则一致，但应该在对应 Runtime 和 Schema 稳定后逐步增加。

## I. 对文章本身的审慎评价

### I.1 优点

1. **明确承认简单项目不需要重框架**，避免把架构复杂度包装成普遍正确。
2. **从完整战斗运行时而非技能类出发**，覆盖时间、状态、空间、结算、表现、同步和诊断。
3. **强调同一核心跨 Unity、纯 C#、Console 和服务端运行**，边界目标清晰。
4. **把 Trace、Replay 和 Smoke 纳入设计**，不只讨论运行时功能。
5. **区分源码阅读顺序和项目接入顺序**，提醒读者不要按目录整仓照搬。
6. **承认 AbilityKit 仍在开发期**，没有把当前 API 描述成稳定产品契约。

### I.2 文章没有充分回答的问题

1. **模块数量与维护成本的量化标准不清楚**。文章描述了何时复杂，但没有给出技能数量、规则组合数、团队规模或缺陷成本等触发阈值。
2. **确定性细节不足**。虽然讨论 StateHash、Replay 和同步，但没有在文章层面完整说明随机数、浮点、排序稳定性、容器遍历和版本兼容策略。
3. **配置版本迁移没有展开**。Schema、ActionSchema 和代码生成被提及，但旧配置升级、废弃字段和跨版本回放兼容仍是关键风险。
4. **Trigger 依赖图风险没有展开**。循环触发、嵌套深度、重入、事件风暴、优先级冲突和事务边界需要额外约束。
5. **错误恢复语义不足**。Pipeline 中途失败、Action 部分执行、服务异常或配置缺失时，是回滚、拒绝还是产生失败 Result，需要项目自行定义。
6. **性能结论缺少基准数据**。生成执行根、解释执行、多个 Registry 和 Trace 都有成本，文章没有提供压测结果。
7. **安全边界不是重点**。服务端权威还需要输入校验、速率限制、协议版本、作弊检测和资源上限，这些不在文章范围内。
8. **仓库实现仍在变化**。文章明确说明包边界、API、asmdef 和示例依赖会继续收敛，因此只能借鉴方向，不能把当前名称当稳定标准。

### I.3 最容易被误读的三点

**误读一：同步要前置，所以现在就做服务器。**

正确理解是序列化状态、Command 入口、逻辑与表现分离等边界要前置，网络实现按项目阶段后置。

**误读二：Buff、AOE、子弹都只是组合，所以不需要领域对象。**

正确理解是底层生命周期和规则语言可以复用，但业务层仍需要清楚术语、Schema、调试视图和编辑器入口。

**误读三：配置组合能力越强越好。**

正确理解是配置只组合经过测试的原子节点。若配置语言开始承载循环、任意状态访问和复杂控制流，它会成为更难维护的脚本系统。

## J. 对 MechStorm 的阶段映射

| AbilityKit 思想 | MechStorm 当前承载点 | 引入阶段 | 当前动作 |
|---|---|---|---|
| World / Session | `BattleSession` | P1 / Sprint 2 | 保持唯一战斗入口，不扩成万能对象 |
| Entity Registry | `CombatUnitRegistry` | Task 2.2 | 只管理单位归属和查询 |
| Stable Timeline | `TurnStateMachine`，后续 `TurnCoordinator` | Task 2.3 | 先完成单位行动和阵营切换 |
| Combat Service | `MovementResolver`、`AttackResolver` | P1 | 维持显式规则服务 |
| Result / View boundary | Task 2.6 结果通知 | Sprint 2 后段 | 输出移动、伤害、死亡、回合变化 |
| Snapshot / Record seed | `BattleSnapshot`、`BattleActionLog` | Task 2.7 | 只做调试导出，不做正式 Replay |
| Ability / Targeting | 最小技能和目标规则 | Sprint 3 | 普通攻击稳定后引入 |
| Attribute / Context | `BattleAttributeSet`、`AttackContext`、`DamageContext` | P2 | 规则增长后收口 |
| Effect Lifecycle | `BattleState / Effect` | P2 | Buff 需求出现后定义生命周期策略 |
| Modifier / Trigger | 轻量 GAS 扩展点 | P3 | 不替代核心移动、攻击和回合流程 |
| Projectile / Area / Motion | 仅机制需要时引入 | P3/P4 | 表现弹道默认不升级为逻辑实体 |
| Trace / Breakdown | 调试日志和报告 | P4 | Schema 与 Runtime 稳定后建设 |
| Command / Result Replay | 本地复盘与权威分享回放 | P2/P3 后段 | 遵循项目既定混合日志方案 |
| StateSync / Server | 服务端权威 PVP | 长期 | 不采用帧同步和 Shooter 全套路线 |

### J.1 Sprint 2 的具体约束

当前只应该落实以下内容：

```text
BattleSession 负责协调，不持有所有深层规则。
CombatUnitRegistry 管理阵营单位，不负责回合推进。
TurnCoordinator 后续接管单位行动和阵营切换。
移动、攻击、死亡走显式方法和结果对象。
表现层不直接修改 CombatUnit。
Task 2.7 导出稳定 ID、客观阵营、位置、HP、死亡和行动记录。
```

当前明确不做：

```text
通用 Pipeline / Timeline
TriggerPlan / ActionRegistry
完整 Effect 生命周期
实时 ProjectileWorld
预测、回滚、帧同步
Orleans 服务端
正式录像格式和跨版本回放
```

### J.2 Task 2.7 的最小日志契约建议

文章对 Trace 和 Replay 的分析可以约束 Task 2.7 的字段，但不扩大其任务范围。

`BattleSnapshot` 至少应包含：

```text
BattleId 或本地会话标识
Round / TurnPhase
CurrentFaction
CurrentUnitId
Board 基础信息
UnitId
CombatFaction
GridPosition
CurrentHp
IsDead
ActionState
```

`BattleActionLog` 至少应包含：

```text
Sequence
ActionType
ActorUnitId
ActorFaction
TargetUnitId（可选）
TargetFaction（可选）
Before / After 的关键客观结果
FailureReason（失败时）
```

此阶段不需要加入：

```text
任意对象引用
Unity GameObject 或本地 Ally / Enemy
完整 Trigger 父子树
二进制网络协议
跨版本 Schema 迁移
正式 Command Replay 播放器
```

## K. 最终结论

更详细阅读后，这篇文章最值得 MechStorm 采用的不是 AbilityKit 的包结构，而是以下五条判断：

1. **按变化原因拆职责**，不要按技能、Buff、子弹这些表面名词堆管理器。
2. **显式区分流程、规则、生命周期、空间原语和结算**，避免任何一层重新变成万能入口。
3. **逻辑事实、表现投影和手感补偿分层**，权威状态不能由动画和 Prefab 决定。
4. **Trace、Result、Snapshot 和 Replay 分别解决解释、输出、状态导出和复现问题**，不能混成一个“日志系统”。
5. **复杂度按阶段购买**，先完成项目自己的最小战斗语言，再逐步引入 Effect、Modifier、Trigger、Pipeline 和同步能力。

对当前 MechStorm 的最直接结论仍然是：

> 保持 `BattleSession + CombatUnitRegistry + Resolver + Result` 的轻量显式结构，完成 Sprint 2 最小闭环；借文章约束未来边界，但不要把 AbilityKit 的复杂运行时提前搬进当前任务。
