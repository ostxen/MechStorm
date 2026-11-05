# MechStorm 战斗配置工程

本工程属于 T1.12.1，配置域已按关卡出战、架构员、机兵装配、武器和技能分工。当前只更新结构，出战单位与装配关联留空。尚未运行 Luban，未生成 C# 或二进制，现有战斗仍读取 `BattleScenarioConfig.asset`。

## 定义与数据

- `luban.conf`：客户端生成目标，顶层命名空间 `GameConfig`。
- `Defines/*.xml`：唯一 Schema 来源。`Battle.xml` 定义关卡与出战关联；`Mech.xml` 定义机兵及武器装配；`Pilot.xml` 定义架构员、驾驶等级和技能池；`Skill.xml` 定义技能与武器默认普攻；`Weapon.xml` 定义武器；`Attack.xml` 定义共用内嵌攻击Bean。
- 普通工作簿使用 `Sheet1`，前三行分别为 `##var`、`##type`、`##` 注释，数据从第4行开始。
- `Datas/Skill/Skill.xlsx`：同一逻辑表的八个Sheet，采用多级分组表头。
- `Datas/Weapon/Weapon.xlsx`：range用两级分组标题展开，四行表头。
- XML负责注册表与定义类型，Excel负责数据，列名、顺序和类型须对应；不另建重复Schema工作簿。

当前共14张逻辑表、14份工作簿：

| 表 | 用途 | 数据行数 |
|---|---|---:|
| `Battle.TbScenario` | 关卡 ID 与棋盘宽高 | 1 |
| `Battle.TbDeployment` | 关卡单位、阵营、站位、顺序、架构员与机兵装配引用 | 0 |
| `Battle.TbUnitSkill` | 本场单位携带的机师技能及顺序 | 0 |
| `Mech.TbMechClass` | 机型规则 | 3 |
| `Mech.TbMech` | 机兵型号 | 4 |
| `Mech.TbMechPart` | 四部位基础定义 | 0 |
| `Mech.TbMechLoadout` | 四部件装配预设 | 0 |
| `Mech.TbMechLoadoutWeapon` | 预设的实际武器挂载 | 0 |
| `Pilot.TbPilot` | 架构员基础定义 | 0 |
| `Pilot.TbDrivingRank` | 驾驶等级与倍率 | 5 |
| `Pilot.TbPilotSkill` | 机师技能池 | 0 |
| `Skill.TbSkill` | 技能组合定义 | 10 |
| `Skill.TbWeaponDefaultSkill` | 武器类别默认普攻 | 3 |
| `Weapon.TbWeapon` | 武器基础定义 | 0 |

## 关卡与出战关联

```text
Battle.Scenario ← Battle.Deployment → Pilot.Pilot → Pilot.DrivingRank
                         │                 └← Pilot.PilotSkill → Skill.Skill
                         ├→ Mech.MechLoadout → Mech.MechPart → Mech.Mech → Mech.MechClass
                         │       └← Mech.MechLoadoutWeapon → Weapon.Weapon
                         └← Battle.UnitSkill → Skill.Skill
Weapon.kind → Skill.WeaponDefaultSkill → Skill.Skill
```

- `Scenario`保留ID 1、15×15棋盘。`Deployment`清空原四条占位部署，仅保留九字段表头：id/scenarioId/unitId/spawnOrder/faction/x/y/pilotId/mechLoadoutId。
- `Deployment.id`标识部署记录，`unitId`标识本场单位；按关卡筛选后，以`spawnOrder`确定出场顺序。`pilotId`引用`Pilot.TbPilot`，`mechLoadoutId`引用`Mech.TbMechLoadout`。
- `UnitSkill`为deploymentId/skillId/slotOrder三字段list表，联合索引`deploymentId+skillId`保证同部署同技能关联唯一，`slotOrder`表达本场携带顺序。当前0行，携带栏位容量及分类限制待定。
- `PilotSkill`表达可用技能池，`UnitSkill`表达本场选择携带；武器默认普攻按已装备武器授予并绑定来源，独立于机师携带技能。
- `CombatFaction`保留`TeamA=0`、`TeamB=1`、`Neutral=2`，由GameLogic转换为Battle类型。
- 旧Battle域的Pilot、Mech、BasicAttack、Ability、UnitAbility定义与五份工作簿已退役，原型数据可从`MechStorm_Unity/Assets/AssetRaw/Configs/BattleScenarioConfig.asset`对照。新配置按新域组织，原七表与SO的等价核对仅为历史记录。

## 机兵核心配置域（2026-10-30）

机兵域包含五表。三种机型有正式规则，型号表已填四个型号；部件、装配预设和武器挂载仍为空表。

| 表 | 工作簿（相对 Datas） | 字段 | 数据行数 |
|---|---|---|---:|
| `Mech.TbMechClass` | `Mech/MechClass.xlsx` | id、name、baseMoveRange、weaponMounts | 3 |
| `Mech.TbMech` | `Mech/Mech.xlsx` | id、name、classId | 4 |
| `Mech.TbMechPart` | `Mech/MechPart.xlsx` | id、mechId、partType、maxDurability、firepower、evasion | 0 |
| `Mech.TbMechLoadout` | `Mech/MechLoadout.xlsx` | id、name、classId、bodyPartId、leftArmPartId、rightArmPartId、legsPartId | 0 |
| `Mech.TbMechLoadoutWeapon` | `Mech/MechLoadoutWeapon.xlsx` | loadoutId、weaponMount、weaponId | 0 |

机型 ID 固定为 `1=轻型`、`2=中型`、`3=重型`，基础移动力分别为 `4/3/3`。默认挂载点用 `WeaponMount` 枚举列表，单元格以英文逗号分隔：

- 轻型、重型：`LeftHand,RightHand`。
- 中型：`LeftHand,RightHand,LeftShoulder,RightShoulder`。

当前型号数据按开发者提供顺序分配ID：

| 型号ID | 名称 | classId | 机型 |
|---|---|---|---|
| 1 | 疾啸 | 1 | 轻型 |
| 2 | 复仇女神 | 2 | 中型 |
| 3 | 辉龙 | 3 | 重型 |
| 4 | 星夜女神 | 1 | 轻型 |

`PartType` 为 `Body=0`、`LeftArm=1`、`RightArm=2`、`Legs=3`；双足为一个部位。它与武器挂载点是不同概念。枚举生成类型不引用 Battle；后续装配时显式转换。

引用关系：`MechClass ← Mech ← MechPart ← MechLoadout`，`MechLoadout.classId` 另引用机型。允许同机型跨型号混搭，不允许跨轻/中/重混搭。型号表不保存整机耐久、实际武器、养成或当前形态，部件表不保存当前耐久。火力与闪避只保存独立基础属性，公式后定，闪避不直接表示最终概率。

XML 已声明主键、范围、枚举和外键。以下跨字段/跨行约束需在填入数据后静态核对，并在后续装配入口实现校验，本批不声称已经有运行时校验器：

- 同型号、同部位的基础定义唯一。
- 四个部件字段分别引用 Body、LeftArm、RightArm、Legs。
- 部件经所属型号取得的机型必须与方案 classId 一致。
- 机型挂载点不可重复；允许挂载不代表已装备或当前可用。
- MechLoadoutWeapon为list表，联合索引`loadoutId+weaponMount`，每个预设每个挂载点最多一条；weaponId引用Weapon.TbWeapon。同一武器定义可供多个挂载记录引用，每条记录代表对应位置的装配。
- 实际挂载须在机型开放位置内，匹配武器安装类别与机型许可；双手握持的占槽规则另行确认。

Deployment通过mechLoadoutId取得装配预设，再读取四部件与武器挂载。战斗代码仍读取SO，配置转换与新模型执行后续接入。

机兵批次不新增Backpack、天赋、模组、形态或养成空表，不预填部件数值；后续独立武器域见下方。每机一个拓展背包及天赋特例仅为后续边界；基础机型身份不因形态变化而改变。部件/装配预设未填充，不能由本域创建完整出战机兵。

## 架构员基础配置域

`Defines/Pilot.xml` 定义 `Pilot.TbPilot`，数据位于 `Datas/Pilot/Pilot.xlsx`。当前仅有表头，数据行数为0；未将截图示例或旧Battle数值分配给未指定角色。

工作簿保持横排：身份与驾驶用浅蓝、六属性用浅绿、战斗资源用浅橙区分。冻结前三行及ID/名称列，中文说明加粗换行；列分组可收起驾驶详情、部分属性或资源列，并保留各区域首列定位。数据列预设对齐与细边框，有内容的数据行按奇偶交替着色，录入从第4行开始。

| 分类 | 字段 | 含义 |
|---|---|---|
| 身份 | id、name | 配置ID与名称 |
| 默认驾驶资格 | allowedMechClassIds | 引用Mech.TbMechClass的ID列表，英文逗号分隔，如1,2表示轻型与中型；不含天赋额外许可 |
| 驾驶等级 | drivingRankId | 引用Pilot.TbDrivingRank，角色不重复保存等级名称与倍率 |
| 基础属性 | melee、assault、shooting、tactics、defense、engineering | 格斗、突击、射击、战术、防御、工程，当前直接配置70级自身基础值，均为非负整数，公式后定 |
| AP | initialActionPoint | 进场初始AP，首回合不额外回复 |
| AP | maxActionPoint | 基础AP上限 |
| AP | actionPointRecoveryPerTurn | 此后己方回合开始时基础AP回复量，回复不超过上限 |
| PP | initialPotentialPoint | 单场初始PP |
| PP | maxPotentialPoint | 基础PP上限 |

科研、芯片等加成由各模块提供，独立装配层统一汇总，战斗与展示共用计算口径；接入等级养成后，自身基础值由当前等级及成长配置确定。职责详见 `MechStorm_Unity/BATTLE_ARCHITECTURE_ROADMAP.md` 的“养成属性与装配边界”。

后续天赋采用独立定义与机师关联表，专属资源及触发行为由装配层组合；等级成长按养成功能扩展。详见同文档“机师配置与天赋扩展”，当前Pilot基础字段保持。

驾驶许可仍为角色默认可驾驶机型列表，与驾驶等级分开。等级拆表后，Pilot共15字段，角色数据仍为空，不给所有角色预设截图等级。

### 驾驶等级表

`Pilot.TbDrivingRank` 与Pilot在同一XML域定义，但独立使用 `Datas/Pilot/DrivingRank.xlsx`，一表一文件。字段为 `id`、`name`、`performanceRate`：

| id | name | performanceRate | 含义 |
|---|---|---|---|
| 1 | 大师IV | 14000 | 140%作战能力 |
| 2 | 超适体III | 13700 | 137%作战能力 |
| 3 | 大师I | 12300 | 123%作战能力 |
| 4 | 大师II | 12900 | 129%作战能力 |
| 5 | 大师III | 13500 | 135%作战能力 |

前两行来自开发者截图，大师I/II/III的123%/129%/135%由开发者补充确认，大师IV为140%也已再次确认。ID仅为稳定配置键，不表示等级排序；不补全未知等级、前置关系或升级经验。倍率采用整数缩放10000=100%，具体影响属性和公式后定，不改变驾驶资格或机兵基础机型，也不自动将耐久、火力、AP等全部乘以倍率。玩家可升级时，再单独保存玩家当前等级，本批不实现养成。

AP与PP分别保留初始值和上限，即使数值相同也不合并；PP不自然回复，不配置PP每回合回复量。AP每回合回复量不表示消耗PP恢复AP的量，本表不含PP转AP字段、技能列表/消耗/携带、天赋效果、角色成长等级或经验。当前剩余资源属于战斗Runtime，不回写配置。

XML声明主键、非负范围及驾驶资格元素的跨域外键。初始AP不超过AP上限、初始PP不超过PP上限、驾驶资格列表不重复等跨字段或集合约束，留待填数据后静态核对及后续装配入口校验，本批不新增运行时校验器。

Deployment.pilotId已引用本域Pilot。当前战斗仍按SO的原型AP规则运行，新资源字段尚待运行时接入。

## 技能配置试表（2026-11-01）

采用简化GAS思路，配置描述玩法条件、消耗与效果，代码实现对应机制。当前三张逻辑表：

| 表 | 工作簿（相对 Datas） | 字段 | 数据行数 |
|---|---|---|---:|
| `Skill.TbSkill` | `Skill/Skill.xlsx` | id、name、description、icon、category、definition | 10 |
| `Pilot.TbPilotSkill` | `Pilot/PilotSkill.xlsx` | pilotId、skillId、displayOrder | 0 |
| `Skill.TbWeaponDefaultSkill` | `Skill/WeaponDefaultSkill.xlsx` | weaponKind、skillId | 3 |

`category`仅控制展示分类。`definition`为统一的组合Bean，不再按主动攻击/事件触发/持续被动划分互斥定义类型：

| 分组 | 含义 |
|---|---|
| `activation` | ManualActivation按武器射程手动使用、FixedRangeManualActivation按技能射程手动使用、EventActivation事件触发、WhileGranted随本场能力生效提供规则 |
| `costs` | 整次成功发动的资源消耗，不按效果或攻击段重复支付；空列表表示无消耗 |
| `limits` | 独立于发动方式的成功使用次数限制，maxUses为上限、reset为周期；空列表表示无次数限制 |
| `effects` | 成功发动后按顺序执行AttackEffect、FixedPartAttackEffect或RestoreResource，各自明确target |
| `ongoingModifiers` | 能力生效期间提供的自身条件伤害修正，移除或禁用能力后停止提供 |

条件列表均为AND：手动发动下的`useConditions`在使用校验与提交时检查；事件发动下的`triggerConditions`在事件发生时检查；持续修正下的`ongoingConditions`在每次参与对应伤害计算时检查。`selection`描述手动选择规则，效果`target`描述实际作用对象，EventTargetHasTag检查原攻击目标，三者不混用。攻击段仍按物理列顺序排列。WhileGranted只提供持续规则，costs、limits和effects须为空；持续修正不因发动一次而永久写入属性，也不自带状态时长。当前未实现运行时装配校验，未来接入时需校验这些组合及目标上下文是否可用。

配置只存上限与规则，已用次数、当前资源及状态生命周期由Runtime维护。简单效果内嵌，后续需要复用或持续状态时再独立定义；不增加通用脚本引擎或完整状态表。当前组合结构覆盖旧三类definition及早期SkillRule/SkillStep固定拆表草案。

Skill.xlsx八个Sheet合并为一个TbSkill，ID须跨Sheet唯一。第1行为`##var`，后续`##+`展开子标题，接着是`##type`和`##`中文说明；“主动攻击”“武器普攻”“固定射程攻击”“方向范围攻击”“固定部位攻击”均为七级标题、数据从第10行开始，“持续被动”“部位条件被动”六级标题、数据从第9行开始，“事件触发”五级标题、数据从第8行开始。合并单元格表达字段分组，`$type`选择发动规则、条件、效果或武器取得方式的多态类型；列表按子分组物理列顺序排列。空列表使用空单元格表示，增加组合时同步对应表头。

保持普通工作表样式：网格线、100%缩放、白底数据区、常规行高，字段表头浅灰底，冻结全部表头及标记/ID/名称三列。原样式包内容保持。

| ID | 技能 | 展示分类 / 结构 | 样例参数 |
|---|---|---|---|
| 1001 | 闪霎 | Active / ManualActivation + AttackEffect | 双刀剑、敌方单体、整次消耗2AP，两条MountedWeapon攻击各8500，均Active+Duel，效果作用于SelectedTarget |
| 1002 | 明镜之心 | PotentialPoint / EventActivation + RestoreResource | 自身主动攻击后，原目标有威压且自身AP≤4，消耗1PP回复自身3AP；limits为maxUses=1、reset=Turn |
| 1003 | 巅峰极诣 | Passive / WhileGranted + 条件伤害修正 | 每次伤害计算检查自身耐久比例>5000，造成伤害+1500，遭受伤害-1000 |
| 1004 | 星坠闪 | Command / FixedRangeManualActivation + AttackEffect | 3AP，菱形距离1～4的敌方单体，含飞行；SelectedWeapon选用一把刀剑，倍率12000，Active+NonDuel |
| 1005 | 碎钢 | Passive / WhileGranted + 条件伤害修正 | 当前攻击携带Active且受击前目标部位耐久≥5000，造成伤害+1500 |
| 1006 | 崩穹斩 | Command / DirectionalAreaManualActivation + AttackEffect | 4AP，面前横向或斜向折角3格敌方单位，左右刀剑各7500，Active+NonDuel |
| 1007 | 辉断终焉 | Active / ManualActivation + FixedPartAttackEffect | 5AP，敌方单体，固定Body，左右刀剑各12000，Active+Duel |

倍率与比例统一10000=100%。icon暂空，以上ID为新域独立配置键，与旧Battle技能ID无映射。样例来源为用户提供的艾达、淬锋凯登技能页，用于核对配表结构，不代表本项目已实现其战斗规则。

待后续机制接入时确认：

- 闪霎暂以左手段、右手段展示双武器列表，这只是样例排列，不定最终先后手规则；双武器射程组合、部位选择和中断规则仍待确认。
- 明镜之心使用语义标识`State.WeiYa`表达威压，状态系统接入时统一注册；它不是已存在的状态表外键。触发沿用原攻击目标上下文，目标Tag检查快照时点及每回合次数重置口径待确认。
- 技能特定的3AP回复不代表通用PP转换量。原文“每次出击最多拥有2PP”保留在说明，属于角色资源额度；角色数据确认后填Pilot，不重复配置到技能实例。
- 巅峰极诣的多部位“自身耐久比例”口径待确认，严格大于50%；修正参与伤害流程，叠算公式后定。

PilotSkill描述机师技能池及展示顺序，不是携带栏位。采用list表及`pilotId+skillId`联合索引，无独立记录id；同机师同技能关联唯一，displayOrder仅用于排序，同机师展示顺序唯一性待后续校验。新Pilot尚无角色数据，关联表先空。当前技能默认全解锁，天赋解锁与玩家携带方案后续扩展；栏位容量、分类限制与固定生效范围尚未确定。

十条技能已静态核对字段树、多态、列表、枚举和关键数值；关联表外键目标存在。首次导表仍需验证多Sheet、嵌套多态与空表兼容性，Excel实际显示待开发者查看。

### 测试用武器普攻

复杂角色技能暂保持，后续正式开发时再完善。Skill.xlsx新增“武器普攻”Sheet，原三个Sheet及样式条目保持。新Sheet沿用组合定义，三条均为手动使用、敌方单体、消耗1AP、1倍来源武器伤害，无额外状态、次数限制或持续修正：

| weaponKind | 默认技能ID | 名称 |
|---|---|---|
| Sword | 2001 | 刀剑普攻 |
| MachineGun | 2002 | 机枪普攻 |
| SniperRifle | 2003 | 狙击枪普攻 |

WeaponDefaultSkill.xlsx只有weaponKind/skillId两列，以武器类别作主键。装配时应按已装备武器的类别查此表，授予对应普攻并绑定具体来源武器；不依赖PilotSkill或角色技能携带，不占用待定的角色技能携带栏位。同类两把武器共享技能定义但保留各自来源，不将来源简化为技能ID，不提前决定HUD合并方式。

SourceWeaponUsable检查来源武器可用及类别匹配；EnemyUnitInSourceWeaponRange读取同一来源武器射程；AttackEffect内的单条攻击以SourceWeapon读取同一武器伤害，damageRate=10000，显式分类Active+Duel。普攻不固定左右手，不强制双武器同时攻击，也不能在来源武器不可用时静默改用另一把。来源武器改变或卸下后，相应授予及上下文应随装配更新，具体运行逻辑尚未接入。

默认普攻映射当前三条。Skill共八个Sheet十条数据，ID/枚举/多态/列表/数值/引用已静态核对；首次导表与战斗接入仍待后续任务。

### 艾达技能补充（2026-11-02）

来源：[艾达技能页](https://mecharashi.wiki/pilots/pilot_038_%E8%89%BE%E9%81%94?tab=%E6%8A%80%E8%83%BD)。本批保留1001闪霎，新增1004星坠闪、1005碎钢。

- 星坠闪保留3AP和单刀剑1.2倍伤害。FixedRangeManualActivation配置菱形选取距离1～4、允许飞行目标；EquippedWeaponUsable检查刀剑可用，Attack.SelectedWeapon绑定本次选用的已装备刀剑。指令攻击分类为Active+NonDuel。本批简化位移、落点校验及瞄准。
- 碎钢在每次伤害计算时同时检查AttackHasTag(Attack.Mode.Active)和TargetPartDurabilityAtLeast(5000)，提供Outgoing+1500；耐久比例取本次受击部位的受击前值。
- 开发者确认双刀合计倍率除以2，分别使用各自武器计算：崩穹斩1.5倍拆为7500+7500，辉断终焉2.4倍拆为12000+12000。闪霎原文明确2×0.85，保持8500+8500。
- 崩穹斩新增“方向范围攻击”Sheet，73列，数据第10行、冻结D10。DirectionalAreaManualActivation.patterns配置八方向模板，每个模板为相对施法者的三个偏移格：四个正交方向的面前横向三格、四个对角方向的折角三格；SelectedAreaEnemies取得覆盖格内敌方单位。两条攻击按左右刀剑分别计算。
- 辉断终焉新增“固定部位攻击”Sheet，27列，数据第10行、冻结D10。FixedPartAttackEffect.partType=Body，两条攻击分别按左右刀剑计算。
- 镜心、复辉、破势后续处理。位移、瞄准及额外资源效果（凝势）后置，基础发动费用保留。

新增条件及武器选择均为静态配置类型，实际绑定和结算由后续战斗接入完成。Pilot与PilotSkill仍为空。

### 统一攻击定义与分类标识（2026-11-02）

`Attack.xml`仅定义内嵌Bean，无独立数据表。`Skill.AttackEffect.attacks`为非空、有序的`Attack.Definition`列表，替代原WeaponAttack和SourceWeaponAttack两个效果；每条定义包含：

| 字段 | 职责 |
|---|---|
| classification.initiativeTag | 主动`Attack.Mode.Active`或非主动`Attack.Mode.Reactive`，必选其一 |
| classification.interactionTag | 对战`Attack.Mode.Duel`或非对战`Attack.Mode.NonDuel`，必选其一 |
| weapon | 多态WeaponUse：SourceWeapon使用能力来源武器；MountedWeapon取得指定挂载武器；SelectedWeapon使用本次选定的已装备武器 |
| damageRate | 该条攻击的武器伤害倍率，非负整数，10000为1倍 |

两个Tag字段分别使用Schema的`set`约束合法值，不采用自由Tag列表，避免同维度冲突、漏填或拼写错误；不创建四条分类记录的Excel、重复布尔标记或攻击分类枚举。后续装配到攻击上下文时，把两者作为Tag集合使用，Tag运行时注册、查询及AttackContext尚未实现。

分类属于具体攻击定义，不属于整个技能、武器大类或发动规则。手动/事件发动不决定主动/非主动，近程/远程不决定对战/非对战；事件触发的追击仍可能是主动攻击。当前闪霎两条指定挂载攻击和三条来源武器普攻均显式填写Active+Duel，不新增占位反应攻击样例。

Effect.target描述作用对象，activation描述选取规则；武器射程负责选取距离，范围技能以AreaPattern表达覆盖格。列表顺序只是双武器攻击的配置顺序，不代表每段必定建立独立交战、反击窗口或完整触发链；武器hitCount、溅射伤害与独立追加攻击也不能一律等同列表元素。整次技能费用由costs支付一次。

这里只统一静态结构与标识，不实现反击资格/顺序、非对战响应、隐身选取、触发时序、元件、状态生命周期或战斗执行，也不宣称Tag本身自动实现这些机制。来源身份绑定及交战边界随后续实际战斗机制接入。

## 武器基础配置域（2026-11-01）

`Defines/Weapon.xml` 注册 `Weapon.TbWeapon`，数据位于 `Datas/Weapon/Weapon.xlsx`。当前0条武器数据。

| 分组 | 字段 | 含义 |
|---|---|---|
| 身份与展示 | id、name、description、icon、quality | 稳定配置ID，名称必填，说明与图标可空；品质当前仅SS |
| 固有分类 | category、kind | 大类与细分类分开；不随机师形态改写 |
| 默认装备限制 | mountType、gripType、allowedMechClassIds | 安装类别、握持分类与默认机型许可，不保存实际左右挂载位置 |
| 基础属性 | attackPower、accuracy、critical、weight | 攻击力不是最终伤害；命中和暴击不是百分比；重量暂不参与负重规则 |
| 基础选取射程 | range.shape、range.minRange、range.maxRange | 普通Bean展开三列；不表示效果覆盖范围，不直接解析Wiki的1+/2+ |
| 基础攻击参数 | ammoCapacity、hitCount | -1表示无限弹药，有限容量须大于0；连击数至少1，不是技能攻击段数 |

共17个业务字段，range展开后19个数据列。Sheet1中A为标记列、B:T为数据列，第1行为`##var`，第2行为`##+`，第3行为`##type`，第4行为中文说明，数据从第5行开始。range合并P1:R1；冻结四行及标记/ID/名称三列。保持网格、100%缩放、黑色11号字、白底与20行高，字段标题浅灰，说明行48并换行。

武器细分类统一为 `Weapon.WeaponKind`，从原Skill域迁入并保留Sword=0、MachineGun=1、SniperRifle=2。Skill的SourceWeaponUsable、WeaponDefaultSkill与Attack.MountedWeapon共同引用。其他细分类枚举只表达身份，不代表已有SS样本、默认普攻或战斗执行支持。普通武器继续按kind查询现有 `Skill.TbWeaponDefaultSkill`，不在武器行重复填写默认技能ID、1AP或伤害倍率。

以下约束已明确，但本批空表且未建立运行时装配校验器：

- allowedMechClassIds引用Mech.TbMechClass，使用英文逗号分隔，非空且不重复；全机型填1,2,3。天赋额外许可与默认限制分开。
- category与kind匹配：刀剑/拳套/打桩机/电锯/长柄/大盾/手盾归格斗，机枪/重机枪/霰弹枪/喷火器归突击，两类狙击归射击，导弹/火箭/电磁炮/浮游炮归战术。粒子荚舱与固定武装的具体大类及装配规则核实后再填。
- Hand使用OneHanded或TwoHanded；Shoulder/Back使用NotApplicable。握持占槽规则待定；实际左右挂载由MechLoadoutWeapon记录。
- minRange和maxRange均为正整数，maxRange≥minRange，选取距离含上下边界。Diamond距离为`abs(dx)+abs(dy)`；Ring距离为`max(abs(dx),abs(dy))`，1圈包含周围8格。打击覆盖范围由技能定义。
- ammoCapacity只能为-1或正整数；0和小于-1非法。配置保存基础容量，当前弹药属于本场Runtime；每次攻击如何消耗弹药、连击如何判定后定，不用攻击力乘连击数推导伤害。

MountType.Back不扩展现有Mech挂载点，中型肩部许可不自动成为背部许可。重量只记录，不预定单位或增加出力/超重公式。后续在填数时确认统一基础等级口径。

角色天赋与形态按category/kind限制武器或提供规则，不增加“先锋形态武器”等专属分类，不回写武器定义。固定武装及限定能力集合随形态功能另行设计，不能将普通默认普攻变成特殊形态无法覆盖的硬规则。

本批不建立元件槽位、武器附加技能/生效方式、种类系数、成长或装备实例字段。以后元件槽位、武器能力及成长规则可通过稳定武器ID关联；玩家武器实例保存养成和实际元件，装配关系保存实际挂载，本场Runtime保存剩余弹药与可用状态。属性贡献由装配统一汇总并保留具体武器来源，不直接把两把武器属性全部加到整机。

武器表结构、空表状态、分组与样式、枚举及默认普攻映射已静态核对；首次导表与运行时接入待后续执行。

## 导出入口（需另行授权后执行）

开发者已自行构建 Luban，`Tools/Luban/source-revision.txt` 记录提交 `029f714f935ba58d7e33530da87e6371ee3c519f`。工具已存在，但尚未执行首次导表；仍需验证与现有 `GameProto/LubanLib` 的兼容性，不能据此宣称导出链已验证。

工具预期位于仓库根 `Tools/Luban/Luban.dll`，并保留其所需依赖；也可将 `LUBAN_DLL` 环境变量设为完整工具路径。运行生成器需要对应的 .NET 运行时。下列导表脚本不下载工具或安装依赖。

### 获取生成工具（与导表分开执行）

仓库根 `Tools/build-luban.bat` 和 `Tools/build-luban.sh` 已适配本项目含空格的路径，按脚本自身位置定位，不依赖当前工作目录。开发者已完成一次构建，本批不重新获取或构建。

- 前置条件：Git、网络，以及官方 Luban 源码要求的 **.NET SDK**，只有运行时不能编译。
- Windows：从仓库根运行 `Tools\build-luban.bat`；双击也可。自动化调用前设置 `AI_MODE=1` 可取消末尾暂停。
- macOS/Linux：从仓库根运行 `bash Tools/build-luban.sh`。
- 首次从 `https://github.com/focus-creative-games/luban.git` 克隆至 `Tools/luban-source`；后续执行 fetch 与 `pull --ff-only`，更新失败即停止，不退回旧源码继续构建。
- Release 构建输出到 `Tools/Luban`，与现有导表入口默认路径一致。脚本不整目录删除，但构建会覆盖同名产物；构建失败时不得继续使用该目录导表。
- 构建成功后 `Tools/Luban/source-revision.txt` 保存源码提交号。脚本跟随官方源码更新，**记录提交号不等于固定版本**；首次使用后仍需确定后续采用的工具提交及运行库兼容性。
- 源码检出与构建产物已加入 Git 忽略。脚本不安装 SDK，不导表，不刷新 Unity。

### 导出配置

Windows：从仓库根运行 `Configs\GameConfig\gen_code_bin_to_project.bat`。

macOS/Linux：从仓库根运行 `bash Configs/GameConfig/gen_code_bin_to_project.sh`。

现有 Unity 转表菜单调用 `gen_code_bin_to_project_lazyload`，因此保留同名兼容入口，但它只委托标准脚本，不复制 Skill 示例中的同步懒加载模板。直接调用和 Unity 菜单均会实际导表，本批次未执行。

固定导出目标：

- 格式：`cs-bin` + `bin`。
- C#：`MechStorm_Unity/Assets/GameScripts/HotFix/GameProto/GameConfig/`。
- 二进制：`MechStorm_Unity/Assets/AssetRaw/Configs/bytes/`。
- 不手写生成代码或数据；新建 Unity 目录与文件的 `.meta` 等待开发者正常刷新生成。
- 脚本没有设置导出后自动刷新、编译或运行，也没有删除旧输出目录的步骤。

## 后续接入边界

1. T1.12.2：YooAsset 异步加载二进制，明确资源句柄释放；配置未就绪或失败时不创建战斗，不采用同步懒加载，不擅自增加异常恢复或自动返回。
2. T1.12.3：GameLogic承担配置转换与单位装配，创建全新Session/Runtime；新四部位、武器及技能组合需要对应Battle数据与执行规则后才能使用。GameProto不依赖GameLogic/Battle，Battle不依赖GameProto、Luban或Unity。
3. T1.12.4：先明确新配置与原型规则的差异及验收范围，再按确认的数据核对初始快照、技能顺序、资源与执行行为，确认配置不受战斗修改、重战恢复初值。SO在新路径验收后退役，测试夹具保留；当前仅保留15×15关卡，尚无完整出战数据，不能按旧七表宣称等价迁移完成。

XML声明主键、联合索引、单字段范围、枚举与外键。站位边界/不重叠、关卡内unitId与出场顺序唯一、携带技能顺序唯一及所属机师技能池匹配、驾驶许可、部件与武器装配合法性、`maxRange >= minRange`等跨字段或跨行约束，须在填数据后核对并在后续装配入口校验。

首次导出时还需实际验证 XML Schema、Excel 解析（含空表与枚举列表）、输出命名与现有 ByteBuf 的兼容性，不以静态核对替代这一步。所有任务状态以 `MechStorm_Unity/DEVELOPMENT_PROGRESS.md` 为准。
