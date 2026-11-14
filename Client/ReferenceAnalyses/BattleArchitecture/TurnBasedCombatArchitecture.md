# 回合制战斗流程与职责拆分架构分析

代码库地址：<https://github.com/LiGameAcademy/godot4_turn_based_combat_system>

参考实现使用 Godot 4，但本文关注的是回合制战斗流程、单位注册表、回合队列、规则检查和上下文对象等可跨引擎复用的架构经验，引擎 API 仅作为实现背景。

分析结论：这是一个 Godot 4 教学型回合制战斗系统，整体更接近 JRPG 站位回合战斗，而不是 SRPG 棋盘战斗。从架构分析看，主要可观察点是战斗流程管理器拆分、角色注册表、回合队列、技能效果组合、状态事件触发和上下文对象。它不适合直接照搬，因为核心逻辑与 Godot Node、动画、全局单例和非确定性随机耦合较重。

## A. 整体评价

这个仓库虽然是教学项目，但架构上已经具备“可拓展系统”的基础意识。它不是把战斗逻辑全部塞进一个脚本，而是拆出了战斗流程、状态机、行动队列、角色注册表、胜负规则、技能系统、技能效果和状态系统。这种拆分方向是正确的，适合作为中小型回合制战斗系统的第一版架构参考。

不过，它还不是一个高成熟度的通用战斗框架。它的扩展点存在，但工程边界不够硬，主要问题是逻辑和表现耦合、`SkillSystem` 使用全局单例、配置资源和运行时状态混用、事件与行动限制大量使用自由字符串、部分高级效果代码没有完全闭环。这些问题在教学项目里可以接受，但如果接入需要无头核心、回放、快照、服务端复用的项目，就必须重构。

综合评价：

| 维度 | 评价 |
|---|---|
| 职责拆分 | 方向正确，适合学习和参考 |
| 拓展意识 | 中等偏上，已有管理器、组件、Effect、Status、Context |
| 工程边界 | 偏弱，逻辑、表现、Godot 运行时耦合较重 |
| 数据驱动 | 有雏形，适合映射到外部配表，但不能照搬 Resource 模式 |
| 可测试性 | 一般，全局单例和动画等待会增加测试难度 |
| 回放/快照友好度 | 较弱，运行时状态和非确定性随机需要重构 |
| 对同类项目的适用性 | 适合作为早期阶段流程拆分草图，不适合作为长期底座直接移植 |

一句话：**这个库的设计方向是对的，但工程边界不够硬。可以学它的职责拆分，不能照搬它的运行时实现。**

## B. 项目结构与对象模型

### B.1 代码库定位

`readme.md` 说明该项目目标是从零构建可学习、可扩展的 Godot 回合制战斗系统，覆盖：

- 角色属性
- 技能设计
- 状态效果
- 战斗流程管理
- AI 逻辑
- UI 交互

从代码结构看，它采用“场景节点 + 子管理器 + 组件 + Resource 数据”的 Godot 风格。不能直接复用实现，但职责拆分方式较清晰。

### B.2 目录结构与关键模块

#### B.2.1 顶层入口

- `project.godot`
  - 注册 Autoload：
    - `SkillSystem`
    - `AudioManager`
- `main.gd`
  - 实例化 `scenes/battle/battle_scene.tscn` 并传入 `BattleData`。
- `main.tscn`
  - 挂载示例战斗数据 `resources/battle_data/forest_encounter.tres`。

#### B.2.2 战斗流程层

关键目录：`scripts/core/battle/`

- `battle_manager.gd`
  - 战斗中央协调器。
  - 管理开始战斗、状态切换、玩家行动、敌人 AI、胜负检查、日志和表现回调。
- `battle_state_manager.gd`
  - 战斗状态机。
  - 状态包含 `IDLE`、`START`、`ROUND_START`、`TURN_START`、`PLAYER_TURN`、`ENEMY_TURN`、`TURN_END`、`ROUND_END`、`VICTORY`、`DEFEAT`。
- `turn_order_manager.gd`
  - 回合队列。
  - 每个大回合按角色 `speed` 降序生成行动队列。
- `battle_character_registry_manager.gd`
  - 角色注册表。
  - 管理全部角色、玩家队伍、敌人队伍、存活查询、敌我关系、死亡反注册。
- `combat_rule_manager.gd`
  - 胜负与战斗规则。
- `battle_visual_effects.gd`
  - 伤害数字、治疗、状态等表现反馈。

#### B.2.3 角色组件层

- `scenes/characters/character.gd`
  - 角色场景脚本，组合 Combat、Skill、AI 组件。
  - 同时承担数据访问、动画、移动、伤害数字、死亡表现等职责。
- `scripts/core/character/character_combat_component.gd`
  - 行动执行组件，支持攻击、防御、技能、物品。
- `scripts/core/character/character_skill_component.gd`
  - 属性、技能、状态运行时管理。
- `scripts/core/character/character_ai_component.gd`
  - AI 决策入口。
- `scripts/core/ai/ai_behavior.gd`
  - AI 行为权重资源。

#### B.2.4 技能、状态、属性资源

关键目录：`scripts/resources/`

- `battle_data.gd`
  - 战斗配置，包含玩家、敌人、出生位置、音乐、奖励、特殊条件。
- `character_data.gd`
  - 角色静态数据，包含属性集、技能、普通攻击、防御技能、AI 行为、图标。
- `skill_data.gd`
  - 技能数据，定义技能类型、目标类型、MP 消耗、效果数组、行动类别等。
- `skill_effect.gd`
  - 技能效果基类，处理禁用、目标覆盖、条件、子效果、视觉请求。
- `skill_status_data.gd`
  - 状态数据，包含持续时间、叠加、属性修改器、初始/持续/结束效果、事件触发效果、行动限制。
- `skill_attribute_set.gd`
  - 属性集合，给角色复制独立属性实例。
- `skill_attribute.gd`
  - 单个属性，支持基础值、当前值、加法、百分比、Override。
- `skill_attribute_modifier.gd`
  - 属性修改器。

#### B.2.5 具体效果

关键目录：`scripts/resources/skill_effect_data/`

- `damage_effect.gd`
  - 伤害计算，包含攻击缩放、防御减伤、元素克制、随机浮动。
- `heal_effect.gd`
  - 治疗。
- `apply_status_effect.gd`
  - 应用状态。
- `modifiy_damage_effect.gd`
  - 修改伤害，用于防御、减伤等效果。
- `counter_attack_effect.gd`
  - 反击。
- `multi_strike_effect.gd`
  - 多段攻击。
- `redirect_damage_effect.gd`
  - 伤害转移。
- `dispel_effect.gd`
  - 驱散。

#### B.2.6 数据资源

关键目录：`resources/`

- `resources/battle_data/forest_encounter.tres`
  - 示例战斗。
- `resources/characters_data/*.tres`
  - 玩家和敌人数据。
- `resources/skills/*.tres`
  - 攻击、防御、火球、眩晕等技能。
- `resources/skill_status/*.tres`
  - 眩晕、沉默、反击、燃烧、防御等状态。
- `resources/skill_attributes/*.tres`
  - HP、MP、攻击、防御、速度等属性模板。

#### B.2.7 UI 与场景

- `scenes/battle/battle_scene.gd`
  - 生成角色、连接 UI 信号、转发玩家输入给 `BattleManager`。
- `scenes/ui/battle_ui.gd`
  - 战斗 UI 总入口。
- `scenes/ui/action_menu.gd`
  - 攻击、防御、技能、物品按钮。
- `scenes/ui/skill_select_menu.gd`
  - 技能选择。
- `scenes/ui/target_selection_menu.gd`
  - 目标选择。

### B.3 核心对象模型

#### B.3.1 战斗层

```text
BattleScene
  负责场景、UI、角色实例化和输入转发

BattleManager
  战斗流程总协调器

BattleStateManager
  状态机

TurnOrderManager
  行动队列

BattleCharacterRegistryManager
  单位注册、阵营、存活查询、敌我关系

CombatRuleManager
  胜负规则

BattleVisualEffects
  表现反馈
```

#### B.3.2 角色层

```text
Character
  Godot 节点实体
  同时承载表现、动画、移动、属性访问和组件入口

CharacterCombatComponent
  执行动作

CharacterSkillComponent
  管理属性、技能和状态

CharacterAIComponent
  决策敌人行动
```

#### B.3.3 技能与状态层

```text
SkillSystem
  全局技能执行管线

SkillData
  技能静态配置

SkillEffect
  效果基类

DamageEffect / HealEffect / ApplyStatusEffect ...
  具体效果

SkillStatusData
  状态模板和运行时状态混合体

SkillAttributeSet / SkillAttribute / SkillAttributeModifier
  属性与修正器
```

## C. 运行时调用流程

### C.1 战斗初始化流程

锚点：

- `main.gd`
- `scenes/battle/battle_scene.gd`
- `scripts/core/battle/battle_manager.gd`
- `scripts/core/battle/battle_character_registry_manager.gd`

流程：

```text
main.gd
-> 实例化 BattleScene
-> 传入 BattleData
-> BattleScene.initialize_battle(battle_data)
-> 创建玩家角色和敌人角色
-> battle_manager.add_character(character, is_player)
-> BattleCharacterRegistryManager.register_character()
-> BattleManager._on_character_registered()
-> character.initialize(battle_manager, cast_marker)
-> CharacterSkillComponent 初始化属性、技能、状态
-> CharacterCombatComponent 注入普通攻击和防御技能
-> BattleManager.start_battle()
-> BattleStateManager.START
```

设计意图：战斗场景负责生成和输入，`BattleManager` 负责流程，角色注册表负责队伍关系。

### C.2 回合推进流程

锚点：

- `battle_manager.gd`
- `battle_state_manager.gd`
- `turn_order_manager.gd`

状态流：

```text
START
-> ROUND_START
   -> CombatRuleManager.add_turn_count()
   -> 检查胜负
   -> TurnOrderManager.build_queue()
      按 speed 降序生成队列
-> TURN_START
   -> TurnOrderManager.get_next_character()
   -> turn_changed(character)
-> BattleManager._on_turn_changed(character)
   -> character.on_turn_start()
   -> 如果不能行动，跳过
   -> 玩家进入 PLAYER_TURN
   -> 敌人进入 ENEMY_TURN
-> PLAYER_TURN
   -> 等待 UI 输入
-> ENEMY_TURN
   -> execute_enemy_ai()
-> TURN_END
   -> 检查胜负
   -> current_turn_character.on_turn_end()
   -> 队列有下一个角色则 TURN_START
   -> 否则 ROUND_END
-> ROUND_END
   -> ROUND_START
```

架构启发：

- `TurnCoordinator` 应只管当前行动单位、跳过死亡单位、阵营切换、行动完成。
- 胜负判断可以拆给 `BattleRuleChecker`。
- 单位列表、阵营和存活查询可以拆给 `CombatUnitRegistry`。

### C.3 玩家行动流程

锚点：

- `scenes/battle/battle_scene.gd`
- `scenes/ui/battle_ui.gd`
- `scripts/core/battle/battle_manager.gd`
- `scripts/core/character/character_combat_component.gd`
- `scripts/autoload/skill_system.gd`

流程：

```text
玩家点击 UI
-> BattleUI 发信号
-> BattleScene 转发
-> BattleManager.player_select_action(action_type, target, params)
-> params 注入 SkillExecutionContext
-> current_turn_character.execute_action()
-> CharacterCombatComponent.execute_action()
   -> 检查行动限制
   -> 检查技能 MP
   -> 分派 Attack / Defend / Skill / Item
-> SkillSystem.attempt_execute_skill()
-> 行动结束后 BattleManager 检查胜负
-> 进入 TURN_END
```

优点：UI、战斗流程、角色行动有基本分层。

问题：行动执行中直接 `await` 动画和移动，这对纯逻辑核心不适合。

### C.4 技能执行数据流

锚点：

- `scripts/autoload/skill_system.gd`
- `scripts/resources/skill_data.gd`
- `scripts/resources/skill_effect.gd`
- `scripts/resources/skill_effect_data/damage_effect.gd`
- `scripts/resources/skill_effect_data/apply_status_effect.gd`

流程：

```text
SkillSystem.attempt_execute_skill(skill_data, caster, selected_targets, context)
-> _determine_execution_targets()
-> _validate_skill_usability()
   -> MP 检查
   -> 目标检查
-> _consume_skill_resources()
-> _process_melee_skill() 或 _process_ranged_skill()
   -> 移动到目标或施法点
   -> 播放动画
   -> 等待前摇
   -> _process_skill_effects_async()
-> SkillEffect.process_effect()
   -> 检查 disable
   -> 解析目标覆盖
   -> 检查 conditions
   -> 调用子类 _process_effect()
   -> 执行 sub_effects
```

架构观察：

- 技能由多个 Effect 组成是可取的。
- 上下文对象承载本次执行的临时数据是可取的。
- 但动画、等待和表现必须移出核心逻辑。

### C.5 伤害与状态事件流

锚点：

- `scripts/core/character/character_combat_component.gd`
- `scripts/core/damage_info.gd`
- `scripts/contexts/damage_event_context.gd`
- `scripts/resources/skill_status_data.gd`

流程：

```text
DamageEffect 计算基础伤害
-> Character.take_damage()
-> CharacterCombatComponent.take_damage()
-> 创建 DamageInfo
-> SkillSystem.trigger_game_event(target, "on_damage_taken", damage_event_context)
-> 目标身上的状态匹配 trigger_on_events
-> 触发状态的 trigger_effects
   例如防御状态修改 DamageInfo.final_damage
-> 实际扣 HP
-> 属性变更信号检查死亡
-> 角色死亡后注册表反注册
```

有价值的点：

- `DamageInfo` 作为可被状态修改的中间对象。
- 状态通过事件触发效果。
- 伤害前后可形成明确时序。

可抽象为：

```text
AttackContext
DamageContext
Timing.Hit.Before
Timing.Hit.After
Timing.Battle.Before
Timing.Battle.After
```

## D. 架构评价

### D.1 它解决的问题

#### D.1.1 BattleManager 不再一个类全包

拆出：

- 状态机
- 回合队列
- 角色注册表
- 规则判断
- 表现效果

这对早期战斗流程拆分有参考价值。

#### D.1.2 角色、技能、状态数据资源化

角色、技能、状态和属性都能在 `.tres` 中配置。体现出数据驱动方向。

#### D.1.3 SkillEffect 统一技能效果

一个技能可以组合多个效果。比如火球可以伤害并附加燃烧。这符合复杂技能由 Effect 片段组合的方向。

#### D.1.4 状态事件触发 Buff/Debuff

防御、反击等效果通过状态监听事件触发，而不是全写死在伤害逻辑中。

#### D.1.5 上下文对象避免全局变量扩散

`SkillExecutionContext`、`EventContext`、`DamageEventContext`、`DamageInfo` 承载技能和伤害流程数据。

### D.2 优点

#### D.2.1 流程拆分适合早期项目

`BattleManager + BattleStateManager + TurnOrderManager + Registry + RuleManager` 是很适合教学和原型的拆法。

#### D.2.2 注册表模式很适合早期多单位战斗

`BattleCharacterRegistryManager` 提供：

- 获取玩家队伍
- 获取敌人队伍
- 获取存活角色
- 判断敌我
- 判断队伍失败
- 死亡后反注册

该职责边界清晰。

#### D.2.3 回合队列独立

`TurnOrderManager` 和状态机分离，便于后续插队、速度排序、再行动。

当前实现的队列独立性便于后续扩展行动顺序。

#### D.2.4 SkillEffect 组合能力强

体现出主动技能效果组合的扩展方向。

#### D.2.5 DamageInfo 思路正确

伤害不是一个立即扣血的数字，而是一个可被减伤、反击、护盾、转移、状态修改的上下文。

### D.3 缺点与潜在坑

#### D.3.1 逻辑与表现耦合重

核心流程中直接调用：

- `move_to_target()`
- `move_to_cast_marker()`
- `play_animation()`
- `create_timer()`
- 伤害数字
- 视觉效果方法

纯逻辑战斗核心必须避免直接等待或播放表现。

#### D.3.2 全局 SkillSystem 不适合同类无头核心

`SkillSystem` 是 Autoload 单例，并保存 `battle_manager` 引用。这会影响：

- 多战斗实例
- 单元测试
- 服务端复用
- 回放与续战

应由局部战斗实例持有技能或行动解析服务。

#### D.3.3 随机数不确定

静态阅读可见多处使用 Godot 随机，例如伤害浮动、概率状态、AI 权重随机。PVP、回放、续战要求确定性，应使用 `BattleRNG`。

#### D.3.4 静态数据和运行时状态混在 `SkillStatusData`

`SkillStatusData` 既有配置：

- `status_id`
- `duration`
- `max_stacks`
- `attribute_modifiers`
- `trigger_effects`

也有运行时字段：

- `source_character`
- `target_character`
- `remaining_duration`
- `stacks`
- `current_turn_trigger_count`
- `current_total_trigger_count`

必须拆开：

```text
StateDefinition
StateRuntime
```

否则快照、悔棋、回放、服务端序列化会变复杂。

#### D.3.5 事件范围较窄

`SkillSystem.trigger_game_event()` 主要扫描事件来源身上的状态，适合“自己受伤触发自己的状态”，但不够支持：

- 战场级监听
- 队友监听
- 光环
- 地形触发
- 警戒反击范围
- 全局统计

后续应区分战场级、单位级、行动上下文级事件范围。

#### D.3.6 行动限制字符串较自由

`restricted_action_categories` 和 `SkillData.action_categories` 使用自由字符串，如：

- `any_action`
- `magic_skill`
- `attack`
- `defend`

方向正确，但容易拼错。应使用集中注册的 `GameplayTag` 或强类型常量。

#### D.3.7 实现存在教学项目残留

静态阅读发现若干疑点：

- `DamageInfo` 构造参数和调用参数可能错位，近战标记可能传到暴击字段。
- `SkillEffect.process_effect()` 条件上下文用的是 `targets`，部分条件可能读取 `target`。
- 部分高级效果引用的方法在当前 `BattleManager` 或 `Character` 中未明显存在，像是未来设计残留。

这说明它适合作为架构参考，不应作为生产代码模板。

## E. 深度复查：资深战斗程序视角

本节以真实项目落地角度补充运行时流程、类组职责、UI / AI / 技能交互，以及前文未展开的代码级风险。

### E.1 实际运行调用流程

#### 项目入口与战斗初始化

关键文件：

- `project.godot`
- `main.gd`
- `main.tscn`
- `scenes/battle/battle_scene.gd`
- `scripts/resources/battle_data.gd`

```text
project.godot
-> Autoload: SkillSystem, AudioManager
-> run/main_scene = main.tscn

Main._ready()
-> instantiate("scenes/battle/battle_scene.tscn")
-> BattleScene.initialize_battle(battle_data)

BattleScene.initialize_battle()
-> 清空 PlayerArea / EnemyArea
-> 根据 BattleData.player_data_list 生成玩家 Character
-> 根据 BattleData.enemy_data_list 生成敌人 Character
-> BattleManager.add_character(character, is_player)
-> 播放 battle_music
-> 连接角色点击信号
-> BattleManager.start_battle()
```

补充判断：`BattleScene` 实际承担了场景生成、UI 信号、音乐、输入、启动战斗等多种职责。它对 Godot 教学项目很自然，但 类似临时入口不应长期承担同样的职责。

#### 管理器初始化

关键文件：`scripts/core/battle/battle_manager.gd`

```text
BattleManager._ready()
-> BattleCharacterRegistryManager.initialize()
-> CombatRuleManager.initialize(registry)
-> TurnOrderManager.initialize(registry)
-> 连接 registry / rule / turn / state 信号
-> SkillSystem.battle_manager = self
-> BattleStateManager.initialize(IDLE)
```

`SkillSystem.battle_manager = self` 是全局单例反向持有当前战斗。单战斗教学项目可以用，但同类无头核心不应采用，因为它会破坏多战斗实例、测试隔离、回放和服务端复用。

#### 角色注册与组件初始化

关键文件：

- `scripts/core/battle/battle_character_registry_manager.gd`
- `scripts/core/battle/battle_manager.gd`
- `scenes/characters/character.gd`
- `scripts/core/character/character_skill_component.gd`
- `scripts/core/character/character_combat_component.gd`

```text
BattleManager.add_character()
-> BattleCharacterRegistryManager.register_character()
   -> 加入 _all_characters
   -> 加入 _player_team 或 _enemy_team
   -> 连接 character.character_defeated
   -> emit character_registered

BattleManager._on_character_registered(character)
-> Character.initialize(battle_manager, cast_marker)
   -> Character._initialize_from_data()
      -> CharacterSkillComponent.initialize(attribute_set_resource, skills)
   -> Character._init_components()
      -> CharacterCombatComponent.initialize(element, attack_skill, defense_skill)
      -> CharacterAIComponent.initialize(battle_manager)
```

值得肯定的点：`CharacterSkillComponent.initialize()` 会 `duplicate(true)` 属性集模板，然后 `SkillAttributeSet.initialize_set()` 再复制每个 `SkillAttribute`。这是“配置模板”和“战斗运行时实例”分离的正确方向。

不足：`SkillStatusData` 后续又把配置和运行时状态混在一起，所以这个边界没有贯彻到底。

#### 回合推进

关键文件：

- `scripts/core/battle/battle_manager.gd`
- `scripts/core/battle/battle_state_manager.gd`
- `scripts/core/battle/turn_order_manager.gd`

```text
BattleManager.start_battle()
-> BattleStateManager.change_state(START)

BattleManager._on_state_changed(START)
-> change_state(ROUND_START)

ROUND_START
-> CombatRuleManager.add_turn_count()
-> CombatRuleManager.check_battle_end_conditions()
-> TurnOrderManager.build_queue()
   -> 收集存活玩家和敌人
   -> 按 speed 降序排序
-> change_state(TURN_START)

TURN_START
-> TurnOrderManager.get_next_character()
   -> pop_front()
   -> emit turn_changed(character)

BattleManager._on_turn_changed(character)
-> character.on_turn_start()
-> 如果 character.can_action == false，change_state(TURN_END)
-> 玩家单位进入 PLAYER_TURN
-> 敌方单位进入 ENEMY_TURN
```

重要判断：`BattleStateManager` 本体只存状态和发信号，真正状态转移表写在 `BattleManager._on_state_changed()`。同时 `change_state()` 发出的信号是同步调用，回调里又继续 `change_state()`，会形成嵌套推进。这种递归式状态推进不宜照搬，更适合显式推进并返回结构化结果。

#### 玩家行动流程

关键文件：

- `scenes/battle/battle_scene.gd`
- `scripts/core/battle/battle_manager.gd`
- `scenes/characters/character.gd`
- `scripts/core/character/character_combat_component.gd`
- `scripts/autoload/skill_system.gd`

```text
BattleScene UI 回调
-> BattleManager.player_select_action(action_type, target, params)
-> 检查是否 PLAYER_TURN
-> params 合并 SkillExecutionContext.new(self)
-> current_turn_character.execute_action()
-> Character.execute_action()
-> CharacterCombatComponent.execute_action()
   -> can_perform_action()
   -> 分派 ATTACK / DEFEND / SKILL / ITEM
   -> _execute_attack() / _execute_defend() / _execute_skill()
   -> SkillSystem.attempt_execute_skill()
   -> emit action_executed
```

具体风险：`CharacterCombatComponent._execute_skill()` 会先调用 `move_to_target()` 或 `move_to_cast_marker()`，而 `SkillSystem._process_melee_skill()` / `_process_ranged_skill()` 里又再次移动一次。这是逻辑和演出混合后出现的重复编排风险。应把逻辑结算和表现播放彻底拆开。

#### 敌方 AI 行动

关键文件：

- `scripts/core/battle/battle_manager.gd`
- `scripts/core/character/character_ai_component.gd`
- `scripts/core/ai/ai_behavior.gd`

```text
BattleManager._on_state_changed(ENEMY_TURN)
-> await timer
-> BattleManager.execute_enemy_ai()
-> CharacterAIComponent.execute_action()
-> decide_action()
-> combat_component.execute_action(action_type, target, params)
-> 返回 AIActionResult
```

AI 决策和行动执行没有分层成“决策”和“提交”，会降低可测试性和可复现性。

这样 AI 可以离线测试，也不会直接耦合表现和节点。

#### 伤害、状态、触发事件

关键文件：

- `scripts/resources/skill_effect_data/damage_effect.gd`
- `scripts/core/character/character_combat_component.gd`
- `scripts/core/damage_info.gd`
- `scripts/contexts/damage_event_context.gd`
- `scripts/autoload/skill_system.gd`

```text
DamageEffect._process_effect()
-> _calculate_damage()
-> target.take_damage()

Character.take_damage()
-> CharacterCombatComponent.take_damage()

CharacterCombatComponent.take_damage()
-> DamageInfo.new(...)
-> DamageEventContext.new(...)
-> SkillSystem.trigger_game_event(target, "on_damage_taken", damage_event_context)
-> 读取 DamageInfo.final_damage
-> 播放 hit 动画
-> CharacterSkillComponent.consume_hp()
-> SkillSystem.trigger_game_event(target, "on_damage_taken_completed", damage_event_context)
```

`DamageInfo` 的设计方向非常值得参考，它让伤害可以在应用前被状态修改。但当前实现有参数错位风险，见 15.4。

### E.2 类组职责表

| 类 / 类组 | 文件路径 | 实际职责 | 架构启发 |
|---|---|---|---|
| `Main` | `main.gd` | 加载 `BattleScene` 并传入 `BattleData` | 入口层承担加载和传入战斗数据职责 |
| `BattleScene` | `scenes/battle/battle_scene.gd` | 生成角色、连接 UI、转发输入、播放音乐 | 承担场景与输入编排职责，不适合放入纯逻辑核心 |
| `BattleManager` | `scripts/core/battle/battle_manager.gd` | 战斗总协调、状态响应、玩家行动、敌方 AI、日志、视觉转发 | 当前职责偏重，可继续拆分为战斗上下文、回合协调、规则检查与结果输出 |
| `BattleStateManager` | `scripts/core/battle/battle_state_manager.gd` | 只保存当前状态并发 `state_changed` 信号 | 状态转移不应长期散落在外部回调里 |
| `TurnOrderManager` | `scripts/core/battle/turn_order_manager.gd` | 按速度构建行动队列，弹出当前行动者 | 早期阶段可先做阵营顺序，后续扩速度队列 |
| `BattleCharacterRegistryManager` | `scripts/core/battle/battle_character_registry_manager.gd` | 全体、玩家、敌人、存活、敌我、死亡反注册 | 非常适合映射为 `CombatUnitRegistry` |
| `CombatRuleManager` | `scripts/core/battle/combat_rule_manager.gd` | 胜负、回合上限、特殊规则入口 | 可映射为 `BattleRuleChecker` |
| `Character` | `scenes/characters/character.gd` | Godot 角色节点，聚合表现、动画、点击、属性快捷访问、组件入口 | 应拆成 `CombatUnit` 和 `CombatUnitVisual` |
| `CharacterCombatComponent` | `scripts/core/character/character_combat_component.gd` | 执行动作、伤害入口、死亡信号、行动限制检查 | 可参考动作入口，不保留动画等待 |
| `CharacterSkillComponent` | `scripts/core/character/character_skill_component.gd` | 属性、技能、状态、行动限制标签 | 职责聚合较多，可继续细分 |
| `CharacterAIComponent` | `scripts/core/character/character_ai_component.gd` | 调用 `AIBehavior` 决策并执行动作 | AI 决策与执行耦合 |
| `AIBehavior` | `scripts/core/ai/ai_behavior.gd` | 权重资源，评估技能和攻击目标 | 后续 Utility AI 可参考，但 RNG 必须确定性 |
| `SkillSystem` | `scripts/autoload/skill_system.gd` | 全局技能管线、资源消耗、目标解析、事件触发 | 全局单例不利于多实例 |
| `SkillData` | `scripts/resources/skill_data.gd` | 技能配置，目标类型、MP、效果数组、行动类别 | 技能数据结构清晰 |
| `SkillEffect` | `scripts/resources/skill_effect.gd` | 效果基类，条件、目标覆盖、子效果、延迟、视觉请求 | Effect 组合思路可取，核心不能含延迟和视觉 |
| `DamageEffect` / `HealEffect` / `ApplyStatusEffect` | `scripts/resources/skill_effect_data/*.gd` | 具体效果实现 | 效果类型边界清晰 |
| `SkillStatusData` | `scripts/resources/skill_status_data.gd` | 状态模板和运行时字段混合，含触发、持续、限制、修正器 | 静态配置和运行时字段混合 |
| `SkillAttributeSet` / `SkillAttribute` / `SkillAttributeModifier` | `scripts/resources/*.gd` | 属性集合、单属性、修正器 | 属性公式结构清晰，但使用浮点 |
| `DamageInfo` | `scripts/core/damage_info.gd` | 可修改伤害上下文 | 可修改伤害上下文的方向清晰 |
| `SkillExecutionContext` / `DamageEventContext` | `scripts/contexts/*.gd` | 行动和事件上下文 | 上下文对象方向清晰 |

### E.3 战斗状态机与 UI / AI / 技能交互

#### 状态机实际形态

`BattleStateManager` 不是完整状态模式，而是状态容器和信号源：

```text
current_state / previous_state
change_state()
emit state_changed(previous, new)
```

实际转移逻辑在 `BattleManager._on_state_changed()` 和 `_on_turn_changed()`。如果照搬，状态测试会变难，因为一次 `change_state()` 可能同步穿过多个状态。显式 API 和结果对象会更利于测试。

#### UI 交互

```text
BattleManager.turn_changed
-> BattleScene._on_turn_changed()
-> show_action_ui(battle_manager.is_player_turn)
-> battle_ui.update_turn_order()
```

玩家点击后：

```text
BattleUI 信号
-> BattleScene 回调
-> BattleManager.player_select_action()
```

UI 直接读取 `battle_manager.current_turn_character` 和 `battle_manager.characters`。UI 直接读取管理器内部集合会增加耦合。

#### AI 交互

AI 输入来自：

- `BattleManager.get_valid_enemy_targets()`
- `BattleManager.get_valid_ally_targets()`
- `Character.current_hp`
- `Character.attack_power`
- `SkillData.effects`
- `AIBehavior.weights`

AI 目前一边决策一边执行。应拆成：

```text
Decide(snapshot) -> Command
Execute(command) -> Result
```

#### 技能与状态交互

```text
SkillData
-> effects: Array[SkillEffect]

SkillEffect
-> conditions
-> target_override
-> sub_effects
-> _process_effect()

SkillStatusData
-> initial_effects
-> ongoing_effects
-> end_effects
-> trigger_on_events
-> trigger_effects
-> restricted_action_categories
```

事件触发：

```text
SkillSystem.trigger_game_event(event_source, event_type, context)
-> event_source.get_skill_component()
-> get_triggerable_status(event_type)
-> status.get_trigger_effects()
-> _process_skill_effects_async()
```

限制：只扫描事件源自己的状态。它可以表达“受击者自己的防御和反击”，但不适合队友援护、地形、光环、警戒范围、战场级规则。更完整的事件系统需要区分单位事件、战场事件、行动上下文事件和空间监听事件。

### E.4 代码级风险和设计缺口

#### 不是 SRPG 棋盘战斗

仓库没有棋盘、格子、移动范围、路径、占格、阻挡、ZOC、LOS。`Character.move_to_target()` 是演出移动，不是规则移动。只能参考流程和技能结构，不能参考移动与空间规则。

#### 确定性不足

非确定性来源包括：

- `DamageEffect._calculate_damage()` 使用 `randf_range()`
- `ApplyStatusEffect._check_if_can_apply_status_by_chance()` 使用 `randf()`
- `ProbabilityCondition.is_met()` 使用 `randf()`
- `CharacterAIComponent.decide_action()` 使用 `randf()`
- `AIBehavior.evaluate_skill()` / `evaluate_attack_target()` 使用 `randf_range()`
- `SkillSystem._get_random_targets()` 使用 `shuffle()`

所有随机应由可注入随机源管理，并记录关键随机结果。

#### `DamageInfo` 参数错位

关键文件：

- `scripts/core/character/character_combat_component.gd`
- `scripts/core/damage_info.gd`
- `scripts/resources/skill_effect_data/counter_attack_effect.gd`

当前调用：

```gdscript
DamageInfo.new(base_damage, source, get_parent(), p_element, is_melee)
```

如果 `DamageInfo._init()` 第 5 参数是 `p_is_crit`，第 7 参数才是 `p_is_melee`，则 `is_melee` 会传错位置。反击效果读取 `context.damage_info.is_melee` 时可能不可靠。

#### 部分高级效果不是闭环实现

`scripts/resources/skill_effect_data/multi_strike_effect.gd` 引用了当前管理器中不明显存在的成员，例如：

- `battle_manager.character_registry`
- `get_all_alive_characters(false)`
- `execute_staged_sub_action()`

这说明仓库中有未来设计残留，不能当生产模板照搬。

#### `SkillEffect.process_effect()` 目标覆盖存在疑点

`SkillEffect.process_effect()` 会计算 `effect_targets`，但循环中仍可能把原始 `target` 传给 `_process_effect(source, target, context)`，而不是 `effect_target`。如果确认如此，`target_override` 对具体效果可能不生效。这类问题反映出教学项目里扩展点存在但稳定性不足。

#### 事件解绑可能不一致

`BattleCharacterRegistryManager.register_character()` 连接的是：

```gdscript
character.character_defeated.connect(_on_character_defeated.bind(character))
```

`unregister_character()` 断开的是：

```gdscript
character.character_defeated.disconnect(_on_character_defeated)
```

绑定 Callable 和未绑定 Callable 可能不是同一连接目标，存在解绑失败风险。事件订阅必须有明确生命周期和可验证清理。

#### 状态持续和回合事件存在双轨残留

`CharacterCombatComponent.on_turn_start()` 调用：

```text
process_active_statuses()
update_status_durations()
```

但 `CharacterSkillComponent` 里还有 `process_turn_start()`，其中会触发 `on_turn_start` 事件。主流程并未明显调用它。Timing 设计应保证每个时序节点只有一个派发入口。
