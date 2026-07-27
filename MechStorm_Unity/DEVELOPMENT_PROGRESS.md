# MechStorm 开发进度

## 当前阶段

- 当前里程碑：P1 / Sprint 4
- 当前任务：Task 4.7 临时聚合场景 SO 与配置校验
- 当前状态：Sprint 4 进行中；Task 4.1～4.6 已完成，准备开始 Task 4.7
- 最后更新：2026-07-27

## 状态约定

- `[ ]` 未开始
- `[~]` 进行中
- `[x]` 已完成
- `[!]` 阻塞
- `[?]` 待决策

## P0 / Sprint 1：最小可玩原型

- [x] Task 1.0 建立 `MechStorm.Battle` / `MechStorm.Presentation` asmdef 边界
  - 状态：已完成
  - 完成标准：`MechStorm.Battle` 不引用 `UnityEngine`，`MechStorm.Presentation` 可引用 Battle
  - 验证方式：已检查 `MechStorm.Battle.asmdef` 的 `noEngineReferences: true` 与 `MechStorm.Presentation.asmdef` 引用 Battle；`dotnet msbuild MechStorm.Battle.Tests.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过
  - 备注：对应历史提交 `d612f9a Establish MechStorm battle assembly boundary`

- [x] Task 1.1 基础数据结构：`Vector2Int`、`MechPart`
  - 状态：已完成
  - 完成标准：纯 C# 结构体与枚举位于 `MechStorm.Battle`，不依赖 Unity
  - 验证方式：`dotnet build MechStorm.Battle.csproj --no-restore` 通过；确认 Battle 目录无 `UnityEngine` 引用
  - 备注：项目决策使用 `MechStorm.Battle.Foundation.Vector2Int`；表现层如需 Unity 类型，显式使用 `UnityEngine.Vector2Int`

- [x] Task 1.2 简单方格拓扑：`SquareGrid`
  - 状态：已完成
  - 完成标准：提供邻居查询与曼哈顿距离
  - 验证方式：`dotnet msbuild MechStorm.Battle.Tests.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过；EditMode 测试用例已覆盖边界、邻居数量、曼哈顿距离
  - 备注：已添加 `Vector2IntTests` 与 `SquareGridTests`；已修复非法尺寸校验、邻居集合容量、曼哈顿距离绝对值；后续可在 Unity Test Runner 实跑确认

- [x] Task 1.3 基础 BFS 移动范围
  - 状态：已完成
  - 完成标准：给定移动力返回可到达格子集合
  - 验证方式：`dotnet msbuild MechStorm.Battle.Tests.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过；Unity Test Runner EditMode 测试已通过
  - 备注：已补 `GetReachablePositions` 与测试，当前仅支持等代价四方向移动，不含障碍、地形 Cost、ZOC、A*；BFS 依赖 Queue 先进先出保证近距离格子先处理，首次访问即为最短步数

- [x] Task 1.4 实体壳结构：`PilotData` / `MechData` / `CombatUnit`
  - 状态：已完成
  - 完成标准：字段归属正确，AP 挂 Pilot，HP/攻击/移动挂 Mech
  - 验证方式：`dotnet msbuild MechStorm.Battle.Tests.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过；Unity Test Runner EditMode 测试已通过
  - 备注：已实现 `PilotData`、`MechData`、`CombatUnit`；`Position` 通过 `MoveTo` 显式变更，后续接入移动校验与 Command

- [x] Task 1.5 `TurnStateMachine`
  - 状态：已完成
  - 完成标准：玩家回合 → 敌方回合 → 循环
  - 验证方式：`dotnet msbuild MechStorm.Battle.Tests.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过；Unity Test Runner EditMode 测试已通过
  - 备注：已由开发者实现初版，AI 做 Review 与小修；P0 暂不接入行动配额、事件系统、AI 决策与 TEngine Procedure；当前默认玩家先手仅服务单人 P0 原型，后续如需敌人先手可给构造函数增加初始阶段参数

- [x] Task 1.6 基础攻击结算
  - 状态：已完成
  - 完成标准：相邻格可攻击，固定伤害，HP 归零死亡
  - 验证方式：`dotnet msbuild MechStorm.Battle.Tests.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过；Unity Test Runner EditMode 测试已通过
  - 备注：已拆分 `PilotData` / `PilotRuntime`、`MechData` / `MechRuntime`；`MechRuntime` 已支持扣耐久、耐久归零与销毁状态；`AttackResolver` 已支持相邻格固定伤害与非相邻异常；相邻距离使用命名常量表达业务含义

- [x] Task 1.7 最简 Unity 表现层
  - 状态：已完成
  - 完成标准：完成 1.7.1~1.7.7 子任务后，能在 Unity 中看到格子、单位、点击移动与血条变化
  - 验证方式：Unity Play Mode 手动验证；纯 C# 辅助逻辑优先补 EditMode 测试
  - 备注：进入表现层前已补 `CombatUnitFactory`，用于由 `PilotData` / `MechData` / 初始位置创建逻辑战斗单位，并自动初始化独立的 `PilotRuntime` / `MechRuntime`；相关 EditMode 测试已通过；表现层保持在 `MechStorm.Presentation`，不要让 Battle 引用 Unity

- [x] Task 1.7.1 坐标转换器：`GridCoordinateConverter`
  - 状态：已完成
  - 完成标准：支持 `Battle.Foundation.Vector2Int` 与 `UnityEngine.Vector3` 双向转换，包含 `cellSize`、`origin`
  - 验证方式：`dotnet msbuild MechStorm.Presentation.csproj /t:Build /p:Configuration=Debug /verbosity:minimal` 通过；Unity Test Runner Presentation EditMode 测试已通过
  - 备注：这是 Presentation 适配层，不能放进 Battle；`origin` 使用 `UnityEngine.Vector3` 表示整个网格左下角世界坐标，`cellSize` 表示单格 Unity 世界尺寸，后续需抽到外部作为转换器与棋盘渲染器共享的公共参数；`GridToWorld` 返回格子中心点，`WorldToGrid` 返回世界点所在格子；注意避免与 `UnityEngine.Vector2Int` 命名冲突

- [x] Task 1.7.2 棋盘显示：`BattleBoardRenderer` / `CellView`
  - 状态：已完成
  - 完成标准：根据 `SquareGrid` 生成可见格子地面，P0 可使用薄 Cube 占位
  - 验证方式：Unity Play Mode 手动验证格子数量、位置、尺寸正确；已确认棋盘坐标与 `GridCoordinateConverter` 对齐
  - 备注：先不做复杂材质、地图资源、地形 Cost 与障碍；P0 可先用一个缩放后的 Plane 或薄 Cube 表示棋盘，`CellView` 可选；TODO：将 `cellSize` / `origin` 抽到外部公共配置或启动参数，供 `GridCoordinateConverter` 与 `BattleBoardRenderer` 共同使用

- [x] Task 1.7.3 单位显示：`CombatUnitVisual`
  - 状态：已完成
  - 完成标准：用 Cube 显示 `CombatUnit`，能根据 `CombatUnit.Position` 同步世界坐标
  - 验证方式：Unity Play Mode 手动验证单位出现在对应格子；已确认运行时测试通过
  - 备注：一个 `CombatUnitVisual` 只管理一个 `CombatUnit` 的表现，可持有单位引用和自身 `Transform`；Visual 只同步位置、颜色、选中态与后续血条挂点，不判断移动是否合法，也不写战斗规则

- [x] Task 1.7.4 输入与射线检测：`BattleBoardInputter`
  - 状态：已完成
  - 完成标准：鼠标点击通过 `Physics.Raycast` 得到格子世界坐标，并转换为 Battle 网格坐标
  - 验证方式：Unity Play Mode 手动验证点击不同格子能得到正确坐标；已确认点击有日志输出
  - 备注：射线检测属于 Presentation；逻辑层只接收转换后的网格坐标；该控制器只负责输入采集与坐标转换，不选择单位、不执行移动；当前命名为 `BattleBoardInputter`

- [x] Task 1.7.5 移动规则控制器：`MovementResolver`
  - 状态：已完成
  - 完成标准：输入 `CombatUnit`、目标格与 `SquareGrid` 后，能判断目标格是否在移动范围内；合法时调用 `CombatUnit.MoveTo(target)`，非法时保持原位置
  - 验证方式：EditMode 测试覆盖可达移动、不可达不移动、越界不移动、原地移动、空单位与空网格；已确认测试用例全绿
  - 备注：这是移动规则入口，放在 Battle 逻辑层或纯 C# 可测试层；P0 复用现有 BFS 移动范围，不做路径动画、A*、障碍、地形 Cost 和单位占用表；命名采用 `MovementResolver`，与 `AttackResolver` 保持一致

- [x] Task 1.7.6 表现层编排器：`BattlePresentationController`
  - 状态：已完成
  - 完成标准：串联单位选择、格子点击、移动规则控制器与 `CombatUnitVisual` 刷新，形成“选单位 → 点格子 → 合法移动 → 表现同步”的闭环
  - 验证方式：Unity Play Mode 手动验证单位能移动到可达格子，不可达格子不移动；已确认验收通过
  - 备注：P0 先用单个玩家单位；该类只做流程编排，不直接实现移动规则，也不直接处理射线细节；移动插值、朝向变化和可移动范围高亮暂不纳入本任务

- [x] Task 1.7.7 最简血条：`UnitHealthBarView`
  - 状态：已完成
  - 完成标准：显示 `CurrentDurability / MaxDurability`，攻击或扣血后可刷新
  - 验证方式：Unity Play Mode 手动验证血条能正常创建并显示
  - 备注：当前采用程序化创建的 World Space Canvas，挂在单位 `HealthBarAnchor` 下；显示血条背景、填充条与 `current/max` 文本；暂不接 TEngine UIWindow、Prefab 动态加载和对象池

## 坐标系统约定

- Battle 逻辑层使用 `MechStorm.Battle.Foundation.Vector2Int` 表示平面格子坐标。
- `Vector2Int.X` 映射到 Unity 世界坐标 `x`。
- `Vector2Int.Y` 映射到 Unity 世界坐标 `z`。
- Unity 世界坐标 `y` 只表示表现高度，不进入当前 P0 战斗逻辑坐标。
- `origin` 表示整张棋盘左下角边界点，不是 `Grid(0,0)` 的中心。
- `GridCoordinateConverter.GridToWorld` 返回格子中心点。
- 当 `origin = (0,0,0)` 且 `cellSize = 1` 时，`Grid(0,0)` 的中心是 `(0.5,0,0.5)`，`Grid(1,1)` 的中心是 `(1.5,0,1.5)`。
- `WorldToGrid` 会把世界点转换为其所在格子；落在边界上的点按 `Mathf.FloorToInt` 规则归入右侧或上侧格子。
- `CombatUnit.Position` 暂时保持二维格子坐标；`CombatUnitVisual` 通过额外高度偏移处理 Cube 或后续模型的离地高度。

## P1 / Sprint 2~8：核心机制深化

- [x] Sprint 2：实体与战斗深化
  - 状态：已完成
  - 总目标：从单单位移动演示升级为最小战斗流程，支持多个单位、当前行动单位、移动 / 普通攻击、扣血、死亡、回合推进与表现同步
  - 完成标准：场上至少有 TeamA 与 TeamB 各一个单位；当前单位可移动、攻击相邻敌对单位；目标扣血后血条刷新；HP 为 0 后不再行动；回合可从 TeamA 切到 TeamB 再切回 TeamA
  - 备注：继续使用 `TempGameEntry` 作为测试入口，不立即接 TEngine 正式流程；先把 Battle 纯 C# 核心机制跑通

- [x] Task 2.1 战斗流程控制器：`BattleController` / `BattleSession`
  - 状态：已完成
  - 完成标准：集中管理棋盘、单位列表、当前回合、当前行动方与当前行动单位；提供移动、攻击、结束行动等入口
  - 验证方式：`MechStorm.Battle.Tests` 与 `GameLogic` 编译通过；EditMode 测试覆盖初始化、当前行动单位、移动、攻击、结束行动与基础异常输入，已由开发者在 Unity Test Runner 手动验证通过
  - 备注：`BattleSession` 已作为棋盘、单位注册表、回合状态机和规则服务的统一入口；`TempGameEntry` 只创建测试战斗，`BattlePresentationController` 通过会话执行移动；结构化 `BattleActionResult` 仍按 Task 2.6 引入

- [x] Task 2.2 多单位与阵营管理
  - 状态：已完成
  - 完成标准：支持 `TeamA / TeamB` 至少两个稳定阵营；能查询存活单位、死亡单位、当前阵营单位；死亡单位不能再行动
  - 验证方式：`MechStorm.Battle.Tests` 与 `GameLogic` 编译通过；EditMode 测试覆盖阵营与成员查询、Neutral、死亡跳过、死亡单位禁止行动及阵营全灭判断，已由开发者在 Unity Test Runner 手动验证通过
  - 备注：实际范围比原计划略有扩展：新增可选 `Neutral` 阵营；拆出无 Grid 依赖的 `CombatUnitRegistry`，统一负责成员、阵营、存活、死亡、空集合、重复注册和跨阵营重复校验；单位初始位置合法性仍由拥有棋盘的 `BattleSession` 校验。当前 `BattleSession` 已具备“阵营内查找下一存活单位并切换阵营”的临时逻辑，属于 Task 2.3 的提前雏形，但尚未拆出 `TurnCoordinator`、建立行动状态与新回合重置，因此不视为 Task 2.3 完成

- [x] Task 2.3 行动流程与回合推进：`TurnCoordinator`
  - 状态：已完成
  - 完成标准：单位行动后能进入下一个可行动单位；阵营内所有可行动单位完成后切换阵营；新回合重置行动状态
  - 验证方式：`MechStorm.Battle.Tests`、`MechStorm.Presentation`、`GameLogic` 与 `Assembly-CSharp-Editor` 编译通过；Unity Test Runner 全部测试由开发者手动验证通过；Play Mode 已验证 TeamA / TeamB 切换、当前单位变化、回合数递增和对应单位表现移动
  - 备注：已用 `TurnCoordinator` 替代仅切换 Player / Enemy 的 `TurnStateMachine` / `TurnPhase`，集中管理当前回合、当前阵营、当前单位索引、死亡跳过和阵营推进；`BattleSession` 只保留委托入口，Neutral 不进入自动行动顺序。实际范围略有扩展：补齐 EnemyA 表现与血条、按 `CombatUnit` 映射 Visual、Inspector 战斗状态面板、行动结束调试按钮和切换前后日志。这些内容分别为 Task 2.4 的敌方表现、Task 2.5 的结束行动入口和 Task 2.6 的调试观察能力提供了前置验证，但尚不等同于正式攻击交互、游戏 UI 或结构化结果通知

- [x] Task 2.4 普通攻击接入战斗流程
  - 状态：已完成
  - 完成标准：通过 `BattleController` 调用 `AttackResolver`；攻击后目标扣耐久，目标血条刷新；HP 归零后进入死亡状态
  - 验证方式：Battle、Battle.Tests、Presentation、GameLogic 与 Editor 编译通过；EditMode 测试覆盖相邻攻击、非相邻失败、致死、空目标、未注册目标、同阵营目标、死亡目标和 TeamB 反向攻击；Unity Play Mode 血条刷新、失败日志和死亡日志已由开发者手动验证通过
  - 备注：`BattleSession` 负责成员、阵营和死亡目标校验，`AttackResolver` 保持距离与伤害结算职责；临时 Inspector 增加当前单位攻击对方的验证入口，攻击成功后按目标单位刷新血条。正式点击敌方单位选择目标仍属于 Task 2.5，结构化攻击结果仍属于 Task 2.6

- [x] Task 2.5 最小战斗输入
  - 状态：已完成
  - 完成标准：支持点击当前单位、点击格子移动、点击敌方单位普通攻击，以及最小结束回合入口
  - 验证方式：Presentation、GameLogic、Editor 与 Battle.Tests 编译通过；开发者已在 Unity Play Mode 手动验证未选择拒绝、当前单位选择、格子移动、相邻攻击、非法攻击、血条刷新、行动结束和阵营切换后的重新选择，结果均正常
  - 备注：已将输入检测改为 Camera 生成射线并通过单次 `Physics.Raycast` 区分单位 Collider 与棋盘 Collider；表现控制器维护当前选择，未选择时拒绝移动和攻击，移动成功后刷新单位位置，攻击成功后刷新目标血条，行动结束后清空选择。Collider 到 `CombatUnit`、`CombatUnit` 到 `CombatUnitVisual` 的映射只存在于表现层；`BattleInputAction` 仅描述本地输入处理结果，不替代 Task 2.6 的结构化战斗结果。先不做技能栏、部署拖拽、移动范围高亮和正式 TEngine `BattleMainUI`

- [x] Task 2.6 战斗结果通知
  - 状态：已完成
  - 完成标准：移动、攻击、扣血、死亡、回合开始 / 结束等行为能以结果对象或本地事件形式返回给表现层
  - 验证方式：Battle、Battle.Tests、Presentation、GameLogic 与 Editor 编译通过；EditMode 单元测试全部通过；Unity Play Mode 已由开发者手动验证移动、攻击、扣血、死亡和回合推进结果均正常
  - 备注：已引入纯 C# `BattleActionResult`、`BattleActionType`、`BattleActionFailureReason` 与 `BattleActionChangeType`；移动、攻击和结束行动统一返回结构化结果，表现层按结果刷新位置、血条和日志。正常玩法拒绝使用失败结果，非法调用仍使用异常；暂不接 TEngine `GameEvent`

- [x] Task 2.7 轻量战斗快照与调试导出
  - 状态：已完成
  - 完成标准：可从纯 Battle 逻辑层导出 `BattleSnapshot` 与关键 `BattleActionLog`；失败测试或手动调试时能输出 JSON 诊断数据，用于离线分析战斗状态
  - 验证方式：Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 编译通过；开发者已确认 Play Mode、EditMode 单元测试和 JSON 文件导出均正常
  - 备注：已完成稳定且大于零的显式 `UnitId`、Registry 唯一性和按 ID 查询、Session 快照与行动日志、版本化 JSON 序列化、Inspector 导出入口及防御性复制测试；Snapshot 记录创建时的完整战场数据，ActionLog 记录每次已提交操作的结果和相关变化。Sprint 2 不实现动态召唤、完整录像、悔棋、正式回放 UI 或权威回放系统

- [x] Sprint 3：占格、攻击范围与目标规则
  - 状态：已完成
  - 总目标：建立统一的动态占格、可移动范围、普通攻击范围和候选目标查询，让 Battle 成为移动与攻击合法性的唯一事实来源，并提供最小范围高亮供 Play Mode 验收
  - 完成标准：存活单位不能初始重叠、不能移动到或穿过其他存活单位；死亡单位释放占格；普通攻击使用显式最小 / 最大范围；Battle 可无副作用查询移动格、攻击格与合法目标；Presentation 只消费查询结果显示高亮
  - 备注：继续使用等代价 BFS 和瞬移表现，不实现地形 Cost、Dijkstra、A*、路径动画、技能、Buff、Modifier、Trigger 或完整目标选择框架；只有出现第二个真实 Selector / Filter 使用方时才提取接口

- [x] Task 3.1 战场占格与位置查询
  - 状态：已完成
  - 完成标准：可查询指定格子的存活单位并判断是否被占用；战斗初始化拒绝存活单位位置重叠；死亡单位不再占格
  - 验证方式：新增 `BattleOccupancyTests`，覆盖空格、三种阵营占格、初始存活单位重叠、死亡释放占格、死亡单位与存活单位共格及越界查询契约；纯 C# NUnit 隔离运行 8 / 8 通过；Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 项目编译通过；Unity Test Runner 单元测试已由开发者手动验证通过
  - 备注：`BattleSession` 新增 `TryGetAliveCombatUnitAt` 与 `IsPositionOccupied`；`SquareGrid` 继续只管理边界和拓扑，不保存单位；占格由存活单位当前位置动态计算，暂不提前引入三层 `SpatialManager`

- [x] Task 3.2 占格感知的移动范围
  - 状态：已完成
  - 完成标准：当前单位自己的起点不算障碍；其他存活单位所在格不可停留且不能穿越；移动查询与最终移动校验使用同一套规则
  - 验证方式：`SquareGridTests`、`MovementResolverTests` 与 `BattleOccupancyTests` 覆盖己方 / 敌方 / Neutral 阻挡、狭窄通道、可绕行路径、目标格占用、死亡单位放行、当前单位起点和失败后状态不变；纯 C# NUnit 隔离运行 102 / 102 通过；Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 项目编译通过；开发者已确认 Task 3.2 验收无问题
  - 备注：`SquareGrid` 的 BFS 新增动态阻挡谓词，`MovementResolver` 的查询和最终移动共同使用该谓词，`BattleSession` 注入存活单位占格事实；继续使用瞬移，不提前实现 A* 或路径动画

- [x] Task 3.3 基础攻击配置与攻击范围
  - 状态：已完成
  - 完成标准：普通攻击由显式伤害、最小射程和最大射程描述；近战、远程和最小射程规则均可验证；失败原因从固定“非相邻”升级为通用“目标超出范围”
  - 验证方式：`CombatUnitTests` 与 `BattleSessionTests` 覆盖攻击配置不变量、最小 / 最大射程边界、过近、过远、曼哈顿距离、成功伤害和失败状态不变，并保留同阵营、死亡、未注册目标与致死伤害测试；纯 C# NUnit 隔离运行 112 / 112 通过；Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 项目编译通过；开发者已手动验证无问题
  - 备注：新增不可变 `BasicAttackData`，`AttackResolver.TryAttack` 使用配置伤害和闭区间射程，范围失败统一为 `TargetOutOfRange`；调试 JSON 因失败原因契约变化升级到 Schema 2；不提前实现多挂载点、切换武器、弹药、冷却、命中率或完整 Luban 武器表

- [x] Task 3.4 范围与候选目标查询
  - 状态：已完成
  - 完成标准：Battle 可查询当前单位可移动格、普通攻击覆盖格和合法目标；查询不修改战斗状态；候选结果与最终行动校验一致
  - 验证方式：`BattleSessionTests`、`MovementResolverTests` 与 `CombatUnitTests` 覆盖边界、阵营、死亡、占格、范围过滤和无效输入契约，并通过 JSON 化 Snapshot 与 ActionLog 验证查询无副作用；纯 C# NUnit 隔离运行 118 / 118 通过；Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 项目编译通过；开发者已在 Unity Test Runner 手动验证通过，本任务未修改表现层，不要求额外 Play Mode 验证
  - 备注：`BattleSession` 作为统一查询入口，组合当前行动单位、单位注册表与规则服务；移动范围由 `MovementResolver` 计算，攻击覆盖格由 `AttackResolver` 计算，合法目标与最终攻击共享同阵营、死亡和射程校验；暂不建立完整 `ITargetSelector` / `ITargetFilter`

- [x] Task 3.5 移动与普通攻击范围高亮
  - 状态：已完成
  - 完成标准：选择当前单位后可显示移动范围和普通攻击范围；合法目标有独立标识；移动、攻击、取消选择、结束行动和切换单位后清除旧高亮
  - 验证方式：`BattleRangeHighlightMapperTests` 覆盖移动、攻击、范围重叠、合法目标优先级、重复位置、空输入、非法输入和查询集合不变；纯 C# NUnit 隔离运行 123 / 123 通过；Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 项目编译通过；开发者已在 Unity Play Mode 验证范围、颜色、坐标、持续选中和手动结束行动流程
  - 备注：Presentation 通过 `BattleSession` 权威查询生成高亮映射，`BattleRangeHighlighter` 缓存场景模板实例并使用 `MaterialPropertyBlock` 刷新颜色；移动或攻击后保持当前单位选中并刷新范围，结束行动或单位切换后恢复默认网格颜色；Test 场景与材质已随提交 `e8a8de6` 保存，不接正式 TEngine `BattleMainUI`

- [~] Sprint 4：最小主动技能闭环
  - 状态：进行中
  - 总目标：在 Sprint 3 的范围和目标规则上增加第一个可操作、可测试、可记录的单体主动技能，并建立由临时聚合场景 SO 驱动、可供后续技能、状态和 Buff 持续回归的固定 2v2 最小对局；补齐行动内位置调整、可扩展行动选择状态、双路径操作面板、取消回退和地格焦点反馈
  - 完成标准：技能具备定义、目标规则、固定 AP 消耗和直接伤害；AP 不足或目标非法时不消耗资源；成功结果能驱动表现并进入 ActionLog / Snapshot / JSON 调试导出；Battle 权威层能够判定阵营全灭、胜方和对局结束，结束后拒绝继续操作；`Test.unity` 通过 Inspector 引用的聚合场景 SO 创建对局，能够调节 AP、耐久、移动力、基础攻击和技能参数，并完成固定 2v2 手动对局、显示结果和重新开始；每次机兵行动以行动开始位置为移动范围锚点，在未锁定行动或目标前可在该固定范围内反复调整站位，成功武器攻击或主动技能会自动结束当前机兵行动；支持行动优先与目标优先两种选择顺序，当前接入基础武器和主动技能两类行动来源，进入行动锁定后只能确认或取消；当前焦点地格具备不覆盖范围语义的独立选中框
  - 任务进度：
    1. [x] Task 4.1 最小 `AbilityDefinition` 与 `TargetRule`
    2. [x] Task 4.2 `AbilityResolver` 与单体直接伤害技能
    3. [x] Task 4.3 仅服务首个主动技能的固定 AP 消耗与行动开始恢复
    4. [x] Task 4.4 技能 Result、ActionLog、Snapshot 与 JSON 诊断
    5. [x] Task 4.5 临时技能按钮、目标选择和技能范围高亮
    6. [x] Task 4.6 最小对局生命周期与胜负判定
    7. [ ] Task 4.7 临时聚合场景 SO 与配置校验
    8. [ ] Task 4.8 行动内位置调整与行动结束规则
    9. [ ] Task 4.9 可扩展行动选择状态机、输入锁定与取消回退
    10. [ ] Task 4.10 行动优先/目标优先双路径面板与默认基础武器入口
    11. [ ] Task 4.11 独立四角地格选中框与焦点反馈
    12. [ ] Task 4.12 固定 2v2 战斗实验场景与重新开始
  - 当前验证：Task 4.6 纯 C# Battle 测试 207 / 207、Unity EditMode 测试 221 / 221 通过；Battle、Battle.Tests、Presentation、Presentation.Tests、GameLogic 与 Editor 项目编译通过，其中 GameLogic 仅保留既有 `CS8632` 警告；开发者已完成 Task 4.6 的 Unity Play Mode 手工验收
  - Task 4.5 当前内容：Battle 提供无副作用的技能范围与合法目标查询，并增加区分单位行动结束与阵营回合结束的权威入口；Presentation 使用“选择技能 → 点击合法目标 → 确认释放”的两阶段技能交互，已选目标使用独立高亮；全部临时操作迁移到运行时 UI Toolkit 战斗 HUD，按“左上单位状态、顶部回合、右下行动、右上折叠诊断”组织；显式指定运行时字体，避免动态 `PanelSettings` 无主题时文字不可见；`TempGameEntry` 拆分启动编排、调试动作和单位状态表现；Inspector 改为 UI Toolkit 配置视图，不再承担运行时按钮；单位血条下方显示分段 AP 条（与血条等宽，按上限分格，有 AP 为黄、空缺为灰，技能扣除和行动/回合推进恢复时同步）；调试 JSON Schema 因新增 `EndFactionTurn` 升级为 4
  - Task 4.6 当前内容：新增不可变 `BattleOutcome` 与 `BattleEndReason`，由 `BattleSession` 在 TeamA / TeamB 最后一名机兵受到普通攻击或主动技能致死伤害后立即记录胜方、全灭方和结束原因，Neutral 不参与胜负；最后一击 Result 按伤害、死亡、阵营全灭、对局结束顺序记录变化，ActionLog、Snapshot 与 JSON 同步携带 Outcome，调试 JSON Schema 升级为 5；对局结束后移动、攻击、技能、结束单位行动和结束阵营回合统一返回 `BattleAlreadyEnded` 且不改变位置、耐久、AP 或回合，行动范围与候选目标查询返回空集合；Presentation 清理选择和高亮、拒绝后续棋盘输入，临时 HUD 禁用战斗交互按钮并保留日志与导出；已覆盖 TeamA / TeamB 获胜、非最后单位死亡、Neutral 死亡、结束后无副作用、变化顺序、Outcome 不变量和序列化测试，Play Mode 已验证最后一击、结束后输入拒绝与调试导出
  - Task 4.7 计划范围：新增一个位于 Temporary 边界内的聚合场景 ScriptableObject，由 `Test.unity` 中的 `TempGameEntry` 通过 Inspector 直接引用，不使用 `Resources.Load`；配置包含棋盘尺寸、固定 2v2 双方机兵的稳定 ID、名称、阵营、初始位置、耐久、AP、移动力、攻击力、基础攻击最小/最大射程，以及主动技能列表中每个技能的名称、AP 消耗、伤害、最小/最大射程和目标规则；启动与重新开始时只读取配置并创建全新的 `PilotData`、`MechData`、`AbilityDefinition` 和 Runtime，运行中不得回写 SO；显式校验非正棋盘尺寸、重复 ID、越界或重复占格、负数数值、非法射程与阵营数量；缺少配置时明确报错并停止启动，不保留隐藏硬编码回退；本任务不把角色、机兵、武器和技能拆成多套独立 SO，也不替代 Sprint 11 的 Luban 正式配置
  - Task 4.8 计划范围：在 Battle 权威层记录当前机兵的行动开始位置，并以该位置生成本次行动固定的可达范围；在尚未锁定武器、技能或攻击目标时，允许机兵在这份固定范围内反复调整最终站位，不能从每次新位置重新获得完整移动力；移动不消耗 AP，也不妨碍之后使用当前仍合法的行动；锁定行动或目标后禁止继续移动，取消时保留当前站位并重新开放原固定范围内的位置调整；成功武器攻击、主动技能或待机后结束当前机兵行动并推进到下一可行动机兵；当前不实现击杀再动、无条件再动或通用行动配额；补齐固定范围内多次调整、越界拒绝、锁定与取消、移动后行动、成功推进及失败不推进测试
  - Task 4.9 计划范围：建立与具体 UI 布局和行动来源解耦的显式交互状态，只区分机兵行动列表、行动已选待目标、目标已选待行动，以及行动与目标均已选择待确认，不建立武器专用或技能专用状态；当前行动选项接入基础武器攻击与主动技能，并预留未来增加左右手武器、道具、支援、交互、警戒或防御时无需重写状态流；行动目标模型预留单位目标、地格目标和自身/无目标类别，但 Sprint 4 只实际接入现有单位目标技能；支持行动优先和目标优先两种选择顺序；锁定状态下禁止点击空地移动，只允许确认或取消；取消返回机兵行动列表，成功行动交由 Task 4.8 自动结束当前机兵行动；补齐状态转换和失败无副作用测试
  - Task 4.10 计划范围：基于 Task 4.9 状态驱动临时 UI Toolkit HUD，并验证行动优先与目标优先两条 UI 路径；选中机兵后，右侧偏上显示一个默认选中的基础武器栏，右下水平显示技能列表与大号“待机”；点击基础武器栏按场景 SO 转换得到的 `BasicAttackData` 射程进入目标选择，点击技能按配置技能射程进入目标选择；先选目标时，右侧纵向显示基础武器攻击及该目标可用技能；目标与行动均有效时同时显示取消和确认/攻击；锁定后隐藏移动范围，取消时恢复行动开始位置锚定的固定移动范围；“待机”结束当前机兵行动，左下全局面板只保留结束当前阵营回合；本任务不提前实现左右手、装备槽或通用武器系统
  - Task 4.11 计划范围：在 Presentation 增加独立于范围底色的地格焦点表现，使用四角开放式选中框而非完整闭环；选中机兵时定位到机兵格，普通地格点击时定位到所点地格，锁定攻击或技能目标时固定到目标格，取消时回到当前机兵格，结束行动或清除选择时隐藏；选中框不得覆盖移动、攻击、技能范围和合法目标颜色，不参与射线检测；首版使用高对比静态颜色，不加入脉冲动画、贴图资源或正式美术特效
  - Task 4.12 计划范围：使用 Task 4.7 的聚合场景 SO 将 `Test.unity` 从硬编码 1v1 调整为固定 2v2 战斗实验场景，双方机兵均可手动选择和操作，能够完整经历多机兵行动顺序、多轮阵营切换、移动、基础武器攻击、现有主动技能、死亡和一方全灭；HUD 显示胜负结果并提供重新开始，重新开始后机兵、耐久、AP、位置、回合、选择、高亮、日志和对局状态全部从同一 SO 恢复初始值；建立可供纯 C# 对局级回归测试使用的等价固定构建方式，但不把临时场景 SO 升级为正式关卡 Schema；本任务不接敌方 AI、TEngine Procedure、Luban、部署、关卡选择、剧情、掉落或正式结算 UI
  - Task 4.4 完成内容：扩展 UseAbility / InsufficientActionPoint / ActionPointSpent / ActionPointRestored；BattleActionResult 增加 AbilitySucceeded / AbilityFailed 与 AP 字段；BattleActionLog 与 BattleUnitSnapshot 同步技能身份与 AP；BattleSession.TryUseAbility 接入 AbilityResolver 并在结束行动时记录 AP 恢复；调试 JSON Schema 升级为 3；补齐 Result、Session、Snapshot 测试
  - Task 4.3 完成规则：
    - AP 归属 `Pilot`；移动继续不消耗 AP，本任务也不改变普通攻击规则
    - `Pilot.MaxActionPoint`、局内当前 AP 与技能消耗均为非负整数；每个技能只配置一个 `ActionPointCost`，首个实际技能使用正消耗，数据模型允许后续出现 0 AP 技能
    - Task 4.3 阶段的技能结算入口允许在 AP 足够时重复调用，AP 恰好等于消耗时允许释放并降为 0；Task 4.8 接入最小行动结束规则后，一次成功武器攻击或主动技能会结束当前机兵行动，AP 只作为行动费用，不再代表同一次行动内可连续释放，后续再行动需重新获得机兵行动机会
    - 空参数和未注册单位仍属于调用错误；死亡、同阵营、棋盘外、射程非法和 AP 不足属于正常玩法失败
    - 所有目标与 AP 校验必须先无副作用完成；成功时先原子支付 AP，再结算伤害，任一失败都不能扣 AP 或修改目标耐久
    - 当下一个存活单位成为 `CurrentCombatUnit`、开始自己的行动时，将其 AP 恢复到 `Pilot.MaxActionPoint`；首个行动单位由 `CombatUnitFactory` 以满 AP 初始化
  - Task 4.4 已确认玩法规则：
    - BattleSession 提供技能释放入口，返回结构化 BattleActionResult 并写入 ActionLogs
    - 成功技能结果记录技能 ID、目标、Actor AP 前后值、目标耐久前后值，以及 ActionPointSpent / DamageApplied（致死时再加 UnitDied）
    - 失败技能结果保留技能 ID 与目标身份，失败原因可区分 AP 不足、同阵营、死亡、越距等；失败不修改 AP 与耐久
    - 单位快照与 JSON 导出补充当前 AP / 最大 AP；结束行动时若下一单位 AP 被恢复，结果记录该恢复前后值
    - 调试 JSON 因契约扩展升级 Schema 版本；空技能、空目标、未注册目标仍属调用契约异常
  - Task 4.4 开发范围：扩展 BattleActionType / FailureReason / ChangeType；扩展 BattleActionResult / BattleActionLog / BattleUnitSnapshot；BattleSession 接入 AbilityResolver 并返回技能 Result；扩展 JSON 序列化与对应测试
  - Task 4.3 完成内容：扩展 `AbilityDefinition` 固定 AP 消耗；为 `PilotData` / `PilotRuntime` 建立 AP 非负不变量，并为 Runtime 增加查询、原子支付和恢复方法；由 `AbilityResolver` 编排“校验目标与余额 → 提交消耗 → 伤害”；由 `TurnCoordinator` 在单位行动开始边界恢复 AP；补齐 Data、Runtime、Resolver 和 Turn 测试
  - 备注：首个技能不附带 Buff，不实现 Effect 列表、Modifier、Trigger、完整 GAS 或正式技能栏；Task 4.3 不建立通用 `BattleResource` / `SkillCost[]`，Sprint 7 再将固定 AP 规则迁移为通用资源管线；仅当首个真实技能明确需要召唤时，才在本 Sprint 验收后追加动态召唤垂直切片

- [ ] Sprint 5：地形 Cost 与路径查询
  - 状态：未开始
  - 总目标：在占格与范围规则稳定后引入最小地形数据、加权移动范围和单目标最优路径
  - 完成标准：格子具备地形类型、移动 Cost 和通行性；Dijkstra 计算全部可达格；A* 返回到指定目标的最低 Cost 路径；算法结果可通过纯 C# 测试和最小调试表现观察
  - 计划任务：
    1. Task 5.1 `GridCell`、`TerrainType`、`MovementCost` 与 `IsWalkable`
    2. Task 5.2 Dijkstra 加权可达范围
    3. Task 5.3 A* 单目标路径查询
    4. Task 5.4 地形 Cost 与路径调试可视化
  - 备注：BFS 继续负责等代价场景，Dijkstra 负责带权范围，A* 负责单目标路径；单位仍保持瞬移，不在本 Sprint 引入路径动画、`BattleDirector`、Elevation、Locomotion、ZOC 或 LOS

- [ ] Sprint 6：机甲战斗深度
  - 状态：未开始
  - 总目标：补回原主计划中因实际 Sprint 2 改为战斗流程闭环而尚未实施的机甲核心机制
  - 计划任务：
    1. Task 6.1 角色与机兵的战斗属性合成边界
    2. Task 6.2 躯干 / 左臂 / 右臂 / 腿多部位耐久与溢出转移
    3. Task 6.3 最小可追踪来源的 GameplayTag 容器
    4. Task 6.4 部位破坏状态及武器 / 移动力联动
    5. Task 6.5 最小机型、武器数据与部位挂载关系
  - 备注：不照搬原“简单 HashSet Tag”；Tag 的授予与移除必须能按来源撤销。完整 GameplayAttribute、Modifier、背包、元件和养成继续后置

- [ ] Sprint 7：资源、命中与确定性结算
  - 状态：未开始
  - 总目标：完善 AP / PP 资源、技能成本、命中率、确定性 RNG 和最小伤害 / 部位命中管线
  - 计划任务：
    1. Task 7.1 将 Sprint 4 的固定 AP 规则迁移为通用 `BattleResource` 与 `SkillCost[]`
    2. Task 7.2 通用 AP 回合回复、PP 固定额度与 PP 转 AP
    3. Task 7.3 确定性 `BattleRNG`、种子 / 游标与命中率
    4. Task 7.4 部位命中权重与最小伤害公式
    5. Task 7.5 仅在流程参数和中间状态确实膨胀时引入最小 `AttackContext` / `DamageContext`
  - 备注：所有概率、倍率和取整继续使用整数规则；不为未来 Hook 机械建立完整五步 Pipeline

- [ ] Sprint 8：高级空间规则与最小 PVE AI
  - 状态：未开始
  - 总目标：在地形、命中和部位规则稳定后补齐移动能力、高低差、ZOC、LOS、掩体与最小敌方决策
  - 计划任务：
    1. Task 8.1 Elevation 与 Locomotion 通行规则
    2. Task 8.2 ZOC 移动 Cost 增量
    3. Task 8.3 LOS 阻挡、掩体命中惩罚与高地射程
    4. Task 8.4 贪心 AI：靠近合法目标并执行可用攻击
    5. Task 8.5 仅在第二种真实拓扑确定后评估 `IMapTopology`
  - 备注：`IMapTopology` / Hex 不是承诺任务；若始终只有 Square，则继续保留具体实现

## P2 / Sprint 9~12：工程化、状态与回溯

- [x] TEngine 框架接入
  - 状态：已完成
  - 完成标准：项目已接入 TEngine 框架及依赖库
  - 验证方式：历史提交记录确认
  - 备注：对应历史提交 `dbd147e 接入 TEngine 框架以及依赖库`；后续 Sprint 9 只接正式战斗流程、UI 和资源边界，不重复执行框架接入

- [ ] Sprint 9：正式战斗流程与部署
  - 计划内容：TEngine Procedure 战斗流程、正式 `BattleMainUI`、BattlePreparation / Deployment、Presenter / View 边界、`BattleDirector` 表现时序与最小战斗配置入口
  - 备注：随机先手、速度先手和剧情指定先手由独立 `InitiativeResolver` 生成行动顺序，不塞入 `TurnCoordinator`

- [ ] Sprint 10：属性、状态、Modifier 与 Trigger
  - 计划内容：`BattleAttributeSet`、来源可追踪的 `BattleStateRuntime`、回合时序调度、Attribute Modifier、必要 Trigger、眩晕 / 断腿 / DOT / 护盾 / 警戒验证样例
  - 备注：Buff 以有生命周期的 State 表达，Modifier 只修改值，Trigger 只做扩展点，不替代移动、攻击和回合主流程

- [ ] Sprint 11：Luban 数据驱动与实体装配
  - 计划内容：Pilot / Mech / Weapon / Ability / State / Modifier 配表、职业系统、角色专属资源、`MechFactory` 多对多装配、完整挂载点和配置校验
  - 备注：先稳定 Runtime 和 Schema，再替换硬编码；编辑器只能生成数据，不能拥有另一套战斗规则

- [ ] Sprint 12：Command、恢复与回放基础
  - 计划内容：`BattleCommand` / `BattleCommandLog`、Command 与 ActionLog / Snapshot 的 Sequence 关联、局部恢复检查点、悔棋、续战存档、本地 Command Replay 与确定性校验
  - 备注：本地点击、选择和未提交到 Session 的拒绝只属于开发期 `InputTrace`；Result Replay、分享回放、PVP 服务端、断线重连和观战继续保留为长期任务

## P3 / Sprint 13+：扩展与商业化

- [ ] 背包词条、元件系统、天赋树、Utility AI、追加攻击、行动配额、HybridCLR 热更新配置、YooAsset 正式资源流、状态透视仪、养成、分享录像与 PVP
  - 备注：天赋触发召唤不直接操作单位注册表，而是通过 Trigger / Ability 复用统一召唤入口；若此前没有主动召唤玩法，则到首个真实需求出现时补齐动态单位生命周期

## 长期 Backlog 与条件式任务

- 动态召唤：首个真实召唤技能出现时，作为独立垂直切片插入，不固定占用某个 Sprint。
- 完整 `IMapTopology` / Hex：只有第二种真实拓扑确定后才实施。
- 正式地形美术、路径动画、移动插值、朝向和弹道导演：逻辑结果稳定且出现表现需求后实施。
- 死亡单位表现注销与预制体回收：首次需要销毁或对象池回收单位表现时实施，统一清理 Collider、Visual、血条与选择状态映射，不纳入 Task 3.2。
- 完整 Buff 叠层 / 刷新 / 驱散 / 免疫 / 光环、复杂 Modifier / Trigger 条件链：Sprint 10 状态主干验收后按真实玩法逐项进入。
- 完整五步 `BattleResolver`、防御 / 暴击 / 条件增伤等伤害公式：Sprint 7 的最小确定性结算出现真实 Hook 和组合需求后再提取。
- 三层 `SpatialManager`：地貌层、附着物层和占据层均出现真实独立数据后再从现有空间规则中提取，不为占格查询提前建立。
- 追加攻击、反击、协力、行动配额、背包、元件、天赋、养成和 Utility AI：依赖 Attribute / State / Modifier / Trigger 与数据装配稳定。
- Result Replay、分享回放、PVP 服务端、断线重连、观战和防作弊：Sprint 12 的 Command / Snapshot / 确定性基础完成后再排期。
- Schema / Validator / Breakdown、沙盒预览、战斗内容编辑器与内容生产工业化：继续遵循 `BATTLE_EDITOR_TOOLING_STRATEGY.md` 的 P4～P6 路线，不纳入近期 Sprint 3～12 验收。

## 旧规划审计与归属

| 旧规划来源 | 尚未完成或已调整内容 | 当前归属 |
|---|---|---|
| 主计划旧 Sprint 2 | 属性合成、多部位、GameplayTag、部位破坏、机型与武器 | Sprint 6 |
| 主计划旧 Sprint 2 | AP / PP、技能成本、命中率、确定性 RNG、部位命中与伤害管线 | Sprint 4 先做最小 AP；完整能力放 Sprint 7 |
| 主计划旧 Sprint 3 | Cost Matrix、A*、TerrainType、Elevation、Locomotion、ZOC、LOS、掩体 | Terrain Cost / Dijkstra / A* 放 Sprint 5；Elevation / Locomotion / ZOC / LOS / 掩体放 Sprint 8 |
| 主计划 P1 AI | 贪心 AI 与长期 Utility AI | 最小贪心 AI 放 Sprint 8；Utility AI 保留 Sprint 13+ |
| 主计划旧 Sprint 4～5 | 正式 TEngine 战斗流程、Presenter、State / Attribute / Modifier / Trigger、Luban、装配、Command、悔棋、续战和回放 | 已完成的框架 / asmdef 不重复；其余拆入 Sprint 9～12 |
| 主计划旧 Sprint 6+ | 背包、元件、天赋、追加攻击、行动配额、养成、热更新资源流、分享录像和 PVP | Sprint 13+ 或满足前置条件后再单独排期 |
| 编辑器与参考分析 | TargetValidationReport、Breakdown、Schema、Validator、沙盒和内容编辑器 | 保留 P4～P6 工具链路线，不进入近期机制 Sprint |

主计划保留为历史蓝图，不回写其旧 Sprint 编号；ReferenceAnalyses 继续作为职责和风险参考，不作为任务状态来源。上述映射与后续调整统一以本文件为准。

## 决策记录

| 日期 | 决策 | 原因 | 影响 |
|------|------|------|------|
| 2026-06-13 | 使用独立进度文件，不直接修改主计划 | 主计划保持蓝图稳定，进度文件记录执行状态 | 新会话需读取 `AGENTS.md`、主计划、进度文件 |
| 2026-06-15 | Battle 核心坐标类型命名为 `Vector2Int`，不改为 `SVector2Int` | 保持简洁；若表现层需要 Unity 类型，则显式写 `UnityEngine.Vector2Int` | Battle 内默认使用 `MechStorm.Battle.Foundation.Vector2Int`，跨层代码需避免省略命名空间导致歧义 |
| 2026-06-17 | AI 新建 Unity 文件或目录时不手写 `.meta` 文件 | `.meta` 应由 Unity Editor 自动生成，避免 GUID / 导入配置不一致 | AI 可创建源码文件，但 `.meta` 等用户打开 Unity 后自动生成，再由 AI 检查是否一并提交 |
| 2026-06-18 | 采用 `Data + Runtime` 命名区分静态数据与运行时数据 | `Data` 表示静态基础数据，`Runtime` 表示某场战斗中会变化的实例状态 | `PilotData` / `MechData` 不承载当前值，当前 AP / 当前耐久放入 `PilotRuntime` / `MechRuntime` |
| 2026-06-18 | 业务含义数字使用命名常量，坐标方向字面值集中管理 | `1` 在攻击距离中表示“相邻格”，属于业务规则；方向向量中的 `1/-1` 表示坐标单位，可集中在方向数组中 | `AttackResolver` 使用局部常量表达相邻距离；后续若引入武器/技能范围，再迁移到 `Data` 配置 |
| 2026-06-26 | 战斗系统长期架构以轻量 GAS 变种为主干，借鉴 `GameObject + State + Attribute + Modifier + Trigger` 思路作为补充，按阶段渐进引入 | 该思路适合支撑后续机甲状态、装备、地形、触发器与复杂技能扩展；当前 P0 不应提前完整照搬，且该规划属于前期方案，后续可随玩法验证调整 | 新增 `BATTLE_ARCHITECTURE_ROADMAP.md` 作为长期拓展规划；P0 继续优先完成可玩闭环，后续按复杂度逐步引入 GameplayTag、GameplayAttribute、轻量 State、流程上下文、Modifier 与 Trigger |
| 2026-06-26 | 战斗逻辑层优先采用整数化数值规则，浮点只用于表现层 | PVP 服务端权威结算、悔棋、续战、录像和回放都需要确定性；整数化概率、倍率、减免和取整策略更容易复现与校验 | 百分比、倍率、固伤率等用万分比或整数缩放表达；所有非整数结果必须声明取整时机和方式；定点数库暂不作为 P0 必需项，后续如出现实时 Lockstep 或连续物理逻辑再评估 |
| 2026-06-27 | AI 默认只提供设计思路、职责划分、接口建议与验证方式，不直接修改代码 | 当前阶段开发者希望先自行实现代码，AI 作为方案讨论与校验辅助，避免未确认方案被提前写入项目 | 除非开发者明确要求“帮我实现 / 帮我改 / 提交”，AI 不主动编辑代码；若涉及文档记录、进度同步或提交，也应先确认意图 |
| 2026-06-28 | 阶段完成后打版本标签，后续功能通过临时分支开发 | P0 / Sprint 1 已形成可运行小版本，需要稳定主线并方便回溯；后续直接在 `main` 开发风险较高 | 当前版本建议标记为 `v0.1.0-p0-sprint1`；后续从 `main` 新建 `feature/<阶段或功能名>` 分支开发，验证通过后再合并回 `main` |
| 2026-06-28 | P1 / Sprint 2 继续使用临时入口，不立即接 TEngine 正式流程 | Sprint 2 的重点是 Battle 纯 C# 核心机制，过早接 Procedure、BattleModule、正式 UI 和资源加载会增加干扰 | `TempGameEntry` 继续作为测试入口；TEngine 正式流程、BattleMainUI、Prefab 动态加载与对象池放到 P2 或机制稳定后处理 |
| 2026-06-28 | Buff / Modifier / Trigger 后置，技能雏形可在普通攻击稳定后引入 | Buff 会牵扯持续时间、叠层、触发时机、属性修正、驱散和免疫，过早引入会让基础战斗流程复杂化 | Sprint 2 不做 Buff / 技能；Sprint 4 做最小主动技能；Sprint 10 再引入 State、Modifier、Trigger 与轻量 GAS 变种 |
| 2026-06-28 | 输入 UI 分阶段接入，先做最小战斗输入 | 技能栏、目标选择、部署拖拽和正式 UI 都依赖稳定的战斗流程和单位管理 | Sprint 2 只做点击单位、点击格子移动、点击敌方普通攻击与结束回合；Sprint 4 做临时技能按钮和目标选择；部署拖拽放入 Sprint 9 的 BattlePreparation / Deployment |
| 2026-06-28 | 轻量战斗快照与调试导出排入 Sprint 2 后段 | 离线调试需要依赖 BattleController、多单位、回合推进和结果通知，太早做会缺少稳定数据源；但等到 P2 再做会降低后续逻辑调试效率 | Sprint 2 在 Task 2.6 之后追加 Task 2.7，只做 `BattleSnapshot`、关键 `BattleActionLog` 和 JSON 诊断导出；正式 Command、恢复、悔棋和本地回放放到 Sprint 12 |
| 2026-07-12 | 分离持久阵营、流程阶段、行动角色与本地敌我视角 | `Player / Enemy` 是客户端相对关系，不能作为 PVP 服务端状态和回放日志中的稳定身份；`Attacker / Defender` 也只描述单次行动 | Task 2.2 使用 `TeamA / TeamB / Neutral` 表达稳定阵营，Task 2.3 的流程阶段不再充当阵营参数；Task 2.7 与后续 PVP 回放记录统一的客观权威日志，客户端按观察者阵营映射 `Ally / Enemy / Neutral` |
| 2026-07-15 | `CombatUnit` 使用本场战斗内稳定的显式 `UnitId`，动态召唤按真实玩法需求分阶段接入 | Snapshot、日志和回放不能依赖运行时对象引用；召唤同时影响注册、占格、回合队列、结果、表现和恢复，不应在 Task 2.7 提前实现，也不应等到天赋开发时临时拼接 | Task 2.7 只完成初始单位 ID、唯一性和查询；若 Sprint 4 的首个真实主动技能需要召唤，则在最小技能闭环验收后追加独立垂直切片，否则继续后置。天赋触发召唤在 P3 通过 Trigger / Ability 复用统一召唤入口 |
| 2026-07-15 | 正式 Command、回放与 PVP 使用 `BattleCommandLog + BattleActionLog + BattleSnapshot` 分层记录 | 输入意图、权威结算事实和世界状态解决的问题不同；把本地点击拒绝混入权威日志会污染回放，把请求参数只放在 Result 中又无法解释失败操作原本想做什么 | Task 2.7 继续只做 ActionLog 与 Snapshot；Sprint 12 增加 BattleCommandLog 并关联三层数据。未进入 Session 的 UI 输入拒绝保留为非权威 InputTrace，调用契约异常进入错误日志 |
| 2026-07-16 | Battle 与 Presentation 按职责目录和命名空间归类，保持现有 asmdef 边界 | 原 `MechStorm.Battle.Combat` 已同时容纳单位、回合、规则、行动结果和调试数据，无法继续表达职责；Sprint 3 会增加空间和范围脚本，后续 Sprint 还会增加技能脚本 | Battle 使用 `Actions / Diagnostics / Foundation / Numeric / Rules / Snapshots / Spatial / Turns / Units`，`BattleSession` 保留在根命名空间；Presentation 使用 `Board / Controllers / Input / Units`，测试目录同步镜像，不拆分程序集 |
| 2026-07-18 | P1 执行顺序调整为“范围 → 技能 → 地形”，并补回旧规划遗漏 | 实际 Sprint 2 已完成战斗流程、Result、Snapshot 与日志，与主计划旧 Sprint 2 的属性、部位、资源和武器任务不同；把范围、技能、地形和复杂机甲机制塞入同一 Sprint 会扩大返工风险 | Sprint 3 做占格与范围，Sprint 4 做最小技能，Sprint 5 做 Terrain Cost / Dijkstra / A*，Sprint 6～8 补机甲深度、资源命中和高级空间规则；原 P2 工程化顺延到 Sprint 9～12，主计划保持历史蓝图，当前执行以本文件为准 |
| 2026-07-18 | Sprint 3 继续瞬移，A* 放到地形阶段 | 当前没有路径预览、逐格移动动画或 AI 路径消费方，等代价格子的范围查询由 BFS 即可正确完成；A* 需要先有稳定通行和 Cost 规则 | Sprint 3 不实现 A*；Sprint 5 使用 Dijkstra 计算带权可达范围，A* 只负责单目标最低 Cost 路径 |
| 2026-07-22 | 玩法任务先以可观察需求卡描述，再映射为开发任务 | 仅列类名、模块名和架构目标无法让开发者还原具体玩法、成功失败规则与状态时序，容易在编码阶段猜测策划意图 | 后续玩法任务默认说明玩法目的、数值示例、成功失败边界、状态流程、开发职责、验收测试与非目标；通用模板记录在 `ENGINEERING_COLLABORATION_GUIDELINES.md` |

## Git 版本管理约定

- `main` 保持可运行、可回退的稳定版本。
- 每完成一个阶段性小版本，在 `main` 当前提交上创建 annotated tag。
- P0 / Sprint 1 建议标签：`v0.1.0-p0-sprint1`。
- 后续功能开发从 `main` 新建临时分支，例如：`feature/p1-sprint2-battle-controller`。
- 分支内完成实现、编译验证、必要运行验证后，再合并回 `main`。
- 若需要同步远程，先推送功能分支或合并后的 `main`，不要在未验证状态直接污染主线。

## 阻塞与风险

| 日期 | 问题 | 影响 | 处理方案 | 状态 |
|------|------|------|----------|------|

## 下一步

1. 开始 Task 3.4，为当前单位提供无副作用的移动格、普通攻击覆盖格与合法目标查询。
2. 保证查询结果与最终移动、攻击校验使用同一套 Battle 规则。
3. 不提前实现范围高亮或完整 TargetSelector / Filter。
