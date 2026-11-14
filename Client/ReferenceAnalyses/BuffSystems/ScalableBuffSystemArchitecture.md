# 可扩展 Buff / 持续状态系统架构分析

## 1. 分析对象与结论

分析文章：

- 标题：《游戏开发-战斗系统设计：Buff 系统，为什么会越写越难改？》
- 作者：梁祝好
- 来源：知乎专栏“通用战斗系统设计”
- 发布时间：2026-07-13
- 原文地址：<https://zhuanlan.zhihu.com/p/2060062198844612800>
- 关联开源项目：<https://github.com/HOBOBO/AbilityKit>
- 本地来源：用户提供的知乎完整 HTML 存档

本文只分析文章表达的架构思想和对 MechStorm 的参考价值，没有进一步审计文章关联仓库的当前源码，因此“文章描述的实现”与“仓库当前真实实现”必须视为两个证据层级。

### 1.1 一句话结论

文章最有价值的观点不是给出一套标准 Buff 框架，而是指出：

> Buff 是项目业务语言，不是底层万能对象。持续状态真正需要稳定的是申请门禁、实例身份、叠层与刷新、生命周期调度、来源归因、阶段规则出口、结果通知、清理顺序和恢复边界。

从资深战斗程序和主程视角看，这个方向是正确的，并且与 MechStorm 已规划的轻量 GAS 变种高度一致。但文章展示的 MOBA 装配方案包含明显的实时游戏假设，不能直接迁移到回合制 SRPG。MechStorm 当前只应吸收职责边界、结构化结果、来源追踪和可观察性原则，Buff、Modifier、Trigger、Continuous 与恢复系统仍应按项目阶段后置。

### 1.2 结论来源标记

下文使用三种标记：

- **【文章原意】**：文章明确表达的观点。
- **【主程复核】**：从生产项目、长期维护和团队协作角度推导出的判断。
- **【MechStorm 决策】**：结合项目当前规划给出的采用、暂缓或拒绝结论。

---

## 2. 文章试图解决的根本问题

### 2.1 表面问题：Buff 类越来越大

项目早期通常从一个简单对象开始：

```text
Buff
  Duration
  Stack
  Tick()
  OnApply()
  OnRemove()
```

随着需求增加，这个对象不断吸收：

- 加速和减速。
- 中毒和灼烧。
- 护盾与破盾。
- 眩晕、沉默和免疫。
- 层数、刷新、延长和替换。
- 属性修改。
- 事件监听。
- 表现图标、特效和飘字。
- 光环范围。
- 网络同步和回放。

最后“Buff”不再是一种明确职责，而变成所有持续战斗逻辑的默认容器。

### 2.2 真正问题：变化原因没有拆开

【文章原意】Buff 难改并不是因为 Buff 一定要轻或一定要重，而是多个独立变化原因被塞进同一对象：

```text
状态如何申请
状态是否允许申请
重复申请命中哪个实例
层数如何变化
持续时间如何变化
周期何时触发
这一跳执行什么规则
状态如何结束
结束时清理什么
表现看到什么
日志和恢复记录什么
```

这些规则变化的频率和原因不同。把它们放在一个类中，会导致修改叠层策略时影响 Tick，修改表现时影响伤害，修改网络恢复时影响本地生命周期。

### 2.3 架构目标

文章想建立的不是“大而全 Buff 系统”，而是以下能力边界：

```text
稳定入口
明确身份
可组合生命周期
来源可追踪
顺序可解释
失败可观察
数据可恢复
模块可按需装配
```

【主程复核】这类设计的最终目标不是减少类数量，而是让每种需求只在一个清楚的位置发生变化。

---

## 3. 文章结构和核心论点

### 3.1 Buff 不是底层答案

【文章原意】Buff 是策划、程序和表现都容易理解的业务名称，但不应该天然成为战斗系统底层的中心对象。

以下对象都可能“持续存在”，但不一定应该统一为 Buff：

- 中毒。
- 护盾。
- 光环。
- 引导施法。
- 持续移动。
- 技能 Pipeline。
- 周期触发器。
- 区域临时规则。

它们可以共享某些生命周期能力，但所有权、时序、恢复和结束条件并不相同。

### 3.2 一条状态规则需要回答什么

文章要求状态设计至少回答：

1. 从哪里申请。
2. 申请是否允许。
3. 如何匹配已有实例。
4. 重复申请如何处理。
5. 运行时保存什么。
6. 持续期间由谁推进。
7. 周期抵达时执行什么。
8. 什么条件会结束。
9. 结束时撤销什么。
10. 来源如何保留。
11. 表现如何获得通知。
12. 调试、快照和恢复如何验证。

【主程复核】如果这些问题没有明确答案，复杂度不会消失，只会藏进条件分支、事件监听和配置特殊字段中。

### 3.3 最小生命周期主线

文章提出的最小主线可整理为：

```text
Apply Request
-> Admission / Gate
-> Runtime Identity Match
-> Stack / Refresh / Replace Policy
-> Runtime Initialization
-> Continuous Activation
-> Lifecycle Projection
-> Stage Rule Execution
-> Result / Cue / Trace
-> Remove / Cleanup
```

关键约束：

- 非法状态不能先进入活动列表再补校验。
- 重复申请必须产生明确结果。
- 替换旧实例必须走完整结束流程。
- 新实例初始化失败必须回滚。
- Remove 必须有明确结束原因。
- 表现只能消费结果，不能决定状态是否生效。

### 3.4 中毒案例的真实复杂度

文章使用“三层中毒”说明一句策划需求包含多个维度：

```text
持续 5 秒
每 1 秒造成一次伤害
最多 3 层
重复申请增加层数
重复申请刷新时长
可驱散
开始、变化和结束通知表现
保留施法来源
```

它实际需要：

- 门禁规则。
- 实例匹配规则。
- 叠层规则。
- 时长规则。
- 周期规则。
- 伤害规则。
- 来源归因。
- 驱散规则。
- 结束清理。
- 表现通知。

文章的核心提醒是：不要用“Buff 自己 Tick，结束时回调”掩盖这些真实决策。

### 3.5 配置、运行时和规则出口分离

文章建议分为：

| 层次 | 负责内容 | 不负责内容 |
|---|---|---|
| Definition / 配置 | 默认时长、周期、策略标识、Tag、Modifier 声明 | 当前层数、剩余时间、对象引用 |
| Runtime / 运行时 | 当前层数、剩余时间、来源、下一周期、上下文 | 通用玩法脚本和表现 |
| Rule / 规则出口 | Add、Interval、Remove 时执行的玩法行为 | 持有活动状态列表 |
| Result / 输出 | 已经确认发生的事实、拒绝原因和表现请求 | 反向修改权威状态 |

【主程复核】这与 MechStorm 已采用的 `Data + Runtime`、后续 `Context + Result + Snapshot` 分层完全一致。

### 3.6 实例身份不能只靠 Buff 名称

同名状态是否属于同一个运行时实例，需要显式定义匹配键。

文章列出的常见方式包括：

```text
状态类型
状态类型 + 来源单位
状态类型 + 来源上下文
目标 + 状态类型 + 来源规则
强制独立实例
```

不同选择会影响：

- 多个施法者的 DOT 是否共存。
- 同一施法者重复命中是否叠层。
- 光环和主动技能是否共享状态。
- 驱散移除一条、一个来源还是全部同类。
- 来源死亡后状态是否继续。

【主程复核】实例身份是 Buff 系统最容易被低估的设计点之一。没有明确身份，叠层、移除、归因、快照和网络同步都会不稳定。

### 3.7 Duration 与 Interval 是两种时间语义

文章强调：

```text
Duration：状态何时结束
Interval：下一次周期何时发生
```

重复申请可能有多种规则：

- 同时重置 Duration 和 Interval。
- 只刷新 Duration。
- 延长 Duration，不改变 Interval。
- 立即执行一次 Interval，再重新计时。
- 所有层共享周期。
- 每层独立周期。

【主程复核】如果这些规则被隐式写死在 `Tick()` 中，策划需求变化时很容易多跳、少跳或通过刷新状态额外触发伤害。

### 3.8 替换不是覆盖字段

旧实例可能绑定：

- 持续调度。
- Tag。
- Modifier。
- Trigger。
- 技能运行时。
- 来源上下文。
- 表现 Cue。

所以 Replace 应执行：

```text
结束旧实例
-> 停止调度
-> 撤销投影
-> 清理关联
-> 冻结来源快照
-> 发布结束结果
-> 创建新实例
```

直接覆盖层数、来源和剩余时间，会留下旧订阅、旧 Modifier 或错误归因。

### 3.9 Remove 阶段需要来源快照

状态结束时，运行时对象和上下文可能马上释放，但 Remove Trigger、击杀归因、日志和表现仍需要知道：

- 谁施加的。
- 当前层数。
- 剩余时间。
- 为什么结束。
- 关联技能或上下文。

文章通过持久来源快照解决“对象已准备释放，但后续规则仍需读取来源”的问题。

【MechStorm 决策】长期的 `StateRemoveResult` 和 `BattleActionLog` 应保存稳定 ID 与数值快照，不保存 Unity 对象或短生命周期上下文引用。

### 3.10 Continuous 是比 Buff 更底层的持续能力

文章提出轻量 `IContinuous` 概念，负责：

```text
Owner
Elapsed / Remaining
Activate
Pause
Resume
End
```

Tag、Modifier、Trigger、Cue 和网络恢复通过 Binder 或适配能力接入。

【主程复核】这个抽象有价值，但风险也很高。若 Buff、技能、移动、投射物和区域都进入同一个 ContinuousManager，它可能成为新的万能系统。共享生命周期语义不等于必须共享同一个运行时容器。

### 3.11 Tag 与 Modifier 的边界

文章的合理边界可以整理为：

| 概念 | 负责内容 |
|---|---|
| Tag | 状态语义、分类、门禁、免疫和条件匹配 |
| Modifier | 修改属性或流程数值，并保留来源 |
| State / Buff Runtime | 保存生命周期、层数、来源和持续事实 |
| Trigger | 在明确时序点匹配条件并执行动作 |
| Area | 计算范围、进入和离开 |
| Cue / Presentation | 消费权威结果并播放表现 |

长期需要坚持：

```text
State 管生命周期
Modifier 只修改值
Tag 只表达语义与条件
Area 只产生范围变化
Presentation 不决定规则
```

### 3.12 光环应拆成范围与状态

光环至少包含两类职责：

```text
Area / Spatial
  计算谁进入、谁离开

State / Modifier Projection
  为进入者添加来源明确的状态或 Modifier
  为离开者撤销同一来源的投影
```

【主程复核】把范围扫描、叠层、属性修改和表现都写在 AuraBuff 中，会导致移动性能、状态语义和属性撤销纠缠。MechStorm 后续应由空间层产生 Enter/Leave 事实，再由状态入口处理 Apply/Remove。

### 3.13 命令队列与重入控制

文章示例将 Apply / Remove 请求放入带序号的队列：

```text
外部请求入队
-> 单一 Drain 执行
-> 执行中产生的新请求继续排队
-> 限制单次最大命令数
```

它解决：

- 递归调用。
- 执行中修改活动列表。
- 连锁申请顺序混乱。
- 无限申请的最低限度保护。

但文章也承认，它不是严格的事务批次隔离。如果项目要求“当前批次”和“下一批次”明确分离，还需要双队列或冻结队列长度。

### 3.14 状态系统必须能够解释自己

文章要求系统至少能回答：

- 为什么 Apply 被接受或拒绝。
- 命中了哪个运行时实例。
- 为什么加层、刷新、替换或忽略。
- 为什么周期少了一跳或多了一跳。
- 为什么状态提前结束。
- Remove 使用了哪个来源。
- 状态哈希差异来自哪条运行时。
- 快照恢复了什么，哪些行为还没重建。

【主程复核】复杂状态系统没有 Trace、来源拆解和生命周期日志，维护成本通常会超过系统本身的开发成本。

### 3.15 数据导入不等于完整恢复

文章明确区分：

```text
恢复 Runtime 数据
≠
恢复为可运行的战斗世界
```

快照导入后还可能需要重建：

- Continuous。
- Tag 投影。
- Modifier 投影。
- Trigger 订阅。
- Owner 索引。
- 来源上下文。
- 技能运行时关联。

完整恢复流程更接近：

```text
停止当前运行时
-> 清理旧订阅和投影
-> 导入纯数据
-> 重建索引
-> 重建持续行为
-> 重建 Tag / Modifier / Trigger
-> 校验状态哈希
-> 决定是否补发表现
```

---

## 4. 从资深战斗程序视角看文章的优点

### 4.1 从职责问题出发，而不是从框架名出发

文章没有先规定“Buff 必须是什么类”，而是先枚举状态规则必须回答的问题。这符合第一性原理，也避免了机械照搬 GAS 或某个商业项目的对象模型。

### 4.2 正确认识 Buff 膨胀的根因

文章把问题定位为变化原因混合，而不是简单归咎于：

- 类太大。
- 继承层级不够。
- 配置字段太少。
- 没使用 ECS。
- 没使用 GAS。

这个判断对长期维护非常重要。

### 4.3 对生命周期顺序足够敏感

文章特别关注：

- 门禁在入表前执行。
- 初始化失败需要回滚。
- Replace 需要完整结束旧实例。
- Remove 要先保留来源事实。
- 表现不能决定权威结果。

这些都是生产环境中高频且难排查的问题。

### 4.4 正确拆开 Duration 与 Interval

这是文章最具体、最有实战价值的设计细节之一。它直接避免大量 DOT、HOT、蓄力、护盾恢复和周期触发的边界缺陷。

### 4.5 来源感知设计方向正确

同一个 Tag、属性加成或状态可能来自：

- 装备。
- Buff。
- 光环。
- 地形。
- 天赋。
- 部位破坏。

按来源添加和撤销，可以避免移除一个 Buff 时误删其他来源提供的能力。

### 4.6 兼顾简单项目和复杂项目

文章没有宣称所有项目都需要完整恢复、网络回滚和复杂 Binder，而是强调按需求装配。这与 KISS / YAGNI 更一致。

### 4.7 把可观察性视为架构能力

结构化拒绝原因、命令序号、Trace、快照、哈希和恢复边界不是附属功能，而是状态系统可以长期维护的基础。

---

## 5. 从主程视角看文章的不足和风险

### 5.1 抽象碎片化

当系统拆成：

```text
Admission
Identity Matcher
Stack Policy
Lifecycle Executor
Continuous Runtime
Binder
Trigger
Modifier
Context
Cue
Recovery Provider
```

一个简单减速也可能跨越大量对象。如果缺少统一入口、装配规则、调用链 Trace 和自动化测试，单体大类只会变成分布式大泥团。

### 5.2 Continuous 可能成为新的万能管理器

共享 `Activate / Pause / Resume / End` 并不代表 Buff、移动、技能和区域必须共用同一个调度中心。

它们可能具有不同：

- 所有者。
- 时序。
- 暂停语义。
- 快照字段。
- 恢复方式。
- 失败处理。

MechStorm 应先共享术语和接口，再根据真实复用决定是否共享实现。

### 5.3 叠层策略存在组合爆炸

真实叠层至少涉及：

- 实例匹配键。
- 最大层数。
- 同来源与不同来源。
- 到达上限后的行为。
- 层数共享或独立计时。
- 刷新 Duration 或 Interval。
- 数值求和、取最高或覆盖。
- 驱散一层、一个来源或全部。
- 来源死亡后的行为。
- 属性快照或实时读取。

`Ignore / Replace / Stack / Refresh` 枚举只能描述粗粒度类别，无法替代项目规则代码和测试。

### 5.4 Binder 会产生隐藏顺序耦合

Tag、Modifier、Trigger 和 Cue Binder 同时响应 Apply / Pause / Resume / Remove 时，需要明确：

- Binder 稳定顺序。
- 前一步失败后的处理。
- 是否允许产生新状态请求。
- Pause 与 Remove 是否复用撤销路径。
- 恢复时是否执行正常 Apply 逻辑。
- 是否需要补偿或事务。

否则顺序依赖只是从 Buff 类转移到 Binder 列表。

### 5.5 Trigger 重入和事件风暴仍未完全解决

命令队列只能降低递归风险，生产系统仍需处理：

- Apply Trigger 再次 Apply 自己。
- Remove Trigger 重新添加刚移除状态。
- A 触发 B，B 再触发 A。
- 多段攻击重复触发。
- 死亡后是否继续处理剩余 Trigger。
- 当前批次或下一批次。
- 单行动最大深度与次数。

需要同时限制：

```text
FollowUpDepth
TriggerCount
PerActionBudget
Sequence
Scope
```

### 5.6 Modifier 计算模型没有完整展开

文章强调 Modifier 来源和撤销，但没有完整解决：

```text
Base
Add
Multiply
Override
Clamp
FinalModify
```

仍需项目明确：

- 同层合并规则。
- Override 优先级。
- 整数缩放。
- 取整时机。
- 最大值变化时当前值的处理。
- 属性依赖和循环检测。

### 5.7 实时时钟假设不适合直接搬入 SRPG

文章主要使用秒级 Duration、Interval、暂停和恢复。

MechStorm 应转换为权威回合制时序：

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

“持续三回合”必须说明在哪个时序点减少，否则施加时间不同会产生一回合偏差。

### 5.8 快照恢复的行为重建成本很高

文章正确指出恢复数据后仍需重建投影，但没有给出一个足够通用的事务模型。生产项目必须自行定义：

- 是否补发 Apply。
- 是否重播 Cue。
- 如何避免重复 Trigger。
- 如何清理旧订阅。
- 配置版本不一致如何处理。
- 恢复失败如何回滚。

### 5.9 确定性依赖没有全部显式化

要支持哈希、回放和服务端权威，还需要：

- 稳定 RuntimeId。
- 稳定 SourceId。
- 稳定容器遍历顺序。
- 明确 Sequence。
- 确定性 RNG。
- 固定配置版本。
- 不依赖 Unity 时间和场景对象。
- Trigger Payload 在执行期间不可被随意修改。

这些不能只依靠“使用命令队列”自然获得。

### 5.10 性能成本不会因拆分类而消失

真正的运行成本包括：

- 周期调度。
- 光环范围变化。
- Owner 索引。
- Tag 聚合。
- Attribute 重算。
- Trigger 条件匹配。
- Snapshot 排序和序列化。
- Trace 和日志。
- 恢复时行为重建。

对象池只能减少部分分配，不能解决算法和数据访问成本。

### 5.11 工具链是必要成本

复杂状态系统至少需要查看：

- 当前 State / Buff。
- RuntimeId 与 SourceId。
- 当前层数。
- 剩余时长和下一周期。
- Tag 来源。
- Modifier 来源。
- 叠层决策。
- RemoveReason。
- Trigger 父子链。
- Snapshot Diff。
- 恢复重绑结果。

如果没有工具，策划、测试和程序无法共同解释“为什么现在是这个结果”。

---

## 6. 对 MechStorm 的参考价值

### 6.1 与项目现有方向的一致点

文章与 MechStorm 已有规划在以下方面一致：

| 文章观点 | MechStorm 现有方向 |
|---|---|
| Buff 不是万能对象 | 轻量 GAS 变种，State / Tag / Modifier / Trigger 分工 |
| 配置与运行时分离 | `Data + Runtime` |
| 表现只消费结果 | `MechStorm.Battle` 无头核心 + `BattleActionResult` |
| 来源需要稳定记录 | 服务端权威、快照、回放和局部恢复 |
| 主流程显式 | `BattleSession + Resolver + Result` |
| 复杂能力按需装配 | KISS / YAGNI，Modifier / Trigger 后置 |
| 状态必须可解释 | Task 2.7 Snapshot / ActionLog 与后续调试工具 |

### 6.2 当前 P1 / Sprint 2

当前不实现 Buff，但立即采用以下边界：

1. `BattleActionResult` 使用结构化失败原因。
2. 结果中的变化类型（可理解为一次操作中依次发生的战斗事件，不是程序事件）保持稳定顺序。
3. 结果记录 Actor、Target 和客观阵营事实。
4. 表现层只消费结果，不重新结算。
5. Task 2.7 的日志和快照不保存 Unity 对象。
6. 为未来状态操作预留稳定 Sequence 和 FailureReason 思路。

当前明确暂缓：

- `BuffRuntime`。
- `ContinuousManager`。
- 通用叠层策略。
- Modifier。
- Trigger。
- 状态恢复。
- 全局事件总线。

### 6.3 Sprint 3

Sprint 3 先建立：

```text
AbilityDefinition
TargetRule
BattleEffectResult
强类型 Timing Anchor
```

普通攻击和主动技能先统一输出 Result；只有多个步骤确实需要共享和修改中间数据时，才引入最小 `AbilityContext`，不急着建立通用 Buff 生命周期。

### 6.4 P2 / Sprint 4~5

当部位破坏、眩晕、DOT、护盾和警戒进入开发时，再建立轻量状态主干：

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

建议优先使用以下样例验证架构：

1. 眩晕：禁止行动并在固定回合时序结束。
2. 断腿：提供 `State.Broken.Leg`，将移动力 Override 为 1。
3. 三回合 DOT：Duration 与 Interval 独立。
4. 简单护盾：受击消耗，归零后以明确原因 Remove。
5. 警戒：在明确 Timing Anchor 上产生反应攻击。

### 6.5 P3 复杂机制阶段

在真实需求出现后再扩展：

- 多来源叠层。
- 刷新与延长策略。
- 计次型护盾。
- 光环 Enter / Leave。
- 驱散、免疫和互斥。
- Trigger 条件链。
- 追击、反击和协力。
- 元件与天赋注入。
- Trigger 深度和次数限制。
- 正式 Command / Result Replay。

### 6.6 P4 / P5 工具与内容生产

文章直接支持项目后续建设：

- State Schema。
- StackPolicy / RefreshPolicy。
- MutexStates。
- Modifier 和 Trigger 配置。
- StateChangeLog。
- TriggerExecutionLog。
- AttributeBreakdown。
- 配置静态校验。
- 战斗沙盒。
- 固定回归样例。
- 状态透视仪。

---

## 7. 采用、暂缓与拒绝清单

| 文章思想 | MechStorm 决策 | 原因 |
|---|---|---|
| Buff 是业务语言，不是底层万能对象 | 采用 | 与轻量 GAS 变种一致 |
| 配置、运行时和规则出口分离 | 采用 | 支持测试、Luban、快照和服务端 |
| Duration 与 Interval 分离 | 采用 | 回合 DOT 同样需要独立语义 |
| 来源感知的实例身份和撤销 | 采用 | 装备、光环、状态和部位会共同影响属性 |
| Apply / Remove 统一入口 | 采用 | 确定性和调试基础 |
| 结构化拒绝原因和结果 | 采用 | Task 2.6 与 Task 2.7 直接需要 |
| 表现层只消费结果 | 采用 | 无头核心约束 |
| 数据恢复与行为重绑定分离 | 采用 | 避免伪恢复 |
| Continuous 统一 Buff、技能和运动 | 谨慎暂缓 | 容易形成新的万能中心 |
| 生命周期 Binder | P2 后评估 | 需先有真实 Tag / Modifier / Trigger 需求 |
| 通用叠层策略系统 | P2 / P3 再做 | 当前没有 Buff 验收需求 |
| 完整状态哈希和恢复 | 分阶段采用 | 先 Result / Snapshot / Log |
| 预测回滚 Buff | 默认不采用 | 项目是服务端权威回合制 |
| Buff 自己 Update / Tick | 拒绝 | 破坏统一调度和确定性 |
| Buff 直接改表现 | 拒绝 | 破坏无头核心 |
| 全局事件总线驱动状态主流程 | 拒绝 | 顺序不确定、易产生事件风暴 |
| 一张 Buff 表表达全部规则 | 拒绝 | 容易演化成不可调试 DSL |

---

## 8. MechStorm 后续状态系统的强制设计问题

### 8.1 术语

1. `State`、`Buff`、`Effect`、`Modifier`、`Tag`、`Trigger` 各自负责什么？
2. Buff 是否只作为策划语言，运行时统一使用 `StateRuntime`？
3. 护盾、警戒、过热和装备被动是否具有相同生命周期？
4. 哪些规则是一次性 Effect，哪些规则是真正持续状态？

### 8.2 身份与叠层

5. 状态实例键由哪些字段组成？
6. 同名不同来源是否共存？
7. 同来源重复申请如何处理？
8. 每层独立计时还是共享计时？
9. 刷新 Duration 是否影响 Interval？
10. 到达上限后忽略、延长、替换还是触发结算？
11. 驱散移除一层、一个来源还是全部？

### 8.3 生命周期顺序

12. Apply、Pause、Resume、Remove 的唯一权威入口在哪里？
13. Tag、Modifier、Trigger 和 Result 的固定顺序是什么？
14. Remove Trigger 执行时 Modifier 是否仍有效？
15. 死亡清理是否执行正常 Remove 逻辑？
16. 同一行动内 Refresh 和 Dispel 的顺序是什么？
17. 连锁请求进入当前批次还是下一批次？

### 8.4 数值和确定性

18. Modifier 的完整计算顺序是什么？
19. 同层 Modifier 如何排序？
20. 倍率使用什么整数缩放？
21. 每一步在哪个位置取整？
22. DOT 使用施加时快照还是周期时实时属性？
23. 属性来源变化后旧状态如何解释？

### 8.5 快照和恢复

24. Snapshot 保存哪些 DefinitionId、RuntimeId、SourceId 和 ContextId？
25. 如何重建 Tag、Modifier、Trigger 和 Owner 索引？
26. 恢复是否补发 Apply 或 Cue？
27. 配置版本变化后旧快照如何处理？
28. 如何证明下一次周期不会遗漏或重复？

### 8.6 性能和工具

29. 单场最大单位、状态和 Trigger 数量是多少？
30. 状态推进按单位扫描、时序桶还是优先队列？
31. 光环按移动增量更新还是全量范围查询？
32. Trace 是常开、采样还是调试构建开启？
33. 策划如何查看状态来源和叠层决策？
34. 如何静态检查循环 Trigger、无效 Tag 和不可达条件？

---

## 9. 建议的长期调用流程

### 9.1 Apply

```text
StateApplyRequest
-> Validate Definition / Source / Target
-> Evaluate Immunity / Tag / Mutex
-> Match Runtime Identity
-> Resolve Stack / Refresh / Replace
-> Prepare Source Snapshot
-> Initialize Runtime
-> Bind Scheduler
-> Project Tags / Modifiers
-> Execute OnApplied Rules
-> Emit StateApplyResult
```

### 9.2 Interval

```text
Timing Anchor 到达
-> Scheduler 选择到期状态
-> 冻结本次阶段上下文
-> 执行 Interval Rule
-> 生成 Damage / StateChange Result
-> 更新下一次时序
```

### 9.3 Remove

```text
确定 StateRemoveReason
-> 防止重复结束
-> 停止调度
-> 冻结来源与运行时快照
-> 按固定顺序撤销 Tag / Modifier / Trigger
-> 清理索引和上下文
-> 执行 Remove Rule
-> Emit StateRemoveResult
-> 释放运行时资源
```

### 9.4 Restore

```text
停止当前状态世界
-> 清理旧订阅和投影
-> 导入 StateRuntime 数据
-> 重建 RuntimeId / SourceId 索引
-> 重建 Scheduler
-> 重建 Tag / Modifier / Trigger
-> 校验状态哈希
-> 恢复可运行状态
-> 按策略刷新表现
```

---

## 10. 最终评价

### 10.1 文章的主要价值

1. 把 Buff 膨胀问题解释为职责和变化原因混合。
2. 给出了较完整的生命周期、失败和清理边界。
3. 强调实例身份、来源追踪和恢复语义。
4. 明确配置、运行时、规则和表现的区别。
5. 反对所有项目复制统一的重型 Buff 模板。
6. 将可观察性和恢复视为系统能力。

### 10.2 文章的主要局限

1. 主要示例来自实时 MOBA，时钟语义不能直接迁移。
2. Continuous 可能演化成新的万能抽象。
3. Binder、叠层和 Trigger 的组合复杂度仍需项目自行控制。
4. Modifier 计算顺序和事务失败没有完全展开。
5. 完整恢复、配置版本和确定性约束仍需生产项目补齐。
6. 没有工具链时，拆分后的系统可能更难理解。

### 10.3 对 MechStorm 的最终结论

当前最正确的做法不是立即实现 Buff，而是把文章中的边界逐步落入项目路线：

```text
Sprint 2
  Result / FailureReason / Sequence / Snapshot 雏形

Sprint 3
  Ability / EffectResult / Timing Anchor

P2
  StateDefinition / StateRuntime / Attribute / Source-aware Tag

P3
  Modifier / StackPolicy / Trigger / Aura / Dispel

P4/P5
  Trace / Breakdown / Recovery / Editor / Regression Samples
```

文章应作为 MechStorm 后续 Spike 2.A“特异性 Buff 与光环”以及轻量 GAS 状态主干的重要参考，但不高于 `DEVELOPMENT_PROGRESS.md`、`BATTLE_ARCHITECTURE_ROADMAP.md` 和项目主计划中的阶段决策。
