# 📄 MechStorm (机兵风暴) — SRPG 战棋游戏架构与开发主计划 (V3.0 整合版)

> 适用对象：项目主程 / AI 编码助手 / Scrum Master。

---

## 📑 目录

- [一、项目概述与架构铁律](#一项目概述与架构铁律)
- [二、游戏核心规则 (Game Mechanics Rulebook)](#二游戏核心规则-game-mechanics-rulebook)
- [三、核心系统模块拆解 (Core Modules)](#三核心系统模块拆解-core-modules)
- [数据管理与生命周期决策](#数据管理与生命周期决策)
- [四、技术栈与基建选型 (Infrastructure)](#四技术栈与基建选型-infrastructure)
- [五、开发排期表与里程碑 (Milestones & Sprints)](#五开发排期表与里程碑-milestones--sprints)
- [六、技术探针 (Spikes)](#六技术探针-spikes)
- [七、悔棋方案选型](#七悔棋方案选型-undo-strategy)
- [八、C# 编码与命名规范](#八c-编码与命名规范)
- [九、AI Agent 工作流指令](#九ai-agent-工作流指令)

---

## 一、项目概述与架构铁律

本项目是一款类似《钢岚》的硬核 3D 战棋游戏（SRPG），包含多部位破坏、ZOC 领域、复杂 Buff 排轴、地形高低差与掩体遮挡等机制。
为实现**极致解耦、服务端复用（PVP 预留）及高度可维护性**，确立以下四大架构铁律：

1. **无头核心舱 (Headless Core)**
   - `MechStorm.Battle` 必须是 100% 纯 C#，**绝对禁止**引入 `UnityEngine` 命名空间。
   - 不继承 `MonoBehaviour`，无 `Vector3`，无协程。数学运算使用纯 C# 结构体 `SVector2Int`（地形无高度差，2D 坐标全程够用）。
2. **数据驱动 (Data-Driven)**
   - 彻底抛弃硬编码。所有机兵属性、技能载荷、地形消耗矩阵、天赋树节点，全量由 **Luban 配置表** 驱动。
   - 代码中严禁出现字面量（Magic Numbers）。
3. **确定性与防腐层 (Deterministic & Facade)**
   - 采用确定性随机数（`BattleRNG` 纸带）与指令队列（`CommandQueue`）。
   - 表现层（View）只通过 Interface（如 `ICombatEngine`）与核心层交互，逻辑与表现完全物理隔离。
4. **指令驱动与快照回溯 (Command & Snapshot)**
   - 核心逻辑修改状态必须生成 `Command` 压栈，并生成 `Snapshot` 支持 O(1) 悔棋。
5. **PVP 服务端权威 (Server-Authoritative)**
   - 回合制战棋不采用帧同步。PVP 走**服务端权威**模式：服务端跑核心逻辑（唯一计算真相），客户端只负责提交指令 + 播放表现。
   - 因此无需因 PVP 引入完整定点数库；但战斗核心结算仍优先采用整数化规则，浮点数只用于表现层动画、插值、相机、特效和 UI 过渡。`BattleRNG` 种子由服务端持有，客户端无权生成随机数。
   - 前提：严守"无头核心"，使 `MechStorm.Battle` 能直接搬到 .NET 服务端运行。
6. **P0 即建程序集边界 (Assembly Boundary from Day One)**
   - P0 阶段就建立 `MechStorm.Battle.asmdef`，不引用 `UnityEngine`。结构先对，内容再逐步填充。
   - **禁止**把战斗逻辑混入 `MonoBehaviour`——哪怕原型阶段也不行。P0 的表现层允许脏耦合，但逻辑层边界必须干净。

---

## 二、游戏核心规则 (Game Mechanics Rulebook)

*所有代码生成需基于以下业务边界：*

1. **双层实体模型 (Pilot × Mech → CombatUnit)**
   - 战斗世界由**角色（Pilot）**与**机兵（Mech）**两层实体组成，二者为**多对多**关系。
   - 同一角色可在不同关卡驾驭不同机兵；同一机兵可被不同角色驾驭，上阵不同关卡（角色可驾驭的机兵种类有限）。
   - 战场实际参与单位为 `CombatUnit`，由角色属性 + 机兵属性**合成**而来：
     - **角色提供：** AP / PP 资源、职业、天赋（影响命中、回避、资源循环等）。
     - **机兵提供：** 机体 HP / 装甲、移动力、武器挂载点（MountPoint）、出力。
   - 关卡结构为**多小关卡制**：一个大关卡含多个小关卡，每个小关卡上场角色不同，但机兵可复用。

2. **机型系统 (Mech Class — 轻/中/重)**

   | 机型 | 耐久/防御 | 移动力 | 出力 | 挂载点（MountPoint） | 特性标签 |
   |------|-----------|--------|------|----------|----------|
   | 轻甲 Light | 低 | **最高** | 低 | 左手 + 右手 | `Mech.Class.Light` |
   | 中甲 Medium | 中 | 中 | **最高** | 左手 + 右手 + **双肩** | `Mech.Class.Medium` |
   | 重甲 Heavy | **最高** | 最低 | 中 | 左手 + 右手 | `Mech.Class.Heavy` |

   - **挂载点数量是机型属性（MountPoint）**，从机型配表读取，禁止硬编码"左手+右手"。中甲专属双肩挂载点（`Mech.Slot.Shoulder`）可装导弹/火箭。武器上的元件孔位称为**元件槽（ComponentSlot）**，属于武器本身。
   - **出力（Power）** 约束机兵可装备部件的总量/总重。

3. **双资源系统 (AP & PP)**

   | 资源 | 性质 | 恢复机制 | 用途 |
   |------|------|----------|------|
   | **AP（行动力）** | 每回合循环 | 每回合回复，回复量 = 基础 + 潜能天赋 + 天赋树词条 | 移动、攻击、技能消耗 |
   | **PP（潜能点）** | 整场出战固定额度 | 不自然恢复，整局有限 | 消耗 1 点 PP → 恢复 X 点 AP（爆发型操作）|

   - **AP / PP 归属角色（Pilot），不归属机兵。**
   - **移动不消耗 AP**，使用技能/武器消耗对应 AP，允许一回合内多次攻击（只要 AP 足够）。
   - AP（ActionPoint）每回合回复量是**计算属性**（`GameplayAttribute` 拦截器公式用例），非常数。
   - PP（PotentialPoint）当前为整场固定额度，未来天赋树可能引入回复词条，与 AP 走同一套恢复管线。

4. **战场资源体系 (BattleResource System)**
   - 战斗内所有行动消耗资源统一建模为 `GameplayAttribute`，以 `BattleResource.*` Tag 标识，区别于局外的抽卡/体力等资源。
   - 公共资源（全角色共有）：`BattleResource.ActionPoint`（AP）、`BattleResource.PotentialPoint`（PP）。
   - **角色专属资源：** 部分角色拥有自己独有的消耗资源（如蓄力层数），装配时动态注入对应 `GameplayAttribute`，代码结构零修改。
   - 技能消耗描述为资源列表 `SkillCost[]`，校验时遍历列表检查 `CurrentValue >= amount`，消耗时执行 `ApplyModifier`。UI 显示用缩写（AP/PP），代码内用全称。

5. **角色技能与天赋体系 (Pilot Talents)**
   - **潜能天赋（Potential）：** 角色固有特性，影响 AP 基础循环量等底层属性。
   - **职业天赋（神经驱动树 / Neural Drive Tree）：** 局外加点的天赋树，点亮词条改变 AP 循环量、解锁技能效果等。
   - 两类天赋均通过 `TalentBridgeFactory` 把局外配置转换为局内 Infinite Modifier 注入。

5. **多部位破坏机制 (Part-Break System)**
   - **躯干 (Torso)：** HP 归零则机兵死亡。
   - **左/右臂 (Arms)：** 对应单手/双手武器。HP 归零触发 `[State.Broken.Arm]`，导致无法使用对应武器或命中率大跌；溢出伤害按比例转移至躯干。
   - **腿部 (Legs)：** HP 归零触发 `[State.Broken.Leg]`，移动力强制降为 1，丧失 `Hover`/`Jump` 标签，退化为基础步兵移动逻辑。
6. **装备系统 (Equipment System)**
   - **武器（Weapon）：** 按机型槽位装备（左手/右手/双肩），武器决定攻击力、射程、AP 消耗、命中率。`State.Broken.Arm` 触发后对应武器禁用。
   - **背包（Backpack）：** 背包提供被动词条（如稳动力+1、飞行能力解锁等），装备时注入 Infinite Modifier，卸下时移除。词条种类由配表驱动。
   - **元件系统（Component Slots）：** 武器上有 3~4 个元件槽，可放入**触元件**和**应元件**。触元件监听战斗事件（如"命中敌方时"），满足条件后判定应元件是否触发（如概率附加 Buff、额外伤害）。两者构成条件链，是装备深度的核心。
7. **机兵养成与模组系统 (Mech Cultivation & Module System)**
   - **品阶（Tier）：** 每个部件（躯干/左右手/足）有独立品阶：金一 → 金二 → 金三 → 彩。晋升需要相同部件合并或特定材料。
   - **三套固有模组（每套机兵固定持有）：**
     - **四级模组（4-Star Module）：** 每级提供有条件增伤，满级上限与品阶金一绑定。来源：拆解同套机兵部件。
     - **八级模组（8-Star Module）：** 每级提供有条件增伤，满级（第8级）解锁额外词条（如 Buff 回合+1、技能倍率+1）。解锁满级条件：整套机兵全部达到彩品阶。来源：商店特卖。
     - **通用小模组（Generic Module）：** 固定类型词条（命中率+10%、暴击伤害+10% 等），每个 2 级，插两个叠加为 4 级。来源：商店购买。
   - **固有模组词条数量由配表决定，禁止硬编码。** 普通机兵通常 3 条，联动/限定机兵最多 7 条（多出的 4 条为小模组级别）。
   - **模组插槽数量同样由配表决定**，当前通常为 4 个，未来可扩展至 6/8 个，零改代码。
   - **特殊词条类型：**
     - 标签型（低空飞行、无视障碍穿行）→ 装配时注入 `Locomotion.*` Tag，与背包词条同路径
     - 插槽增幅型（指定插槽效果翻倍：等级 1 → 2）→ 对目标 Modifier 的 Multiply 层加成
   - **词条效果架构覆盖范围：** 所有词条效果均归入三类：①修改数值（`GameplayAttribute` Add/Multiply 层）、②注入/移除状态（`GameplayTag`）、③事件触发（`EventBus` 订阅 + `GameplayModifier` 钩子）。修改游戏流程本身（如跳过回合、改变回合顺序）的词条需单独在状态机层面处理。
   - 模组词条本质是**局外静态 Modifier**，装配时由 `MechFactory` 检查品阶与模组等级，决定注入哪些 Modifier。
   - **原型阶段处理：** P0~P2 战斗原型中所有机兵默认彩品阶、模组满级，以硬编码数值注入，不实现养成流程。
9. **战斗持久化与录像 (Battle Persistence & Replay)**
   - **续战存档：** PVE 战斗中途强制退出（包括杀进程），重启后可恢复到退出前的战斗状态。实现原理：每次行动提交后将战场快照 + RNG 游标序列化写入本地磁盘；启动时检测未完成存档并加载恢复，复用 `MechSnapshot` 机制，仅切换存储介质。
   - **战斗录像：** 战斗结束后可查看回放，并可分享至公共聊天频道。回放分两类：本地调试/本地复盘使用 Command Replay（初始状态 + Command 序列 + RNG 种子/游标，重新执行序列）；PVP、排行榜、防作弊或分享回放使用 Result Replay 或混合日志（Command + 服务端权威 Result），观看端按权威结果播放表现，不依赖本地重新结算。
10. **攻击方式分类 (Attack Mode Classification)**
   - 所有攻击按两个维度交叉形成四种类型，决定哪些模组/元件词条生效：

   | | 对战（Duel） | 非对战（Non-Duel） |
   |---|---|---|
   | **主动（Active）** | 普攻、武器技能、追击连击 | 战术武器、范围攻击、指令攻击 |
   | **非主动（Reactive）** | 协力、警戒、反击、反应攻击 | 战术协力、战术警戒、指令预设攻击、额外打击 |

   - **攻击方式用 `GameplayTag` 表示，不用枚举**（`Attack.Mode.Active`、`Attack.Mode.Duel` 等），未来新增类型只加配表，代码零修改。
   - 每次攻击的 `AttackContext` 携带 Tag 集合（而非单个枚举值），元件/模组词条的触发条件匹配 Tag 集合中的子集。

11. **时序锚点 (Timing Anchor)**
   - 词条/元件在战斗流程特定阶段生效，对应五步结算管线的时序节点：

   | Tag | 时机 |
   |-----|------|
   | `Timing.Battle.Before` | 战前（合法性校验后、伤害计算前） |
   | `Timing.Battle.During` | 战中（RNG 计算、伤害应用阶段） |
   | `Timing.Battle.After` | 战后（伤害结算完成后） |
   | `Timing.Turn.Start` | 回合开始 |
   | `Timing.Turn.End` | 回合结束 |
   | `Timing.Action.Before` | 行动前 |
   | `Timing.Hit.Before` / `Timing.Hit.After` | 受击前 / 受击后 |

   - 触元件同时携带时序 Tag + 攻击方式 Tag，EventBus 在每个时序节点派发事件，两者均满足时触发。
   - 新增时序节点只需在配表加一行，零改代码。

12. **追加攻击系统 (Follow-up Attack System)**
   - 追加攻击是由事件（攻击结束、命中、部位破坏等）触发的嵌套攻击 Command，走完整五步结算管线。
   - **追加攻击分两类，区别在于 `AttackContext` 携带的攻击方式 Tag：**

   | 类型 | 攻击方式 Tag | 元件判定 | 增伤 Buff | 说明 |
   |------|------------|----------|-----------|------|
   | **追击（Chase）** | `Attack.Mode.Active` + `Attack.Mode.Duel` | ✅ 能触发 | ✅ 享受主动对战增伤 | 等同一次低倍率主动对战攻击 |
   | **额外打击等** | `Attack.Mode.ExtraStrike`（独立 Tag） | ❌ 不触发 | ❌ 不享受 | 元件条件不匹配，自然跳过 |

   - 不需要额外的布尔开关，攻击方式 Tag 本身即为过滤器。
   - 为防止追加攻击无限嵌套，`AttackContext` 携带 `FollowUpDepth` 深度计数，超过上限（通常 1~2）不再触发。

13. **行动配额系统 (Action Quota System)**
   - 部分词条触发后授予单位额外的行动配额，抽象为 `ActionQuota` 数据注入 `CombatUnit`：

   | 词条 | MoveRange | CanAttack | 说明 |
   |------|-----------|-----------|------|
   | **再行动** | -1（完整移动力） | ✅ | 完整额外回合 |
   | **再攻击** | 2 格 | ✅ | 限制版额外行动 |
   | **再移动** | 2 格 | ❌ | 仅移动配额 |

   - 词条触发时（`Timing.Battle.After`）由 `GameplayModifier` 注入 `ActionQuota`，回合状态机在玩家回合结束前检查是否有未消费的配额；有则允许继续行动。
   - 新词条只需配不同参数值，代码零修改。优先级：**P2**。

14. **词条效果设计总纲 (Effect Design Framework)**
   - 游戏中所有词条效果归入三套体系，互相独立，组合覆盖绝大多数需求：

   | 效果类型 | 实现方式 | 例子 |
   |----------|----------|------|
   | **禁止某行为** | `Forbidden.*` Tag | `Forbidden.ActionQuota`（禁止再行动）、`Forbidden.Attack`、`Forbidden.Move` |
   | **修改数值** | `GameplayAttribute` Modifier（Add/Multiply） | 攻击力降低、AP上限增加、移动力减少 |
   | **赋予/剥夺能力** | `Locomotion.*` / `State.*` Tag 注入/移除 | 获得飞行、失去跳跃、被眩晕 |

   - 行为执行前检查对应 `Forbidden.*` Tag；数值读取时走拦截器公式自动叠算；能力 Tag 由寻路/技能校验前置检查。
   - 新词条出现时只需确定归属哪套体系，配表填参数，代码零修改。
   - **范围压制（Aura Suppression）**：光环 Modifier 进入/离开时动态注入/移除 `Forbidden.*` Tag，不修改原始数据，悔棋时 Tag 状态随快照恢复，配额数据不丢失。

15. **ZOC 与掩体系统 (Zone of Control & Cover)**
   - 敌方机兵周围产生 ZOC，进入该区域额外消耗移动力。
   - 射击线（LOS）若经过掩体遮挡，计算命中率惩罚。
   - **地形高度差不影响伤害公式。** 高地/低地仅影响 LOS 射程（高地扩展视野与射程）和移动合法性（特定地形需要对应 `Locomotion` Tag 才能通过）。

---

## 三、核心系统模块拆解 (Core Modules)

> 每个模块标注所属优先级层，指导实现顺序。

### 3.0 实体装配与资源系统 (Pilot / Mech / CombatUnit)

| 子模块 | 优先级 | 说明 |
|--------|--------|------|
| `PilotData` / `MechData` / `CombatUnit` 壳结构 | **P0** | 字段归属要对（AP 挂 Pilot，HP 挂 Mech），内容可硬编码 |
| `CombatUnit` 属性合成（角色 + 机兵） | **P1** | 部位破坏与 AP 系统依赖合成结果 |
| AP 资源（固定回复量） | **P1** | 战斗循环必需 |
| 机型系统（轻/中/重 + 动态挂载点） | **P1** | 挂载点数由机型决定，配合武器数据接入 |
| PP 资源 + PP→AP 转换指令 | **P2** | 资源策略层，核心战斗稳定后加 |
| 职业系统（职业枚举 + 基础属性差异） | **P2** | 配合 Luban 配表 |
| `MechFactory` / 多对多装配桥接 | **P2** | 角色 + 机兵 + 装备 → `CombatUnit` |
| 潜能天赋 / 神经驱动树（→ Infinite Modifier） | **P3** | 依赖完整 Modifier + 局外养成 |
| BattleResource 公共资源（ActionPoint/PotentialPoint） | **P1** | 以 `BattleResource.*` Tag 注入，简版无拦截器公式 |
| BattleResource 角色专属资源（如 HeatStack） | **P2** | 装配时动态注入，技能 `SkillCost[]` 校验管线 |

### 3.1 实体与状态管线（轻量级 GAS 变种）

| 子模块 | 优先级 | 说明 |
|--------|--------|------|
| `MechEntity` 单血量版 | **P0** | 躯干单 HP，够用即可 |
| 多部位血量 + 溢出转移 | **P1** | 前线任务灵魂机制，P0 跑通后立即追加 |
| `GameplayTags` 简版（哈希集合） | **P1** | 管理 `State.Broken.*`、`Locomotion.*` |
| `GameplayAttribute` 拦截器公式 | **P2** | `(Base+Add)×(1+Multiply)`，重构阶段实现；**预留脏标记接口**（`_isDirty`），P3 接 Utility AI 时实现两层缓存 |
| `GameplayAttribute` 两层缓存 | **P3** | 局外静态层（装配时算一次存 `BaseComputedValue`）+ 局内动态层（脏标记按需重算）；AI 评分大量读属性时避免全量重算 |
| `GameplayModifier` 完整系统 | **P2** | Duration / Stacks / 事件钩子，重构阶段实现 |

### 3.2 空间雷达与网格系统 (Spatial & Grid)

| 子模块 | 优先级 | 说明 |
|--------|--------|------|
| 简单方格 + BFS 移动范围 | **P0** | 等代价，不做 Cost Matrix |
| A* + 地形消耗矩阵（Cost Matrix） | **P1** | 替换 BFS，支持地形差异 |
| 地形类型 + `Locomotion` 合法性校验 | **P1** | 特定地形需对应 Tag 才能通过（如悬浮机甲飞越深坑） |
| ZOC（移动 Cost 增量） | **P1** | 软阻挡实现 |
| LOS + 掩体射击惩罚 | **P1** | 射击线经过掩体时命中率惩罚；高地扩展射程但不加伤害 |
| `IMapTopology` 拓扑接口隔离 | **P1** 末 | 在空间系统稳定后再做抽象 |
| `SpatialManager` 三层空间索引 | **P2** | 重构阶段分离三层 |

### 3.3 战斗管线与时序流转 (Combat Pipeline)

| 子模块 | 优先级 | 说明 |
|--------|--------|------|
| 基础攻击结算（固定伤害） | **P0** | 相邻攻击，无 RNG；公式：`伤害 = 攻击力（固定值）` |
| 伤害公式简版 | **P1** | `伤害 = 技能倍率 × 攻击力 × 命中判定`，支持部位破坏流程即可 |
| 伤害公式完整版 | **P2+** | 防御/装甲减伤、暴击、攻击方式加成、模组/元件增伤叠加、条件增伤优先级——公式细节在战斗流程跑通后再定，越晚定越准确 |
| AP 系统（每回合限次攻击） | **P1** | 技能/攻击消耗 AP |
| `BattleRNG` 确定性随机 + 命中率 | **P1** | 引入概率后才需要确定性 RNG |
| 五步结算管线（`BattleResolver`） | **P1** | 合法性校验 → 战前Hook → RNG → 伤害/断肢 → 战后Hook |
| `EventBus` 事件总线 | **P2** | 直接使用 **TEngine `EventDispatcher`**（非全局单例，可任意实例化）；战场级一个实例，每个 `CombatUnit` 持有一个实例，实体事件不广播到全局，天然避免无效派发；`GameEventMgr` 绑定单位生命周期，死亡时 `Clear()` 自动注销所有订阅 |
| `ICommand` 指令封装 + `CommandStack` | **P2** | 悔棋前提 |
| `MechSnapshot` 快照回溯 | **P2** | O(1) 悔棋 |
| `BattleDirector` 异步导演 | **P2** | UniTask/DOTween 动画时序 |
| 追加攻击系统（追击 / 额外打击） | **P2** | 依赖 `EventBus` + `Modifier`；`AttackContext` 携带 `FollowUpDepth` 防无限嵌套 |
| 行动配额系统（`ActionQuota`） | **P2** | 再行动/再攻击/再移动词条统一抽象为配额注入；状态机检查未消费配额决定是否继续行动 |

### 3.4 装备系统 (Equipment System)

| 子模块 | 优先级 | 说明 |
|--------|--------|------|
| 武器基础数据（攻击力/射程/AP消耗/命中率） | **P1** | 从配表读取，替换 P0 的硬编码攻击力 |
| 挂载点管理（MountPoint，左手/右手/双肩装备与切换） | **P2** | 依赖部位破坏系统；`State.Broken.Arm` 联动禁用 |
| 背包词条（装备时注入 Infinite Modifier） | **P2** 末 | 依赖完整 `GameplayModifier` 系统 |
| 元件系统（触元件 + 应元件条件链） | **P3** | 依赖 `EventBus`、完整 Modifier、`GameplayTags` |
| 机兵品阶系统（金一/金二/金三/彩晋升流程） | **P3** | 局外养成，原型阶段硬编码满级 |
| 模组系统（四级/八级/小模组词条注入） | **P3** | `MechFactory` 装配时根据品阶注入 Modifier；彩满足才解锁八模组满级词条 |

### 3.5 效用评分大脑 (Utility AI)

| 子模块 | 优先级 | 说明 |
|--------|--------|------|
| 贪心 AI（靠近最弱目标并攻击） | **P1** | 够用，让关卡可测试 |
| Utility AI（Action-Location Pair + 响应曲线） | **P3** | 核心稳定后再做 |

---

## 数据管理与生命周期决策

### 决策状态

**已接受（2026-10-05）**

2026-10-27 文案校正：采用当前源码名称，明确配置查询封装按需引入；不改变原有生命周期决策。当前执行范围与验收状态以 `MechStorm_Unity/DEVELOPMENT_PROGRESS.md` 为准，T1.12 最小 Luban 迁移尚未开始。

### 背景

随着 MechStorm 继续发展，项目会同时出现三类“数据”：

1. **静态战斗配置**：Pilot、Mech、Weapon、Ability、State、Modifier 等由策划配置的规则和数值。
2. **单场战斗运行时状态**：当前 HP、AP、位置、回合、行动状态、胜负、日志和快照。
3. **局外长期数据**：玩家拥有的角色、机兵、装备、材料、等级、天赋、解锁内容和关卡进度。

这三类数据的生命周期、修改权限和持久化方式不同。如果把它们都放入一个类似 GF_X 的通用 `DataModel`，会产生以下问题：

- `BattleSession` 的单场战斗边界被全局状态污染。
- 静态配置和可变存档数据混合，难以区分“配置改变”与“玩家成长”。
- UI 临时状态、战斗 Runtime 和长期存档互相引用，导致跨场景残留和重启恢复困难。
- 运行时对象、Unity `GameObject`、事件订阅和可序列化存档模型混在一起，破坏 `MechStorm.Battle` 的无头核心约束。
- 为尚未出现的局外养成需求提前引入通用容器，增加抽象成本而没有当前验收收益。

当前源码已经形成了正确的局部边界：

```text
PilotData / MechData
    静态战斗基础数据

PilotRuntime / MechRuntime
    单场战斗中会变化的状态

CombatUnit
    Pilot 与 Mech 在本场战斗中的组合实体

BattleSession
    单场战斗的权威状态、规则入口、回合和日志

BattleScenarioConfig
    当前 ScriptableObject 战斗初始配置，不承载运行时状态；待 T1.12 迁移数据来源
```

因此，本决策不是“是否需要数据管理”，而是确定不同数据的所有权和生命周期，避免形成一个无边界的全局数据桶。

### 决策

MechStorm 采用**按领域和生命周期分层的数据管理体系**，不直接迁移 GF_X 的通用 `DataModel` 实现。

目标结构如下：

```text
ConfigSystem / Luban
    静态配置中心，只读

BattleSession
    单场战斗运行时数据中心，只在一场战斗内有效

PlayerProfile / ProgressionData
    局外长期数据中心，跨场景、可保存

BattleSaveData
    战斗续战和恢复边界，独立于局外玩家档案

Presentation
    UI 选择、窗口、焦点、高亮等临时表现状态
```

#### 1. 静态配置逐步迁移至 Luban

Pilot、Mech、Weapon、Ability、State、Modifier 等基础定义属于内容配置，不属于玩家存档，也不属于 `BattleSession` 的全局状态。

约束：

- 目标配置源为 Luban 表，运行时在异步加载完成后通过配置访问入口读取；`ConfigSystem` / `Tables` 表示目标职责，不代表当前已经接入，不采用首次访问时同步加载的方式。
- T1.12 先在 GameLogic 侧将 Luban 行转换为现有 Battle Data，再创建独立 Runtime；Battle 不依赖 Luban、GameProto 或 Unity。仅替换当前数据来源，不预建完整配置体系。
- 配置查询优先直接使用已加载的表和装配转换；只有出现重复查询、独立变化或明确复用需求时，才按领域提取查询封装，不要求预建 `PilotConfigRepository`、`MechConfigRepository`、`AbilityConfigRepository` 等仓储层。
- 如引入配置查询封装，其职责仅为读取和引用解析，不负责修改玩家进度。
- 配置表生成代码和二进制产物不能手工维护。
- 当前 `BattleScenarioConfig` 是 Sprint T1 正式战斗流程使用的 SO 配置入口；T1.12 等价与隔离验收后，再退役正式路径中的 SO 战斗数据来源，保留仍被测试使用的夹具。场景对象引用、材质与镜头调试参数留在表现层，不将 SO 战斗数据维护为第二套正式 Schema。

#### 2. `BattleSession` 继续作为单场战斗权威中心

`BattleSession` 管理一场战斗中的棋盘、单位注册表、回合、当前行动单位、规则结算、胜负、`BattleActionResult`、`BattleSnapshot` 和 `BattleActionLog`。

以下数据不得因为存在“全局数据中心”而上移到全局：

- 当前 HP、AP、位置和部位状态。
- 当前回合、当前阵营和已行动单位。
- 当前行动开始位置、行动选择和锁定目标。
- 单场战斗的胜负结果和行动日志。
- `CombatUnit`、`PilotRuntime`、`MechRuntime` 等运行时对象。
- 战斗中的事件订阅、规则服务和表现适配引用。

战斗结束、返回主界面或重新开始时，应释放该 `BattleSession` 及其关联表现对象。下一场战斗应从静态配置和明确的战斗入口重新构建，不能复用上一场的可变 Runtime。

#### 3. 局外数据在出现真实需求后建立领域模块

当第一个真实的局外功能需要跨场景共享和持久化时，再建立最小的局外数据模块。推荐边界为：

```text
GameModule.PlayerData
├── Profile
├── Inventory
├── Equipment
├── TalentProgression
└── SaveService
```

这不是要求一次性实现全部子模块。应从实际功能开始，例如：

- 关卡完成记录。
- 机兵或角色解锁。
- 材料库存。
- 一个天赋节点的解锁与消耗。

局外模块必须满足：

- 对外提供类型化模型和领域操作，不提供无类型字符串字典作为主 API。
- 内部集合不能被 UI 或其他模块直接替换。
- 资源消耗、解锁、升级等修改通过明确的业务方法或命令完成。
- 保存模型与运行时模型分离，支持存档版本号和迁移。
- 通过明确的通知边界刷新 UI，不让 UI 直接修改存档对象。
- 不引用 `BattleSession`、Unity `GameObject` 或单场战斗事件订阅。

#### 4. 战斗续战数据单独建模

战斗中途退出后的续战存档不是普通的局外玩家档案。它应单独使用 `BattleSaveData` 或等价模型，至少包含：

- 战斗场景或关卡身份。
- 可恢复的初始配置版本。
- `BattleSnapshot` 所需的战场 Runtime 状态。
- `BattleRNG` 的种子和游标。
- 已提交的 Command / Action 序列或恢复所需的关联信息。
- 存档 Schema 版本。

续战存档的保存时机是权威行动提交之后，而不是 UI 点击或表现动画播放期间。加载时应先校验配置版本和存档版本，再由 `BattleSession` 从恢复数据重建运行时状态。

#### 5. 表现层状态保持局部

UI 的选中单位、选中行动、待选目标、范围高亮、焦点地格、窗口打开状态和动画播放状态属于 `Presentation`，不进入 `PlayerProfile` 或 `BattleSaveData`，除非未来明确要求恢复某项用户界面偏好。

`BattleSession` 可以向表现层提供只读查询和结构化结果，但表现层不能通过全局数据中心绕过战斗规则直接修改战斗状态。

### 生命周期与所有权表

| 数据类别 | 代表对象 | 所有者 | 生命周期 | 是否持久化 |
|---|---|---|---|---|
| 静态配置 | Luban `TbPilot`、`TbMech`、`TbAbility` | `ConfigSystem` | 应用运行期只读 | 配置资源，不是玩家存档 |
| 战斗静态输入 | 关卡、编队、规则参数 | Battle 创建入口 | 创建 `BattleSession` 时读取 | 通过版本引用间接保存 |
| 战斗 Runtime | `CombatUnit`、`PilotRuntime`、`MechRuntime` | `BattleSession` | 单场战斗 | 仅在需要续战时写入 `BattleSaveData` |
| 战斗诊断 | `BattleSnapshot`、`BattleActionLog` | `BattleSession` / Diagnostics | 单场战斗或调试文件 | 可选，本地调试或回放 |
| 局外 Runtime | `PlayerProfile`、库存、养成状态 | 局外数据模块 | 跨场景、跨流程 | 是 |
| 局外存档 DTO | `PlayerProfileSaveData` | `SaveService` | 读写期间 | 是 |
| UI 临时状态 | 选择、高亮、窗口和动画状态 | `Presentation` | 当前窗口或当前场景 | 否 |

### 不采用的方案

#### 不直接迁移 GF_X `DataModel`

GF_X 的 `DataModel` 适合作为“按类型和 ID 管理多个业务模型”的参考，但不能作为 MechStorm 的整体数据架构直接接入，原因如下：

- 它不能自动解决静态配置、战斗 Runtime、局外存档和 UI 状态的边界问题。
- 通用的 `Create / Get / Release` 容器容易把不同生命周期的数据放到同一入口。
- JSON 持久化示例不能直接满足 MechStorm 的版本迁移、战斗快照、RNG 恢复和回放约束。
- 全局注册和释放时机若没有领域约束，容易与 `BattleSession` 的显式生命周期冲突。

可以借鉴其类型化模型、生命周期和存储接口思想，但具体实现必须贴合 MechStorm 的领域模块和无头核心边界。

#### 不把所有数据做成一个 `GameData` 单例

单例只解决访问位置，不解决所有权、并发修改、版本迁移和恢复时序。`GameModule` 可以作为模块访问入口，但不应成为承载所有业务字段的万能对象。

#### 不用 `PlayerPrefs` 存储复杂业务数据

TEngine 现有 `Utility.PlayerPrefs` 适合语言、音频和简单设置，不适合作为库存、养成、战斗快照或回放的主存储。复杂数据应使用版本化的存档 DTO 和独立的 `SaveService`。

### 接入时机

| 阶段 | 数据管理范围 | 结论 |
|---|---|---|
| 当前 Sprint T1 | `ProcedureBattle` 编排流程并持有 `BattleRuntime`；后者拥有 Session、场景表现与交互对象，随流程统一释放 | 不引入完整 DataModel |
| 首个局外功能 | 最小 `PlayerProfile` + 对应领域服务 + 存档 DTO | 以真实功能为入口建立 |
| T1.12 最小 Luban 迁移 | 异步配置加载、GameLogic 装配转换与等价验收；查询封装按需引入 | 只替换当前配置来源，不改变玩法或引入通用仓储层 |
| Sprint 12 续战 | `BattleSaveData`、Snapshot 恢复、RNG 恢复 | 独立于 PlayerProfile |
| 局外养成扩展 | Inventory、Equipment、TalentProgression 等按领域增加 | 不建立无边界总容器 |

### 后果

正面影响：

- 保留当前 `BattleSession` 的单场权威边界，不破坏纯 C# 战斗核心。
- Luban 配置、战斗 Runtime、局外存档和 UI 状态可以独立演进。
- 续战、悔棋、回放和存档版本迁移有明确落点。
- 局外养成出现真实需求后，可以渐进接入，不需要先搭建空泛框架。
- 便于测试：战斗测试只构造 `BattleSession`，局外数据测试不依赖 Unity 场景。

代价与风险：

- 访问路径不再是一个万能 `DataModel.Get<T>()`，调用方需要理解领域边界。
- 后续需要为 `PlayerProfileSaveData`、`BattleSaveData` 和版本迁移补充模型及测试。
- 配置访问、局外数据模块和战斗装配之间需要明确数据转换与所有权，不能直接共享可变 Runtime；确有需要时才提取查询封装或专用 DTO。
- 如果未来引入网络登录、云存档或多存档槽，需要进一步定义存储服务和冲突策略。

### 与现有路线的关系

- 不改变 `MechStorm.Battle` 纯 C#、确定性、`Data + Runtime` 和 `BattleSession` 约束。
- 不提前改变当前 Sprint T1 的范围。
- 与 T1.12 最小 Luban 迁移及 Sprint 11 的完整数据驱动和实体装配相配合：先替换现有配置来源，再随真实机制扩表；Luban 提供静态定义，装配过程创建本场战斗 Runtime。
- 与 Sprint 12 的 Command、Snapshot、续战和回放相配合：战斗持久化使用独立恢复模型。
- 与 P3 局外养成相配合：玩家进度模块在养成需求真实出现后按领域逐步增加。

---

## 四、技术栈与基建选型 (Infrastructure)

| 领域 | 选型 | 说明 |
|------|------|------|
| 底层框架 | TEngine（基于 UGF） | P2 Sprint 4 接入；P0/P1 裸 MonoBehaviour 开发原型 |
| 热更新方案 | **HybridCLR**（全栈纯 C#） | 弃用 Lua，实现 `MechStorm.Battle.dll` 与 `MechStorm.Presentation.dll` 双端 DLL 替换热更；独立 asmdef 必须加入 `UpdateSetting.HotUpdateAssemblies` |
| UI 架构 | 纯 C# MVP 模式 | `MechStorm.Presentation` 的 Presenter 强类型绑定 `MechStorm.Battle` 数据模型 |
| 配表工具 | Luban | Excel 导出 JSON + C# 对象 |
| 资源管理 | YooAsset | 异步 Bundle 加载 |
| 数值类型 | **整数化战斗逻辑 + 表现层 float** | 核心结算用整数/万分比/整数倍率，PVP 服务端权威无需完整定点数库；float 仅用于表现层动画、插值、相机、特效和 UI |
| GameplayTag 系统 | [GameplayTags](https://github.com/Alex-Rachel/GameplayTags) | 层级 Tag、整数索引高性能查找、内置 `GameplayTagRequirements` 条件匹配；Tag 需预注册 |
| 调试工具 | Odin Inspector + TEngine Runtime Debugger | 数据可视化 + 事件与状态流日志 |

---

## 五、开发排期表与里程碑 (Milestones & Sprints)

> 按"能玩优先"原则重新分层，P0 产出可玩原型，P1 补全核心机制，P2 完成工程化，P3 做扩展。
> **原则：没有可玩原型之前，不做任何超出当前层所需的抽象。**

---

### 🟢 P0 / Sprint 1：最小可玩原型 (Vertical Slice)

**目标：** 两个机甲在 10×10 方格地图上，能移动、能攻击、能死亡、能换回合。上手可操作。
**完成标准：** 玩家选中己方机甲 → 选目标格移动 → 选敌方机甲攻击 → 敌方 HP 归零消失 → 回合切换。

| 任务 | 内容 | 备注 |
|------|------|------|
| Task 1.0 | 建立 `MechStorm.Battle.asmdef`，不引用 `UnityEngine`；建 `MechStorm.Presentation.asmdef` 引用 Battle | 结构先行，逻辑与 Unity 表现层隔离开，哪怕 Battle 里只有一个空类 |
| Task 1.1 | 基础数据结构：`SVector2Int`（平面坐标）、`MechPart` 枚举 | 坐标系为 2D 平面；障碍物高度（`ObstacleHeight: Low/High`）存 `GridCell`，决定飞行单位通行合法性，不影响坐标维度 |
| Task 1.2 | 简单方格拓扑：`SquareGrid`，提供邻居查询与曼哈顿距离 | 不做接口隔离，先求能用 |
| Task 1.3 | 基础 BFS 移动范围：给定移动力，返回可到达格子集合 | 不做 Cost Matrix，先用等代价 |
| Task 1.4 | 实体壳结构：`PilotData`(挂 AP) / `MechData`(挂 HP/攻击/移动) / `CombatUnit` 合成壳 | 字段归属要对，内容先硬编码，不做部位破坏 |
| Task 1.5 | `TurnStateMachine`：玩家回合 → 敌方回合 → 循环 | 敌方先做原地不动 |
| Task 1.6 | 基础攻击结算：相邻格可攻击，固定伤害，HP 归零死亡 | 不做命中率和 RNG |
| Task 1.7 | 最简 Unity 表现层：Cube 占位模型，点击移动，血条 Slider | 允许逻辑与 View 脏耦合 |

**关联探针：** 无，先做，边做边发现问题。

---

### 🟡 P1 / Sprint 2~3：核心机制深化 (Core Mechanics)

**目标：** 让战斗有前线任务的深度——部位破坏、AP 系统、机型差异、地形与 ZOC。
**完成标准：** 打掉敌方手臂导致其换武、打断腿后移动力骤降、进入 ZOC 需要多消耗行动力、轻/中/重机型移动与槽位差异生效。

**Sprint 2 — 实体与战斗深化**

| 任务 | 内容 |
|------|------|
| Task 2.0 | `CombatUnit` 属性合成：角色属性（AP/命中/回避）与机兵属性（HP/移动/出力）合并为战场单位 |
| Task 2.1 | 多部位血量：躯干/左臂/右臂/腿，溢出伤害转移躯干 |
| Task 2.2 | `GameplayTags` 简版：哈希集合管理状态，实现 `State.Broken.*`、`Locomotion.*`、`Mech.Class.*` |
| Task 2.3 | BattleResource 简版：以 `BattleResource.ActionPoint` / `BattleResource.PotentialPoint` Tag 注入 `GameplayAttribute`，实现每回合 AP 回复、技能消耗校验（`SkillCost[]` 遍历）、AP 耗尽禁止行动 |
| Task 2.4 | 部位破坏效果落地：`State.Broken.Arm` 禁用武器，`State.Broken.Leg` 移动力降为 1 |
| Task 2.5 | 战斗结算引入 RNG：命中率计算（含部位命中权重），确定性 `BattleRNG`；`AttackContext` 携带攻击方式 Tag 集合（`Attack.Mode.*`）和时序 Tag（`Timing.*`），为后续元件/模组条件匹配预留接口 |
| Task 2.6 | 机型系统 + 武器数据：机型决定动态挂载点（MountPoint，轻/中/重，中甲双肩挂载点），从配表读武器属性 |

**Sprint 3 — 空间与地形深化**

| 任务 | 内容 |
|------|------|
| Task 3.1 | 升级 A*：引入地形消耗矩阵（Cost Matrix），替换 BFS |
| Task 3.2 | 地形类型与移动合法性：格子存 `TerrainType` + `Elevation`，按 `Locomotion` Tag 校验通行权限 |
| Task 3.3 | ZOC：敌方周围格子增加移动 Cost，软阻挡实现 |
| Task 3.4 | LOS + 掩体：射击线经过掩体/高地时命中率惩罚；高地扩展射程，不加伤害 |
| Task 3.5 | `IMapTopology` 拓扑隔离层：此时再做接口抽象，将 Square 与未来 Hex 隔离 |

**关联探针：** [Spike 1.A 六边形网格](#spike-1a-六边形网格的前瞻研究)（在 Task 3.5 前讨论）、[Spike 2.A 特异性 Buff 与光环](#spike-2a-特异性-buff-与光环的架构设计)

---

### 🟠 P2 / Sprint 4~5：工程化与解耦 (Engineering Hardening)

**目标：** 把 P0 的脏耦合重构干净，建立可维护的工程基础。
**完成标准：** 逻辑层零 UnityEngine 依赖；Luban 接管所有配置；悔棋功能可用。

**Sprint 4 — TEngine 接入 + 架构重构**

| 任务 | 内容 |
|------|------|
| Task 4.0 | **接入 TEngine**：搭建 Procedure 骨架（启动→战斗→结算），迁移 P0 的 UI 到 UIForm；建立分层 EventBus——战场级 `EventDispatcher` 一个实例，每个 `CombatUnit` 持有独立 `EventDispatcher` + `GameEventMgr`（死亡时自动注销订阅） |
| Task 4.1 | 提取/校验 `MechStorm.Battle` 程序集，彻底清除 `UnityEngine` 依赖；`MechStorm.Presentation` 仅负责 Unity 表现与 TEngine 适配 |
| Task 4.2 | MVP 重构：建立 Presenter 层，逻辑层通过事件通知 View，切断直接引用 |
| Task 4.3 | `GameplayAttribute` 完整实现：拦截器公式 `(Base+Add)×(1+Multiply)`，完整 `GameplayModifier` 系统 |
| Task 4.4 | `SpatialManager` 三层空间索引：将地貌层、附着物层、占据层分离 |
| Task 4.5 | 挂载点管理（MountPoint）：左手/右手/双肩装备与切换，`State.Broken.Arm` 联动禁用对应挂载点 |

**Sprint 5 — 数据与回溯**

| 任务 | 内容 |
|------|------|
| Task 5.1 | Luban 接入：设计 `TbMech`、`TbPilot`、`TbSkill`、`TbModifier`、`TbWeapon` 表结构，替换所有硬编码数据 |
| Task 5.2 | PP 资源系统：整场固定额度 + PP→AP 转换指令，纳入战斗结算管线 |
| Task 5.3 | 职业系统：职业枚举 + 职业基础属性差异（命中/回避/资源循环） |
| Task 5.4 | `MechFactory` 装配桥接：角色 + 机兵 + 武器/背包/元件 → 多对多组合为 `CombatUnit` |
| Task 5.5 | `ICommand` 指令封装：移动、攻击封装为 Command 压栈 |
| Task 5.6 | `MechSnapshot` 局部快照系统：只对本次行动影响到的实体序列化（HP/Tags/Modifier 数据/RNG 游标）；恢复时先清空 EventBus 订阅，写回数据，再按 Modifier 类型 ID 重建订阅 |
| Task 5.7 | 悔棋落地：一键回退 Snapshot，表现层同步复原 |
| Task 5.8 | `BattleDirector` 异步导演：UniTask/DOTween 接入，移动动画与弹道时序播放 |
| Task 5.9 | 战斗续战存档：战斗快照 + RNG 游标序列化写入本地磁盘；启动时检测未完成战斗并恢复（复用 MechSnapshot，仅切换存储介质） |
| Task 5.10 | 追加攻击系统：追击（`Attack.Mode.Active.Duel`，享受元件判定和增伤 Buff）与额外打击（`Attack.Mode.ExtraStrike`，不触发元件）；`AttackContext.FollowUpDepth` 限制嵌套深度 |
| Task 5.11 | 行动配额系统：`ActionQuota { MoveRange, CanAttack }` 注入机制；`TurnStateMachine` 在回合结束前检查未消费配额，实现再行动/再攻击/再移动词条 |

**关联探针：** [Spike 3.A PVP 同步策略预研](#spike-3a-pvp-同步策略预研)

---

### 🔴 P3 / Sprint 6+：扩展与商业化 (Scale & Polish)

**目标：** 在稳固核心上叠加扩展功能，向商业化方向延伸。
**前提：** P2 全部完成且核心战斗稳定可测试。

| 任务 | 内容 |
|------|------|
| Task 6.1 | 背包词条系统：装备背包时注入 Infinite Modifier（如稳动力+1、飞行能力解锁），卸下时移除 |
| Task 6.2 | 元件系统：触元件监听 `EventBus` 战斗事件，满足条件后判定应元件触发（概率/叠层），形成条件链 |
| Task 6.3 | 潜能天赋 + 神经驱动树：局外天赋加点，影响 AP 循环量/技能效果，经 `TalentBridgeFactory` 转为 Infinite Modifier 注入 |
| Task 6.4 | Utility AI：Action-Location Pair 双遍历 + 响应曲线打分，替换贪心 AI |
| Task 6.5 | HybridCLR 热更接入：将 `MechStorm.Battle.dll`、`MechStorm.Presentation.dll` 加入 `Assets/TEngine/Settings/UpdateSetting.asset` 的 `HotUpdateAssemblies`，并同步 HybridCLR Settings；构建时确认两个 DLL 均复制为 `.bytes` 热更资源 |
| Task 6.6 | YooAsset 资源管理：异步 Bundle 加载，替换直接 Resources 加载 |
| Task 6.7 | 实体状态透视仪：Odin Inspector 可视化 EventBus 日志与实体状态 |
| Task 6.8 | 机兵品阶养成流程：部件合并/材料晋升逻辑，拆解产出四级模组材料 |
| Task 6.9 | 模组系统：四级/八级/小模组词条配表驱动，`MechFactory` 装配时按品阶注入 Modifier；整套彩品阶解锁八模组满级额外词条 |
| Task 6.10 | 战斗录像（本地 Command Replay）：战斗中记录完整 Command 序列 + 初始状态 + RNG 种子/游标，战斗结束后序列化为本地文件，支持本地回放与确定性验证 |
| Task 6.11 | 战斗录像（分享 Result Replay / 混合日志）：上传服务器认可的 BattleLog，至少包含 Command + 权威 Result，生成分享链接，支持在聊天频道分享并让他人下载后按权威结果播放 |

**关联探针：** [Spike 4.A 可视化工具](#spike-4a-纯代码驱动下的可视化工具)

---

### 🟣 P4 / 长期目标：编辑器前置基建 (Authoring Foundation)

**目标：** 先把“能被编辑器安全配置”的底层能力补齐，不急着做完整可视化编辑器。此阶段重点是机制 Schema、校验器、调试报告、沙盒运行时和少量内部工具，确保后续编辑器不是直接操作散乱配置。

**前提：** P3 的核心战斗、轻量 GAS、Luban 数据驱动、回放和调试日志已经稳定。

**完成标准：** 可以不用完整 UI，仅通过配置表或简易内部面板，配置并验证 3 个复杂机制样例角色，且能解释目标选择、命中、伤害、Buff、Trigger 和 Reaction 的完整过程。

**核心原则：**

```text
先 Runtime，后 Editor
先 Schema，后可视化
先校验，后拖拽
先调试解释，后美观交互
```

**推荐任务拆分：**

| 任务 | 内容 |
|------|------|
| Task 7.1 | 机制 Schema 定稿：稳定 `Ability`、`Effect`、`State`、`Trigger`、`Condition`、`TargetRule`、`DamageFormula` 的数据边界 |
| Task 7.2 | GameplayTag 注册表：集中管理 Tag 定义、层级、互斥关系、废弃标记和引用检查 |
| Task 7.3 | 配置静态校验器：检查缺失引用、非法枚举、循环 Trigger、不可达 Condition、无效 TargetRule |
| Task 7.4 | TargetValidationReport：输出目标合法性、范围、LOS、AOE、阵营、部位和 Tag 过滤原因 |
| Task 7.5 | DamageBreakdown / AttributeBreakdown：输出伤害和属性最终值的来源拆解 |
| Task 7.6 | StateChangeLog / TriggerExecutionLog / ReactionLog：记录状态变化、触发链和状态反应过程 |
| Task 7.7 | `IBattlePreviewRuntime`：提供编辑器和测试共用的沙盒预览接口，不绕过真实 `MechStorm.Battle` 结算 |
| Task 7.8 | 标准复杂样例库：制作追击、反击、部位破坏、状态反应、多段攻击等样例配置 |
| Task 7.9 | 配置级回归测试：每个复杂样例绑定固定输入、期望 Result 和关键日志断言 |
| Task 7.10 | 内部调试面板：先用 Odin Inspector 或简易 EditorWindow 查看 Snapshot、Log、Breakdown 和校验结果 |

**暂不做：** 完整节点式技能编辑器、策划友好的拖拽工作流、内容包发布流水线、大规模平衡模拟。

---

### 🟤 P5 / 远期目标：战斗内容编辑器 MVP (Battle Content Editor)

**目标：** 在 P4 的 Schema、校验、调试和沙盒运行时稳定后，建设真正的战斗内容编辑器，使项目能够通过编辑器创建出《钢岚》同级别机制复杂度的角色。

这里的“同级别”不指表现层复刻，而指机制表达能力达到同类水平：角色、机兵、部位、武器、技能、Buff、触发器、状态反应、追击、反击、协力、条件增伤、AI 评分等都能由数据和规则组合实现，而不是为每个角色写专属硬编码。

**完成标准：** 不改核心代码，仅通过编辑器配置出 3 个以上复杂角色，并通过沙盒预览、配置校验和回归测试验证其机制正确。

**编辑对象：**

```text
PilotDefinition
MechDefinition
MechPartDefinition
WeaponDefinition
AbilityDefinition
StateDefinition
EffectDefinition
TriggerRuleDefinition
ConditionDefinition
TargetRuleDefinition
AreaShapeDefinition
DamageFormulaDefinition
GameplayTagDefinition
AiScoringDefinition
BattleCueReference
```

**必须覆盖的机制类型：**

- 多部位破坏、溢出伤害、部位禁用和挂载点联动。
- AP / PP 消耗、行动配额、再移动、再攻击和资源转换。
- 主动技能、普通攻击、追击、反击、协力和额外打击。
- Buff 持续、叠层、刷新、拒绝刷新、互斥、驱散和免疫。
- 状态反应，例如 `Wet + Burn -> Vaporize`，且支持双向触发和多段攻击多次触发。
- 条件增伤、条件减伤、护盾、伤害转移、命中修正和暴击修正。
- Target / Range / LOS / AOE / 地形 / ZOC / 移动能力标签。
- AI 评分参数、技能偏好、目标优先级和行动选择权重。

**推荐任务拆分：**

| 任务 | 内容 |
|------|------|
| Task 8.1 | 编辑器导航与资源索引：按 Pilot、Mech、Weapon、Ability、State、Tag 分类浏览和搜索配置 |
| Task 8.2 | 角色装配编辑器：组合 Pilot、Mech、部位、挂载点、武器、技能和被动规则 |
| Task 8.3 | 技能编辑器：编辑 Cost、TargetRule、AreaShape、HitSegment、EffectList 和 BattleCue 引用 |
| Task 8.4 | 状态编辑器：编辑 Duration、StackPolicy、RefreshPolicy、MutexStates、GrantedTags、Modifiers 和 TriggerRules |
| Task 8.5 | Trigger / Condition 编辑器：可视化配置时序、条件、触发次数限制和作用目标 |
| Task 8.6 | 公式与数值调试器：编辑 DamageFormula，并展示 DamageBreakdown 和 AttributeBreakdown |
| Task 8.7 | 目标范围预览器：在棋盘沙盒中预览 AOE、LOS、ZOC、部位目标和过滤原因 |
| Task 8.8 | 战斗沙盒模拟器：生成 Caster / Target Dummy，调用 `IBattlePreviewRuntime` 执行真实结算 |
| Task 8.9 | 配置校验面板：编辑时即时展示错误、警告、风险项和阻断项 |
| Task 8.10 | 复杂角色验收集：沉淀 3 个以上钢岚同级复杂角色作为编辑器验收样例 |

**边界约束：** 编辑器只能生成和修改数据，不能在编辑器侧实现一套绕过 `MechStorm.Battle` 的影子规则。

---

### ⚫ P6 / 远期目标：内容生产工业化与平衡验证 (Content Production Pipeline)

**目标：** 在 P5 战斗内容编辑器稳定后，把“能做出复杂角色”升级为“能稳定、批量、安全地生产复杂角色”。此阶段关注内容生产流水线、自动化验证、平衡分析、版本回归和长期运营，而不是继续堆单个战斗机制。

P6 的核心判断标准：

> 不只是能手工配置一个《钢岚》同级复杂角色，而是能持续生产一批同级复杂角色，并且能证明它们规则正确、强度可控、回归风险可发现。

**P6 工具链目标：**

```text
Battle Content Editor
-> Validation Pipeline
-> Simulation Runner
-> Balance Dashboard
-> Regression Test Suite
-> Content Package Builder
-> Release Review Workflow
```

**推荐任务拆分：**

| 任务 | 内容 |
|------|------|
| Task 9.1 | 内容包版本系统：按角色/机兵/武器/技能集合生成可审查的 Content Package |
| Task 9.2 | 自动规则校验流水线：在提交前扫描 Luban 表、Tag、Trigger、Condition、Effect 引用完整性 |
| Task 9.3 | 标准战斗沙盒库扩充：覆盖单体输出、AOE、反击、追击、状态反应、部位破坏等固定测试场景 |
| Task 9.4 | 自动回归 Runner：配置变更后批量执行沙盒，导出 Result、Log、DamageBreakdown、TriggerExecutionLog |
| Task 9.5 | 平衡指标面板：统计胜率、回合数、伤害分布、AP/PP 消耗、技能使用率和异常触发次数 |
| Task 9.6 | 配置 Diff 报告：展示技能、状态、数值公式和触发规则变更对战斗结果的影响 |
| Task 9.7 | 内容 Review 工作流：编辑器内标记风险项、测试覆盖状态、Review 结论和发布阻断项 |
| Task 9.8 | AI 辅助内容草案：生成技能/状态/条件组合建议，并自动附带测试场景草案 |

**边界约束：**

- P6 不替代人工设计，只提高生产、校验、回归和分析效率。
- AI 生成的任何内容都必须经过同一套 Schema、校验器、沙盒测试和人工 Review。
- 平衡模拟只作为参考指标，不能直接替代策划决策。
- 任何工具都不能绕过 `MechStorm.Battle` 真实结算核心。

---
> **当前入口：从 P0 / Task 1.1 开始。**

---

## 六、技术探针 (Spikes)

*需与 AI 助手重点探讨的开放性技术议题：*

### Spike 1.A 六边形网格的前瞻研究
- 若接入 `HexTopology`，相对坐标矩阵范围（如十字、扇形 AOE）在代码层面该如何优雅旋转？
- 六边形的 6 个侧击方向（Flanking）该如何用纯数学判定，并接入未来伤害公式？

### Spike 2.A 特异性 Buff 与光环的架构设计
- 如何设计"计次型" Buff（如抵挡下一次攻击后销毁）的拦截逻辑？
- 动态光环（Aura）移动时的性能陷阱，进出光环引发的频繁事件该如何防抖优化？

### Spike 3.A PVP 同步策略预研（已定案）
- **已确定：** 走服务端权威模式。服务端跑核心逻辑（唯一计算真相），客户端提交指令 + 播放表现。回合制战棋无需帧同步。
- **仍需预研：** 断线重连时快照恢复粒度；观战模式的数据流通道。

### Spike 4.A 纯代码驱动下的可视化工具
- 如何利用 Odin Inspector 或 TEngine Runtime Debugger，在游戏内构建一个"实体状态透视仪"，实时追踪复杂的 EventBus 派发日志？

---

## 七、悔棋方案选型 (Undo Strategy)

### 三种方案对比

| 方案 | 核心思路 | 优点 | 缺点 / 坑点 | 适用场景 |
|------|----------|------|-------------|----------|
| **全量快照** | 行动前序列化所有实体 | 实现简单；多步悔棋天然支持；恢复准确 | 内存随实体数线性增长；Modifier 事件订阅需额外处理（数据恢复了但行为丢失，或旧订阅残留触发两次） | 实体少（<20）、棋类/卡牌游戏 |
| **指令逆运算** | 每个 Command 实现 `Undo()` 反向 delta | 内存占用极低；无序列化开销 | 每个 Command 都要写正确的 Undo 逻辑；溢出伤害转移、Modifier 叠加等有副作用的操作逆运算极难写对；系统越复杂 bug 越多 | 系统简单、副作用可控的早期回合制 |
| **局部快照** | 只对本次行动影响到的实体做快照 | 内存和序列化开销远小于全量；无逆运算 bug 风险 | 需精确追踪"哪些实体被影响"，遗漏则出 bug；连锁效果（溢出→Modifier→事件）容易漏追踪 | 实体数量中等、每次行动影响范围有限、系统复杂度高的战棋/SRPG |

### 本项目选型：局部快照

选择理由：一场战斗 10~20 个单位，Modifier 系统复杂，每次行动影响实体有限，局部快照是最匹配的。

**为什么快照不能直接恢复 Modifier：**

> `GameplayModifier` 不是纯数据——除了持有数值（剩余回合数、层数），它还会在生命周期内订阅 EventBus 事件（如"每回合结束扣血"的 Modifier 会监听 `OnTurnEnded`）。
>
> 快照只保存了数值，不保存订阅关系。反序列化后：
> - 若订阅关系丢失 → 下一回合该 Modifier 不触发，行为消失
> - 若旧订阅未清理 → 同一 Modifier 触发两次
>
> 两种情况都是 bug，且无法通过再次修改数据修复。

**必须同时遵守的配套约束（P2 设计 `GameplayModifier` 时强制执行）：**

> `GameplayModifier` 必须设计为**纯数据 + 行为查表**：Modifier 实例只存数据（类型 ID、剩余时长、层数、数值参数），行为逻辑通过类型 ID 从注册表查出，EventBus 订阅关系由系统统一管理，不由 Modifier 实例自行订阅/取消。
>
> 反序列化恢复流程：① 清空当前实体所有 Modifier 的 EventBus 订阅 → ② 将快照数据写回实体 → ③ 遍历恢复后的 Modifier 列表，按类型 ID 重新注册订阅。
>
> 违反此约束将导致悔棋后行为丢失或重复触发，无法通过数据修复。

---

## 八、C# 编码与命名规范

### 7.1 核心指导思想
命名不仅追求大小写统一，更追求**纯正的英文语法（词性与时态）**。良好命名应让业务代码读起来像自然英文句子，从而消灭 80% 的无意义注释。

### 7.2 词性与时态法则

**布尔值（Booleans）：必须是一般疑问句**
必须添加系动词/助动词前缀，确保该变量只能回答 Yes / No。
- 状态询问（Is / Are）：`IsDead`、`IsTurnActive`、`AreAlliesAlive`
- 拥有询问（Has / Have）：`HasTag`、`HasArmor`、`HaveMoved`
- 能力/策略（Can / Should）：`CanCastSkill`、`ShouldDecayStacks`

**方法与函数（Methods）：必须是动宾短语（Verb + Noun）**
- 获取数据（Get / Find）：`GetNeighbors()`、`FindTarget()`
- 执行动作（Apply / Execute / Resolve）：`ApplyDamage()`、`ExecuteCommand()`、`ResolveAttack()`
- 状态修改（Add / Remove / ForceSet）：`AddModifier()`、`RemoveTag()`、`ForceSetHP()`

**事件与钩子（Events / Hooks）：严格区分时态**
- 动作即将发生（动词原形 / Present）：`OnBeforeAttack`、`OnTurnStart`
- 动作已经完成（过去分词 / Past Participle）：`OnDamageTaken`、`OnTurnEnded`、`OnPartBroken`

### 7.3 领域前缀与分类约束

- **配置表数据容器（Luban Configs）：** 强制以 `Tb`(Table) 为前缀，如 `TbMechBase`、`TbModifier`。
- **战斗修饰器与特效（Modifiers）：** 机制类型前置便于排序归类。
  - 被动词条类：`Passive_ChargeHorn`、`Passive_Vampire`
  - 状态/数值类：`Modifier_Stunned`、`Modifier_AttackUp`
- **数据传输载体（DTOs & Payloads）：** 纯数据容器，绝不允许包含业务逻辑（方法），如 `MechSnapshotData`、`AttackResultPayload`。

### 7.4 标签层级命名法 (Hierarchical Tagging)
`GameplayTags` 本身为字符串，但必须用英文句号 `.` 模拟树状层级：
- 状态类：`State.Dead`、`State.Stunned`、`State.Broken.LeftArm`
- 机制类：`Locomotion.Biped`、`Locomotion.Hover`
- 武装类：`Weapon.Sniper`、`Weapon.Melee.PileBunker`
- 机型类：`Mech.Class.Light`、`Mech.Class.Medium`、`Mech.Class.Heavy`
- 槽位类：`Mech.Slot.Hand.Left`、`Mech.Slot.Hand.Right`、`Mech.Slot.Shoulder`
- 攻击方式类：`Attack.Mode.Active`、`Attack.Mode.Reactive`、`Attack.Mode.Duel`、`Attack.Mode.NonDuel`
- 时序类：`Timing.Battle.Before`、`Timing.Battle.After`、`Timing.Turn.Start`、`Timing.Turn.End`、`Timing.Hit.Before`、`Timing.Hit.After`
- 行为禁止类：`Forbidden.ActionQuota`、`Forbidden.Attack`、`Forbidden.Move`、`Forbidden.Skill.Tactical`

### 7.5 C# 原生大小写底线
严格遵守微软 C# 规范，弃用下划线混用：
1. **类名 / 接口名 / 公开属性方法（Public）：** 帕斯卡命名 `PascalCase`，如 `public int CurrentHP { get; }`
2. **私有字段（Private Fields）：** 下划线 + 驼峰 `_camelCase`，如 `private int _remainingDuration;`
3. **接口声明（Interfaces）：** 强制以大写 `I` 开头，如 `IMapTopology`、`ICommand`

### 7.6 综合示例
```csharp
public class Modifier_ArmorMelt : GameplayModifier
{
    // 规范 7.5：私有字段下划线驼峰
    private int _remainingDuration;

    // 规范 7.2：布尔值疑问句
    public bool IsActive => _remainingDuration > 0;

    // 规范 7.2：事件钩子时态精确（已经附着）
    public override void OnAttached(MechEntity target)
    {
        // 规范 7.2 动宾短语 + 规范 7.4 标签层级
        target.AddTag("State.Debuff.ArmorMelted");
    }

    // 规范 7.2：事件钩子时态精确（回合已经结束）
    public override void OnTurnEnded()
    {
        if (ShouldDecayStacks())
        {
            RemoveOneStack();
        }
    }
}
```

---

## 九、AI Agent 工作流指令

## 开发者学习目标

本项目不仅是功能交付，也是开发者通过实践提升架构与编码能力的训练场。AI 协作原则如下：

- **先思考，再验证**：每个任务开发者先自己实现，完成后由 AI 做 Code Review（架构职责、设计边界、C# 惯用法）
- **先描述思路，再要代码**：遇到卡点先说明思路，AI 指出缺陷后开发者自己实现；不直接索要代码
- **关键设计决策由开发者拍板**：AI 摆利弊，开发者决策；决策过程本身是架构能力训练
- **扩展性自测**：每个模块完成后，AI 出"如果需求变成 X，你的代码需要改哪里？"的问题检验设计
- **P0 完全自己写**，AI 只做 Review 和答疑；P1 起逐步参与，但仍以开发者为主导

---

请扮演本项目的 **资深游戏客户端主程 / AI 编码助手 / Scrum Master**，严格遵守"无头核心"与"纯 C#"铁律。对话按以下工作流推进，**请勿一上来就狂扔代码**：

1. **Step 1：破冰与架构答疑**
   - 先确认已完全理解 `MechStorm.Battle` 的无头限制与 GAS 变种核心思想。
   - 针对四个 [Spike 探针](#六技术探针-spikes)（六边形适配、特异性 Buff、PVP 同步、可视化工具），请用户挑选一个最想先聊的议题。
2. **Step 2：深度推演**
   - 针对所选 Spike 展开深度技术脑暴，给出行业最佳实践与踩坑点，直到在架构思路上达成共识。
3. **Step 3：任务执行（进入 Sprint 1）**
   - Spike 疑虑扫清后，从 **Sprint 1 的 Task 1.1 / 1.2** 开始写代码，提供符合规范的 C# 接口与骨架代码。

