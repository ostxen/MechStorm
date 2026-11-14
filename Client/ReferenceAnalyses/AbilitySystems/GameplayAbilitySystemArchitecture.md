# Gameplay Ability System 模块边界与扩展架构分析

代码库地址：<https://github.com/LiGameAcademy/godot_ability_system>

参考实现使用 Godot 4，但本文关注的是 Ability、Attribute、Status、Effect、Tag、Targeting 和调试工具的职责边界，引擎节点、资源和信号仅作为实现背景。

分析结论：这是一个偏实时动作、ARPG、MOBA 方向的 Godot 4 Gameplay Ability System 插件。它的价值不在于可直接搬代码，而在于把技能、属性、状态、效果、标签、目标选择、表现 Cue 和调试工具拆成了较清晰的模块。从架构适用性看，它适合作为轻量 GAS 变种的长期参考，但不适合在 早期阶段 直接照搬。

## A. 总览与定位

### A.1 代码库定位

`README.md` 明确该项目参考 Unreal GAS，强调：

- 数据驱动：技能、状态、效果由资源配置。
- 组件化：角色通过 Ability、Attribute、Status、Vital 组件接入系统。
- 解耦：逻辑通过组件、信号、事件总线、Cue 分发。
- 可扩展：通过 Feature、Effect、Behavior Tree、Targeting Strategy 扩展。

整体适合 Godot 编辑器资源工作流，核心依赖 `Node`、`Resource`、`Autoload`、`_process(delta)`、Godot 信号和 Group。

## B. 架构结构与运行模型

### B.1 目录结构与关键模块

#### B.1.1 根目录与插件入口

- `README.md`
  - 系统总览，说明核心模块和设计原则。
- `plugin.gd`
  - 注册 Autoload 单例：
    - `GameplayAbilitySystem`
    - `DamageCalculator`
    - `TagManager`
    - `AbilityEventBus`
    - `GameplayCueManager`
- `plugin.cfg`
  - 插件元信息。

这些单例让 Godot 使用很方便，但对可测试、多战斗实例、服务端复用并不理想。

#### B.1.2 文档

- `docs/architecture.md`
  - 分为业务层、组件层、实例层、资源层、系统层。
- `docs/ability_system.md`
  - 技能定义、技能实例、技能组件、Feature、行为树执行。
- `docs/attribute_system.md`
  - 属性定义、属性集、属性实例、Modifier。
- `docs/status_system.md`
  - Buff/Debuff、叠加策略、持续时间策略、状态 Feature。
- `docs/effect_system.md`
  - GameplayEffect、伤害、治疗、状态、属性修改。
- `docs/filter_system.md`
  - 基于 Tag 的目标过滤。
- `docs/behavior_tree.md`
  - 技能行为树、黑板和调试。

#### B.1.3 组件层

- `scripts/components/gameplay_ability_component.gd`
  - 技能容器，负责学习、遗忘、禁用、启用、输入匹配、预览、激活技能。
  - 每帧遍历已学技能并调用 `ability_instance.update(delta)`。
- `scripts/components/gameplay_attribute_component.gd`
  - 属性容器，管理属性集实例。
- `scripts/components/gameplay_vital_attribute_component.gd`
  - Vital 组件，管理生命、魔法等当前值。
- `scripts/components/gameplay_status_component.gd`
  - 状态容器，管理 Buff/Debuff 生命周期、叠加、移除。

#### B.1.4 技能系统

- `scripts/abilities/gameplay_ability_definition.gd`
  - 静态技能定义，包含 `ability_id`、`features`、`preview_strategy`、`execution_tree`、黑板默认值等。
- `scripts/abilities/gameplay_ability_instance.gd`
  - 运行时技能实例，持有 owner、definition、features、行为树实例、黑板和激活状态。
- `scripts/abilities/features/*.gd`
  - 冷却、消耗、输入、切换、被动状态、动态图标等 Feature。
- `scripts/abilities/ability_nodes/*.gd`
  - 行为树中的技能节点，如扣费、进冷却、寻找目标、应用效果。
- `scripts/abilities/targeting/**/*.gd`
  - 目标选择、命中检测、地面预览、范围预览等策略。

核心设计是：

```text
GameplayAbilityDefinition
-> create_instance(owner)
-> GameplayAbilityInstance
-> GameplayAbilityComponent 管理
-> Feature 检查与钩子
-> Behavior Tree 执行技能时序
-> AbilityNode 应用 Effect
```

#### B.1.5 属性系统

- `scripts/attributes/gameplay_attribute.gd`
  - 属性定义。
- `scripts/attributes/gameplay_attribute_set.gd`
  - 属性集合。
- `scripts/attributes/gameplay_attribute_instance.gd`
  - 运行时属性实例，维护 `base_value`、Modifier 列表、脏标记缓存。
- `scripts/attributes/gameplay_attribute_modifier.gd`
  - 属性修改器。

计算方式核心是：

```text
Override 优先
否则 Final = (Base + Add 总和) * (1 + Multiply 总和)
最后 Clamp 到属性上下限
```

这与常见轻量 GAS 规划中的 `Base -> Add -> Multiply -> Override -> Clamp` 很接近，但 Godot 版本使用 `float`，确定性战斗核心应继续用整数或万分比。

#### B.1.6 状态系统

- `scripts/status/gameplay_status_data.gd`
  - 状态静态配置：持续时间、叠加策略、标签、效果、Feature、Cue。
- `scripts/status/gameplay_status_instance.gd`
  - 状态运行时实例：层数、剩余时间、持续时间策略、上下文、Feature 存储、Cue 实例。
- `scripts/status/stacking/*.gd`
  - 刷新、叠层、累计持续时间等策略。
- `scripts/status/duration/*.gd`
  - 持续时间策略。当前偏自然时间。
- `scripts/status/features/*.gd`
  - 周期效果和事件监听。

状态数据流：

```text
GE_ApplyStatus
-> GameplayStatusComponent.apply_status()
-> GameplayStatusInstance.apply()
-> apply_effects
-> feature.apply_feature
-> duration_policy.initialize
-> 移除互斥状态
-> 注入 Tag
-> 播放 Cue
```

#### B.1.7 效果系统

- `scripts/effects/gameplay_effect.gd`
  - 效果基类，处理过滤器、Tag 条件、Cue、子效果链。
- `scripts/effects/ge_apply_damage.gd`
  - 伤害效果。
- `scripts/effects/ge_modify_vital.gd`
  - 修改生命、魔法等 Vital。
- `scripts/effects/ge_attribute_modifier.gd`
  - 添加或移除属性 Modifier。
- `scripts/effects/ge_apply_status.gd`
  - 应用状态。
- `scripts/effects/ge_dispel_status.gd`
  - 驱散状态。
- `scripts/effects/ge_status_transform.gd`
  - 状态转换。

效果基类的执行顺序：

```text
检查 filters
-> 检查 target_required_tags / target_blocked_tags
-> 执行子类 _apply
-> 执行 Cue
-> 递归执行 sub_effects
```

这套“原子效果 + 子效果链”的思路，适合作为复杂技能系统参考。

#### B.1.8 Tag、事件与 Cue

- `scripts/tags/gameplay_tag.gd`
  - Tag 资源，含 ID、显示、父标签、互斥标签。
- `scripts/singletons/gameplay_tag_manager.gd`
  - Tag 注册、引用计数、继承查询、互斥处理、Godot Group 集成。
- `scripts/singletons/ability_event_bus.gd`
  - 全局事件总线。
- `scripts/cues/*.gd`
  - GameplayCue 表现反馈。

Tag 引用计数和互斥关系值得参考，因为 复杂战斗系统后期会遇到同一个状态标签来自多个来源的问题，例如装备、Buff、地形光环同时提供 `Locomotion.Hover`。

#### B.1.9 调试 UI

- `ui/tag_debug_panel.gd`
- `ui/status_debug_panel.gd`
- `ui/bt_debug_panel.gd`
- `ui/ability_bar.gd`
- `ui/ability_button.gd`

GAS 类系统没有调试工具会很难排查。该仓库的调试意识值得作为调试体系参考，尤其是状态快照、行为日志、属性来源解释和 Tag/State 面板。

### B.2 核心流程解析

#### B.2.1 技能学习

锚点：`scripts/components/gameplay_ability_component.gd`

```text
GameplayAbilityComponent._ready()
-> 遍历 _initial_abilities
-> learn_ability(definition)
-> definition.create_instance(get_parent())
-> GameplayAbilityInstance(owner, definition)
-> 加入 _learned_abilities
-> 连接 ability_completed
-> handle_learned()
-> 被动 Feature 可立即应用状态
```

设计意图：静态定义和运行时实例分离，组件只负责持有和调度。

#### B.2.2 技能激活

锚点：`scripts/abilities/gameplay_ability_instance.gd`

```text
GameplayAbilityComponent.try_activate_ability()
-> 填充 context: ability / ability_component / ability_id / instigator
-> GameplayAbilityInstance.try_activate()
-> can_activate()
-> 所有 Feature.can_activate 必须通过
-> 清空并重建黑板
-> Feature.on_activate()
-> is_active = true
-> update(delta) 中 tick 行为树
-> 行为树完成后 end_ability()
```

关键点：`try_activate` 不直接扣资源、不直接产生效果。资源消耗和冷却提交由行为树节点决定时机。

这里的阶段拆分有启发：技能合法性检查、扣费、结算、冷却或行动配额提交，应是流程中的明确步骤，而不是混在 `CanUse` 中。

#### B.2.3 技能时序

典型主动技能可以表达为：

```text
播放动画
-> 等待
-> 提交冷却
-> 提交消耗
-> 搜索目标
-> 应用效果
-> 等待后摇
```

优点是灵活，缺点是对早期回合制战斗流程过重。早期回合制战斗更需要显式流程、明确输入输出和结构化结果。

#### B.2.4 状态生命周期

锚点：`scripts/status/gameplay_status_instance.gd`

```text
apply()
-> 缓存上下文
-> 应用 apply_effects
-> 应用 status features
-> 初始化 duration policy
-> 移除互斥状态
-> 注入状态 tags
-> 执行状态 cue

remove()
-> 应用 remove_effects
-> 移除 apply_effects
-> 移除 features
-> 移除 tags
-> 停止 cue
```

这符合“State 管生命周期，Modifier 管数值变化”的方向。后续复杂状态系统可参考，但必须注意表现 Cue 与逻辑状态的边界。

## C. 架构评价

### C.1 它解决的问题

1. **技能不再写成一个个硬编码方法**
   - 技能由 Definition、Feature、Behavior Tree、Effect 组合。
2. **消耗和冷却时机可配置**
   - 扣费和进冷却由行为树节点触发，不绑定到激活瞬间。
3. **Buff/Debuff 有统一生命周期**
   - StatusInstance 管层数、持续时间、Tag、Feature。
4. **效果可复用**
   - 伤害、治疗、属性修改、状态应用都继承 `GameplayEffect`。
5. **Tag 统一分类、过滤、互斥**
   - 状态免疫、效果条件、标签查询可复用。
6. **表现反馈有 Cue 出口**
   - 逻辑效果触发表现 Cue，避免每个效果硬编码特效。
7. **调试入口完整**
   - Tag、Status、Behavior Tree 都有可视化调试面板。

### C.2 优点

#### C.2.1 静态定义与运行时实例分离

值得在同类架构中保持并扩展：

```text
PilotData / PilotRuntime
MechData / MechRuntime
AbilityData / AbilityRuntime
StateData / StateRuntime
AttributeDefinition / AttributeRuntime
```

#### C.2.2 Feature 机制灵活

Feature 把冷却、消耗、输入、被动、切换等能力从 Ability 主体拆出去，避免技能类无限膨胀。

可抽象为更明确的规则对象：

```text
AbilityCostRule
AbilityTargetRule
AbilityAvailabilityRule
AbilityCommitRule
AbilityCooldownRule
```

#### C.2.3 Effect 原子化

技能、状态、事件监听都复用同一批 Effect，非常适合后续机甲技能、部位破坏、状态触发和装备词条。

#### C.2.4 Tag 引用计数有现实价值

如果一个 Tag 来自多个源，不能简单 Add/Remove 布尔值。应能按来源移除：

```text
AddTag(sourceId, tag)
RemoveTagsBySource(sourceId)
GetTagCount(tag)
```

#### C.2.5 调试工具思路正确

复杂战斗调试工具至少需要：

- 当前 Tag 列表
- 当前 State 列表
- 属性最终值和来源
- 最近 Trigger
- 最近 Attack / Damage / Move Context
- BattleActionLog

### C.3 缺点与潜在问题

#### C.3.1 强绑定 Godot 运行时

核心逻辑依赖：

- `Node`
- `Resource`
- `InputEvent`
- `_process(delta)`
- Godot 信号
- Autoload 单例
- Godot Group
- `Vector2` / `Vector3` 类思维

这些不适合纯逻辑战斗核心。

#### C.3.2 偏实时游戏，不是回合制战棋

状态时间主要由自然时间和每帧更新驱动。回合制系统更适合采用：

```text
Timing.Turn.Start
Timing.Turn.End
Timing.Action.Before
Timing.Action.After
Timing.Battle.Before
Timing.Battle.After
Timing.Hit.Before
Timing.Hit.After
```

#### C.3.3 全局单例影响测试和回放

Autoload 单例让示例易用，但多战斗实例架构需要局部战斗实例，便于：

- EditMode 测试
- 多战斗实例
- 服务端权威
- 回放
- 续战
- 悔棋

#### C.3.4 行为树技能系统对当前阶段过重

早期回合制战斗目标是多单位、普通移动、普通攻击、死亡和回合推进。不应引入完整行为树技能执行。

#### C.3.5 浮点和帧时间不适合确定性核心

该仓库使用 `float`、自然时间和帧更新。确定性核心结算应坚持整数化和确定性 RNG。

#### C.3.6 一些实现细节有坑

静态阅读发现：

- `scripts/abilities/ability_nodes/ability_node_commit_cost.gd`
  - 注释期望扣费失败返回失败，但实现中可能忽略 `try_pay` 结果。
- `scripts/abilities/ability_nodes/ability_node_apply_effect.gd`
  - 存在 fallback 字段但调用时可能固定不启用。
- `scripts/attributes/gameplay_attribute_instance.gd`
  - `_set_base_value` 中 old/new 信号参数可能不准确，因为先赋值再 emit。
- Tag 必须先注册，否则 `TagManager.add_tag` 可能失败。

这些不影响它的设计参考价值，但说明不能直接把实现当生产级模板。

## D. 深度复查：资深战斗 / GAS 程序视角

本节补充前文没有展开或展开不够细的点，重点看实际运行调用链、类组职责、GAS 风格系统在真实项目里的风险。

### D.1 实际运行调用流程

#### 插件与全局单例启动

入口：`plugin.gd`

```text
EditorPlugin._enter_tree()
-> add_autoload_singleton("GameplayAbilitySystem")
-> add_autoload_singleton("DamageCalculator")
-> add_autoload_singleton("TagManager")
-> add_autoload_singleton("AbilityEventBus")
-> add_autoload_singleton("GameplayCueManager")
```

这说明系统默认所有战斗共享一套单例。对 Godot 插件很方便，但对需要 局部战斗实例、单元测试、回放、多战斗并行和服务端复用的项目，不适合原样照搬。

#### 技能学习流程

关键文件：

- `scripts/components/gameplay_ability_component.gd`
- `scripts/abilities/gameplay_ability_definition.gd`
- `scripts/abilities/gameplay_ability_instance.gd`

```text
GameplayAbilityComponent._ready()
-> 遍历 _initial_abilities
-> learn_ability(ability_data)
-> ability_data.create_instance(get_parent())
-> GameplayAbilityInstance.new(owner, definition)
   -> 创建 GAS_BTBlackboard
   -> blackboard.set_var("ability_instance", self)
   -> 如果 definition.execution_tree 存在，创建 GAS_BTInstance
-> GameplayAbilityDefinition.create_instance()
   -> 注入 blackboard_defaults
   -> 注入 features
-> 写入 _learned_abilities
-> 连接 ability_completed
-> learned_ability.handle_learned(self)
   -> Feature.on_learned()
   -> PassiveStatusFeature 可在学习时应用被动状态
-> ability_learned.emit()
-> AbilityEventBus.trigger_game_event("ability_learned", ...)
```

补充点：被动技能不是激活时才生效，而是在 `learn_ability()` 阶段通过 `Feature.on_learned()` 生效。这一点在装配型战斗系统中很重要，因为“装备、天赋、被动技能”更像装配阶段注入 State / Modifier，而不是战斗中主动释放。

#### 输入、预览与激活入口

关键文件：

- `scripts/components/gameplay_ability_component.gd`
- `scripts/abilities/gameplay_ability_instance.gd`
- `scripts/abilities/targeting/ability_preview_strategy.gd`
- `ui/ability_bar.gd`

```text
输入事件
-> GameplayAbilityComponent.match_input(event)
-> AbilityInputFeature.match_input(event)
-> 返回 ability_id
```

```text
GameplayAbilityComponent.request_ability_preview(ability_id)
-> can_activate_ability(ability_id)
-> 没有 preview_strategy 或 smart_cast 时返回 null
-> 否则 ability_instance.start_targeting()
-> 外部每帧 update_targeting(delta, input_context)
-> confirm_targeting()
-> preview_strategy.get_result_context()
-> try_activate_targeting_ability()
```

```text
GameplayAbilityComponent.try_activate_ability(ability_id, context)
-> 填充 context.ability / ability_component / ability_id / instigator
-> GameplayAbilityInstance.try_activate(context)
```

UI 按钮不是完整释放入口。`ui/ability_bar.gd` 的按钮点击只发出：

```text
AbilityEventBus.trigger_game_event("ability_button_pressed", {"ability_id": ability_id})
```

业务侧还需要监听这个事件，再调用 `try_activate_ability()`。这说明 UI、输入和 AbilityComponent 之间不是强闭环，而是靠事件桥接。

#### Ability 激活生命周期

关键文件：`scripts/abilities/gameplay_ability_instance.gd`

首次激活：

```text
GameplayAbilityInstance.try_activate(context)
-> can_activate(context)
   -> 遍历所有 Feature.can_activate()
   -> 任一 false 则失败
-> _blackboard.clear()
-> blackboard.set_var("ability_instance", self)
-> blackboard.set_var("context", context)
-> blackboard.set_var("target", context.get("input_target"))
-> blackboard.set_var("is_first_activation", true)
-> 遍历 Feature.on_activate()
-> is_active = true
```

每帧更新：

```text
GameplayAbilityComponent._process(delta)
-> 遍历所有 _learned_abilities
-> ability_instance.update(delta)
   -> Feature.update(delta)
   -> 如果 is_active，_bt_instance.tick(delta)
   -> 行为树非 RUNNING 时 end_ability(result)
```

结束：

```text
GameplayAbilityInstance.end_ability(final_status)
-> is_active = false
-> Feature.on_completed()
-> ability_completed.emit(success)
-> _bt_instance.reset_tree()
-> GameplayAbilityComponent._on_ability_completed()
   -> ability_completed.emit(ability, success)
   -> AbilityEventBus.trigger_game_event("ability_completed", ...)
   -> 清空 _current_casting_ability / _current_targeting_ability
```

已激活时再次输入：

```text
GameplayAbilityInstance.try_activate(context)
-> is_active == true
-> 写黑板 event_input_received
-> Feature.can_activate()
-> Feature.on_activate()
-> 不重新启动行为树
```

这主要服务连击和 Toggle。它是实时动作游戏的输入窗口模型，不适合早期回合制指令模型原样使用。

### D.2 Active / Combo / Toggle 三类技能模板

#### ActiveAbilityDefinition

关键文件：`scripts/abilities/definitions/active_ability_definition.gd`

默认树：

```text
GAS_BTSequence "ability_sequence"
-> AbilityNodePlayAnimation
-> GAS_BTWait(pre_cast_delay)
-> AbilityNodeCommitCooldown
-> AbilityNodeCommitCost
-> AbilityNodeTargetSearch
-> AbilityNodeApplyEffect
-> GAS_BTWait(post_cast_delay)
```

设计价值：它把“校验、冷却、扣费、目标、效果”拆成阶段。同类系统可以吸收阶段划分，但早期阶段引入行为树会显著增加复杂度。

#### ComboAbilityDefinition

关键文件：`scripts/abilities/definitions/combo_ability_definition.gd`

```text
ComboAbilityDefinition._build_combo_tree()
-> root Sequence
   -> AbilityNodeCommitCost
   -> GAS_BTRepeatUntilFailure
      -> GAS_BTSwitch(combo_index)
         -> 每段 ActiveAbilityDefinition 子树
         -> 非最后一段 GAS_BTWaitSignal(event_input_received, timeout)
         -> GAS_BTSetVar(combo_index = next)
   -> AbilityNodeCommitCooldown
```

这是动作游戏连击窗口模型。其价值主要是“输入事件可以驱动流程分支”的思路，不应作为战棋技能实现主线。

#### ToggleAbilityDefinition

关键文件：

- `scripts/abilities/definitions/toggle_ability_definition.gd`
- `scripts/abilities/features/toggle_feature.gd`

```text
ToggleFeature.can_activate()
-> 如果 ability.is_active，写 context.skip_cost / skip_cooldown

ToggleFeature.on_activate()
-> 首次激活写 blackboard.toggle_action = "turn_on"
-> 再次激活写 blackboard.toggle_action = "turn_off"
-> reset_tree()
```

这适合“开关技能、架势、持续引导”。后续如果有警戒、架势、防御模式，可以参考“再次提交同一能力会切换状态”，但需要用显式指令表达。

### D.3 类组职责表

| 类 / 类组 | 关键文件 | 实际职责 | 备注 |
|---|---|---|---|
| `GameplayAbilityComponent` | `scripts/components/gameplay_ability_component.gd` | 技能容器，学习、遗忘、输入匹配、预览、激活、每帧更新 | 组件持有运行时实例的职责清晰，但 `_process` 驱动会引入帧依赖 |
| `GameplayAbilityDefinition` | `scripts/abilities/gameplay_ability_definition.gd` | 静态技能定义，创建运行时实例并注入 Feature / 黑板默认值 | 静态定义与运行时实例分离清晰 |
| `GameplayAbilityInstance` | `scripts/abilities/gameplay_ability_instance.gd` | 单个技能运行时，管理激活状态、Feature、BT、黑板 | 生命周期边界清晰，但执行方式依赖帧驱动和行为树 |
| `GameplayAbilityFeature` | `scripts/abilities/features/*.gd` | 冷却、消耗、输入、被动、切换等扩展钩子 | 扩展钩子灵活，但弱类型上下文会增加调试成本 |
| `AbilityNode*` | `scripts/abilities/ability_nodes/*.gd` | 行为树里的技能动作节点，扣费、进 CD、找目标、应用效果 | 阶段拆分清晰，但行为树复杂度较高 |
| `TargetingStrategy` | `scripts/abilities/targeting/targeting_strategy.gd` | 目标选择、过滤、排序、裁切 | 目标选择职责清晰 |
| `GameplayEffect` | `scripts/effects/gameplay_effect.gd` | 原子效果基类，过滤、Tag 条件、Cue、子效果链 | 原子效果边界清晰 |
| `GameplayAttributeInstance` | `scripts/attributes/gameplay_attribute_instance.gd` | Base、Add、Multiply、Override、缓存和来源索引 | 概念可取，实现需重写并整数化 |
| `GameplayStatusComponent` | `scripts/components/gameplay_status_component.gd` | 状态容器，应用、移除、堆叠、事件响应 | 状态容器职责清晰，但事件作用域需要收敛 |
| `GameplayStatusInstance` | `scripts/status/gameplay_status_instance.gd` | 状态运行时，层数、持续时间、Feature、Tag、Cue | 生命周期数据清晰，但 Cue 与逻辑耦合 |
| `TagManager` | `scripts/singletons/gameplay_tag_manager.gd` | Tag 注册、继承、互斥、引用计数、缓存 | 引用计数有价值，但缺少来源级管理 |
| `AbilityEventBus` | `scripts/singletons/ability_event_bus.gd` | 全局事件总线 | 全局广播方便但作用域过宽 |
| `GameplayCueManager` | `scripts/singletons/gameplay_cue_manager.gd` | 表现反馈管理 | 表现职责明确，但不应与逻辑结算混合 |
| `ProjectileBase` / `MagicFieldBase` | `scripts/entites/*.gd` | 投射物和区域载具，碰撞或周期触发 Payload | 载具运输 Payload 概念清晰，但实现依赖物理与场景树 |

### D.4 真实项目风险

#### 全局事件总线可能造成跨实体误触发

关键文件：

- `scripts/singletons/ability_event_bus.gd`
- `scripts/components/gameplay_status_component.gd`
- `scripts/attributes/vitals/health_vital.gd`
- `scripts/status/features/feature_event_listener.gd`

风险链路：

```text
任意单位受伤
-> HealthVital.apply_damage()
-> AbilityEventBus.trigger_game_event("damage_received", {"damage_info": damage_info})
-> 所有 GameplayStatusComponent._on_game_event_occurred()
-> 所有监听 damage_received 的状态都有机会响应
```

如果状态自身没有过滤 target，就可能出现“别人受伤，我的 Buff 触发”。事件系统需要具备明确的作用域、source / target / action context 和 timing。

#### TagManager 不是来源级引用计数

当前 Tag 引用计数是：

```text
tag_ref_<tag_id> += 1
tag_ref_<tag_id> -= 1
```

它不能表达：

```text
装备 A 提供 Locomotion.Hover
Buff B 也提供 Locomotion.Hover
只移除装备 A 的来源
```

更完整的设计通常需要：

```text
AddTag(sourceId, tag)
RemoveTagsBySource(sourceId)
GetTagSources(tag)
```

#### `AbilityNodeCommitCost` 忽略扣费失败

关键文件：

- `scripts/abilities/ability_nodes/ability_node_commit_cost.gd`
- `scripts/abilities/features/cost_feature.gd`

当前节点调用 `cost_feature.try_pay(...)` 后仍返回 `Status.SUCCESS`。如果激活检查后资源变化，或者扣费组件缺失，技能可能继续产生效果。资源扣费应事务式提交，失败时应中断后续结算并返回结构化错误。

#### Attribute 实现存在 API 瑕疵

关键文件：

- `scripts/components/gameplay_attribute_component.gd`
- `scripts/attributes/gameplay_attribute_instance.gd`

问题包括：

- `GameplayAttributeComponent.get_modifiers()` 调用 `attr.get_modifiers()`，但实例类里方法名是 `get_modifers()`。
- `_set_base_value()` 先赋值再发 `base_value_changed.emit(base_value, val)`，old/new 会不准确。
- `remove_modifier()` 没同步清理 `_modifiers_by_source_id`，来源索引可能脏。

Attribute 的来源索引和重算规则需要纳入单元测试。

#### Status 应用顺序有副作用风险

关键文件：`scripts/status/gameplay_status_instance.gd`

真实顺序：

```text
apply_effects
-> feature.apply_feature
-> duration == 0 时提前结束
-> duration_policy.initialize
-> remove_mutually_exclusive_statuses
-> apply_status_tags
-> execute_status_cue
```

风险：

- 互斥状态是在新状态效果执行后才移除。
- 瞬时状态不会应用 Tag 和 Cue。
- 堆叠不一定重跑 Feature。
- `status_stacked` 信号定义了，但主流程未明显发出。

State 系统 应固定顺序：

```text
PreCheck -> RemoveConflicts -> AttachState -> ApplyTags -> ApplyModifiers -> FireOnApplied -> EmitResult
```

#### Projectile / MagicField 是表现载具，不是纯逻辑载具

关键文件：

- `scripts/entites/projectile_base.gd`
- `scripts/entites/magic_field_base.gd`

它们依赖 `Area3D`、碰撞、`_physics_process(delta)`、Timer 和随机数。可以借鉴“载具投递 Payload”的设计概念，但逻辑层不能依赖物理碰撞。这类载具更适合作为表现层或高层效果语义，而不是作为核心结算依赖。

#### Cue 与核心逻辑耦合

关键文件：

- `scripts/effects/gameplay_effect.gd`
- `scripts/status/gameplay_status_instance.gd`
- `scripts/singletons/gameplay_cue_manager.gd`

`GameplayEffect.apply()` 和 `GameplayStatusInstance.apply()` 会直接执行 Cue。逻辑层更适合只产生表现请求数据，真正特效、动画、飘字应在表现层执行。
