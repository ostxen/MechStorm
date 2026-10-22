# MechStorm 开发进度

## 当前阶段

- 当前里程碑：P1.5 / Sprint T1（进行中）；Sprint 4 已完成并验收
- 当前任务：T1 后续排期与八档镜头范围已确认；下一实施切片为 T1.10.1 胜利/失败结果页，尚未开始实现
- 当前状态：既有异常退出源码/规范与静态核对完成，仍待开发者 Unity 手动验收；本次仅更新文档，不视为完整回归通过
- 最后更新：2026-10-22

### T1 收尾与后续制作排期（2026-10-22，用户批准；尚未实施）

执行顺序：**结果页 → 重战/返回 → 统计 → 最小 Luban 迁移 → 基础战斗镜头 → T1 其余缺口与完整回归 → 地形与寻路 → 后续战斗机制**。本节覆盖旧“先在 T1.4 前迁移 Luban”和四档镜头安排；保留既有任务编号，追加 T1.13，不重写主计划旧 Sprint 编号。角色库、上下阵与拖拽布阵仍归 Sprint 9，不提前。

| 阶段 | 顺序与任务 | 交付与验收节点 | 粗估有效工作日 |
|---|---|---|---|
| A1 | T1.10.1 胜利/失败结果页 | 从 Session.Outcome 映射玩家胜败，停止战斗输入且不再播放下一阵营提示；程序异常不伪装为战败；不加奖励、经验、掉落或评级 | 1～2 |
| A2 | T1.10.2 重战、返回与再次进入 | 释放旧场后按同一配置创建全新 Session，恢复站位、耐久/AP、回合、日志、选择与高亮；返回后再次进入无旧对象/回调 | 2～3 |
| A3 | T1.10.3 只读统计 | 按单位统计实际造成/承受伤害和击杀，过量伤害不计；统计关闭回到结果页，不修改战斗状态 | 1～2 |
| B | T1.12.1～T1.12.4 最小 Luban 迁移 | 数据工程/导出链 → 异步加载 → 装配来源替换 → 迁移等价与重战隔离；A 阶段流程不变，只替换配置来源 | 3～5 |
| C1 | T1.8 八档观察方位与行动者居中 | 相邻档位 45°、共 8 档，循环回初始方位；固定配置俯角，每个角色开始行动时居中；射线/高亮/血条朝向正确 | 1～2 |
| C2 | T1.13.1 镜头拖拽平移 | 棋盘平面平移、观察焦点边界、点击/拖拽区分；拖动不提交选择、移动或攻击 | 1～2 |
| C3 | T1.13.2 限幅滚轮缩放与输入整合 | 最近/最远限制、俯角不变；UI 与流程阶段输入隔离，重战/离场正确恢复和释放镜头状态 | 1～2 |
| D | T1.4/T1.7/T1.9 补漏与 T1.11 回归 | 固定队伍信息、现有武器/技能路径、角色指令实际状态、各窗口输入隔离、配置错误、八档镜头及反复进出；按验收证据结束 T1 | 2～4，新增功能缺口另估 |
| E | Sprint 5 地形与寻路 | 通行性/地形 Cost → Dijkstra 可达范围 → A* 路径 → 调试可视化 → 按需扩充 Luban 地形数据；单位仍瞬移 | 4～7 |

- 阶段 A 先形成胜利/失败、统计、重战、返回/再次进入的基线，再迁移配置，便于检查等价性。当前异常返回只打开 MainUI，不加载主场景；A2 开工前确认正常返回是否实际切回 main 场景，不将两者混同。
- B 放在寻路和新技能内容前，避免新增数据继续堆入 SO 后再次迁移。本会话项目范围搜索只定位到 Skill 示例数据工程，未定位到正式战斗 luban.conf、ConfigSystem/Tables；需先核实/建立工具链，额外排障时间另计。只迁移当前关卡、初始阵营/站位、驾驶员、机兵、普攻及现有技能，不同时改数值和玩法。
- B 中生成代码归 GameProto，数据归 YooAsset；GameLogic 将配置转换为既有 Battle Data，Battle 不引用 Luban/Unity/GameProto。采用异步加载并明确资源持有与释放，不直接照搬同步懒加载模板；场景引用、材质和镜头调试参数留在表现配置。生成、导表和 Unity 导入仍需开发者确认，不自动执行。
- C 的最终范围为八档（每档 45°），不是讨论过程中的四档 90°。初始方位可配置，默认 45°俯角仅配置可改；手动旋转只改变观察方位，不改变角色逻辑朝向。居中保留当前角度与缩放，手动平移后不每帧拉回，下一个角色开始行动时再居中；同阵营内换角色也要触发，不仅阵营交接。
- C 优先保证棋盘边缘行动者居中，平移限制观察焦点范围，不强求整个画面永不露出棋盘外。拖拽键位、角色移动后的跟随方式和具体缩放/边界参数在镜头开工时确认。当前棋盘在左键按下时提交点击；若使用左键拖拽，必须先区分点击/拖拽再提交，不能拖完才撤销已发生的战斗操作。UI 上的手势不穿透，提示/结算/离场的镜头启停规则一并明确。
- D 不把隐藏角色指令区域当成完成；若补指令需要新技能机制，独立拆任务与估时，不为清空任务表提前搭建完整技能系统。旧 UI Toolkit HUD 已静态退役，不恢复或重复删除。既有异常退出及完整回归待验收项仍保留。
- E 不加入逐格移动动画、表现导演、高低差、ZOC 或 LOS。后续同类单体技能优先复用现有契约和 Luban 增加内容；新效果按具体玩法拆分，多部位/资源/命中/状态等沿后续 Sprint 渐进实施，不重做已完成的最小技能核心。
- 粗估条件：单人有效开发日，含逻辑、必要 UI 接线和基本自验，不含正式美术、等待资源、长时间联调；T1 收尾约 12～22 日，Sprint 5 另约 4～7 日，八档完整覆盖与工具链/回归新增缺口另计，不是交付承诺或测试执行授权。
- 制作可并行：A1/A2 期间准备结果 Prefab、胜败表现和按钮；A3 准备统计页/条目；B 整理现有数据与稳定 ID；C 准备旋转按钮和操作说明并确认手感；D 按固定清单记录问题，不同时扩展新玩法。代码依赖仍按上表顺序推进。
- 本轮只确认排期与规范，未开始以上实现，未运行编译/测试/游戏/Unity 刷新，未导表、暂存或提交。


### 旧 HUD 清理与场景配置正式化（2026-10-19）

- 按用户授权删除 `MechStorm/Temporary` 下五个旧 HUD 脚本、`TempBattleActionPanelStateTests`、两份主题 SO、runtime tss 及其配对 meta；移除两个确实空的专属目录。正式 Controller、行动状态机、BattleUnitPresenter 与战斗核心测试保留。
- 配置代码保留在 `GameLogic/MechStorm/Config`，命名空间改为 `MechStorm.Config`：`BattleScenarioConfig`、`BattleScenarioBuilder`、`BattleScenarioValidator`、`BattleScenarioBuildResult`、`BattleScenarioValidationResult`，嵌套配置 DTO 同步去掉 Temp 前缀；Runtime/SceneBindings 和四个配置测试文件同步更新，不留旧名兼容包装。
- 原 SO 成对移动为 `Assets/AssetRaw/Configs/BattleScenarioConfig.asset`，配置脚本 GUID `d2a0ee04b44e4bd5a84eeb704ef4f8ef`、资源 GUID `9835cf0430e09f9429cc6dc6f6e5b5ba` 不变。全部序列化字段、10×10/四单位/八技能数据、每次构建全新对象且不回写 SO 的语义保留；Validator 仍要求双方各两台、无 Neutral，不扩大为通用关卡规则。
- 静态核对通过：旧类型与11个删除 GUID 在 Assets 无残留；9个改名 C# 文件与2个 Runtime 调用文件相对备份仅有预期名称、命名空间、using、菜单、注释/错误文案变化；10份移动 meta 字节不变；SO 仅改 m_Name，Battle/Test 两场景字节不变；14个配置校验/构建/对局测试用例源码全部保留。
- 未执行编译、测试、运行、Unity 刷新、生成或提交。开发者需确认配置 Inspector 数据与场景引用正常，再验收正式 HUD 的目标优先武器路径、确认/取消/待机及离场重入。此次删除按用户授权提前执行，不代表 T1.11 全流程对齐已验收，也不代表 T1.12 Luban 迁移完成。
- 2026-10-19 开发者反馈“没问题了”，确认以“正式战斗与 HUD 整批”进行本地提交：包括正式流程、Runtime/HUD、生成绑定、相关场景/Prefab、配置正式化与旧入口/HUD 退役，以及必要资源。排除无关主计划/规范改动、工程设置、IDE、缓存、构建产物和未使用素材；不 push。AI 未执行编译或运行测试，该反馈不扩大为 T1 全流程逐项验收。
- 提交前静态资源核对发现：旧示例 `Assets/AssetRaw/UI/BattleMainUI.prefab` 仍有一处缺失 Sprite GUID `b475dce438fe0f54aa0dbd4c905621ec`，在 HEAD 原位置已存在，不是本轮迁移引入；正式入口已不再加载该示例。保留并记录，不在提交任务中擅自替换用户素材。
- 本批必要依赖通过静态 GUID 与 Shader include/Shader.Find 路径核对纳入，未整包加入第三方素材；保留原资源、meta 和第三方 Shader 的空字段尾空格等格式，完整 staged `diff --check` 因这些空白诊断未通过，不进行无关格式化或手改 meta。仍不将静态资源扫描等同于 Unity 编译/测试或构建验证。

### UI Prefab 阶段性备份（2026-10-11）

- 提交 `3c57d5a`：14 个新 Prefab（StartUI、MainUI、LoadingUI、4 个 Battle 窗口、7 个 Battle Widget）及配对/目录元数据，共 31 个文件。
- 用户取消素材复制，已删除本次创建的复制工具及配对元数据；素材、业务脚本与其他暂存改动未纳入提交。
- 仅做序列化结构、元数据配对和提交范围静态检查；未运行 Unity、编译或测试。`git diff --check` 提示 Unity 空字段尾空格，保留原序列化内容。
- 依赖素材与自定义组件后续提交，本次不代表 UI 引用完整或 Sprint 验收完成。
- 2026-10-11 用户要求将 `Assets/AssetRaw/UI` 下其余 Prefab 也提交；新增提交 `a46fe00`，包含 BattleMainUI 的目录移动、LoginUI 更新及 ErrorNetworkUI/PopupUpdateUI 新增。与 `3c57d5a` 合计覆盖当前目录 19 个 Prefab。仅静态核对 Prefab/.meta 配对与 GUID；未运行 Unity、编译或测试，素材依赖仍未提交。

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

- [x] Sprint 4：最小主动技能闭环
  - 状态：已完成
  - 总目标：在 Sprint 3 的范围和目标规则上增加第一个可操作、可测试、可记录的单体主动技能，并建立由临时聚合场景 SO 驱动、可供后续技能、状态和 Buff 持续回归的固定 2v2 最小对局；补齐行动内位置调整、可扩展行动选择状态、双路径操作面板、取消回退和地格焦点反馈
  - 完成标准：技能具备定义、目标规则、固定 AP 消耗和直接伤害；AP 不足或目标非法时不消耗资源；成功结果能驱动表现并进入 ActionLog / Snapshot / JSON 调试导出；Battle 权威层能够判定阵营全灭、胜方和对局结束，结束后拒绝继续操作；`Test.unity` 通过 Inspector 引用的聚合场景 SO 创建对局，能够调节 AP、耐久、移动力、基础攻击和技能参数，并完成固定 2v2 手动对局、显示结果和重新开始；每次机兵行动以行动开始位置为移动范围锚点，在未锁定行动或目标前可在该固定范围内反复调整站位，成功武器攻击或主动技能会自动结束当前机兵行动；支持行动优先与目标优先两种选择顺序，当前接入基础武器和主动技能两类行动来源，进入行动锁定后只能确认或取消；当前焦点地格具备不覆盖范围语义的独立选中框
  - 任务进度：
    1. [x] Task 4.1 最小 `AbilityDefinition` 与 `TargetRule`
    2. [x] Task 4.2 `AbilityResolver` 与单体直接伤害技能
    3. [x] Task 4.3 仅服务首个主动技能的固定 AP 消耗与行动开始恢复
    4. [x] Task 4.4 技能 Result、ActionLog、Snapshot 与 JSON 诊断
    5. [x] Task 4.5 临时技能按钮、目标选择和技能范围高亮
    6. [x] Task 4.6 最小对局生命周期与胜负判定
    7. [x] Task 4.7 临时聚合场景 SO 与配置校验
    8. [x] Task 4.8 行动内位置调整与行动结束规则
    9. [x] Task 4.9 可扩展行动选择状态机、输入锁定与取消回退
    10. [x] Task 4.10 行动优先/目标优先双路径面板与默认基础武器入口
    11. [x] Task 4.11 独立四角地格选中框与焦点反馈
    12. [x] Task 4.12 固定 2v2 战斗实验场景与重新开始
  - 阶段后续：Task 4.11 与 Task 4.12 完成并验收后，优先接入 TEngine 正式战斗流程与项目自有 UGUI 战斗界面，不直接进入 Sprint 5；具体迁移任务在 Sprint 4 收尾时拆分
  - 当前验证：Unity EditMode 293 / 293 通过；Battle.Tests、Presentation.Tests、GameLogic 与 Editor 编译及源码与配置 `git diff --check` 通过；开发者已完成固定 2v2 全灭胜负、结果卡、重新开始，以及四台机兵、位置、耐久、AP、回合、阵营、选择、高亮和后续操作恢复的 Play Mode 验收
  - Task 4.5 当前内容：Battle 提供无副作用的技能范围与合法目标查询，并增加区分单位行动结束与阵营回合结束的权威入口；Presentation 使用“选择技能 → 点击合法目标 → 确认释放”的两阶段技能交互，已选目标使用独立高亮；全部临时操作迁移到运行时 UI Toolkit 战斗 HUD，按“左上单位状态、顶部回合、右下行动、右上折叠诊断”组织；显式指定运行时字体，避免动态 `PanelSettings` 无主题时文字不可见；`TempGameEntry` 拆分启动编排、调试动作和单位状态表现；Inspector 改为 UI Toolkit 配置视图，不再承担运行时按钮；单位血条下方显示分段 AP 条（与血条等宽，按上限分格，有 AP 为黄、空缺为灰，技能扣除和行动/回合推进恢复时同步）；调试 JSON Schema 因新增 `EndFactionTurn` 升级为 4
  - Task 4.6 当前内容：新增不可变 `BattleOutcome` 与 `BattleEndReason`，由 `BattleSession` 在 TeamA / TeamB 最后一名机兵受到普通攻击或主动技能致死伤害后立即记录胜方、全灭方和结束原因，Neutral 不参与胜负；最后一击 Result 按伤害、死亡、阵营全灭、对局结束顺序记录变化，ActionLog、Snapshot 与 JSON 同步携带 Outcome，调试 JSON Schema 升级为 5；对局结束后移动、攻击、技能、结束单位行动和结束阵营回合统一返回 `BattleAlreadyEnded` 且不改变位置、耐久、AP 或回合，行动范围与候选目标查询返回空集合；Presentation 清理选择和高亮、拒绝后续棋盘输入，临时 HUD 禁用战斗交互按钮并保留日志与导出；已覆盖 TeamA / TeamB 获胜、非最后单位死亡、Neutral 死亡、结束后无副作用、变化顺序、Outcome 不变量和序列化测试，Play Mode 已验证最后一击、结束后输入拒绝与调试导出
  - Task 4.7 完成内容：新增 `TempBattleScenarioConfig` 聚合 SO 与 Unit、Pilot、Mech、基础攻击、主动技能配置 DTO，`Test.unity` 通过 Inspector 直接引用，不使用 `Resources.Load`；`TempBattleScenarioValidator` 一次性校验棋盘、固定 2v2 阵营数量、ID、枚举、越界、重复占格、空配置、数值与射程，`TempBattleScenarioBuilder` 仅在校验通过后创建全新的 Battle 静态数据、Runtime、四个 `CombatUnit`、单位技能映射和 `BattleSession`，运行中不回写 SO；`TempGameEntry` 移除棋盘、1v1 单位和调试技能硬编码，按构建结果创建 10x10 棋盘、四单位表现、碰撞输入、血条与当前单位首个技能 HUD，缺少或非法配置时明确报错并停止；配置资源固定双方各两台单位，初始位置为 `(2,3)`、`(2,4)`、`(7,7)`、`(7,8)`；新增 13 项 Validator / Builder 测试覆盖合法映射、全量错误、无部分结果、重复构建对象隔离、Runtime 修改不污染后续构建与 SO 不回写；本任务未拆分多套独立 SO，也未替代 Sprint 11 的 Luban 正式配置
  - Task 4.8 计划范围：在 Battle 权威层记录阵营内已行动单位、当前行动机兵及其行动开始位置，并以该位置生成本次行动固定的可达范围；阵营回合内可从当前阵营存活且尚未行动的机兵中自由选择行动者，切换时上一机兵立即回到行动开始位置；在尚未锁定武器、技能或攻击目标时，允许机兵在固定范围内反复调整最终站位，不能从每次新位置重新获得完整移动力；移动不消耗 AP，也不妨碍之后使用当前仍合法的行动；锁定行动或目标后禁止继续移动和切换行动者，取消时保留当前站位并重新开放原固定范围内的位置调整；成功武器攻击、主动技能或待机后提交当前站位、标记当前机兵已行动并推进到下一台未行动机兵；失败攻击或技能不提交、不推进；结束阵营回合时先回退当前未提交移动，再跳过本阵营剩余单位；暂定移动与位置回退通过结构化 Result / ActionLog 保持可观察，不静默留下与最终位置冲突的日志；当前不实现击杀再动、无条件再动、通用行动配额或 Task 4.9 的通用交互状态机；补齐自由选择、切换回退、固定范围内多次调整、越界拒绝、锁定与取消、移动后行动、成功推进及失败不推进测试
  - Task 4.8 完成内容：`TurnCoordinator` 使用已行动单位集合替代固定行动索引，支持当前阵营存活且未行动机兵的自由选择；`BattleSession` 记录行动开始位置，移动查询与执行均固定使用该锚点，切换行动者和结束阵营回合会权威回退未提交走位并记录位置变化；成功基础攻击或主动技能会自动结束当前机兵行动，手动结束行动作为待机入口，失败行动不推进；Snapshot 与 JSON Schema 6 增加行动开始位置和单位已行动状态；Presentation 在未锁定时将己方点击解释为切换行动者，并同步回退 Visual、HUD 与范围，技能或基础攻击目标锁定后拒绝继续移动或切换；自动测试已覆盖固定范围内多次调整、自由选择、已行动拒绝、切换与结束阵营回合回退、成功行动自动推进、失败不推进及诊断数据，Play Mode 手工验收已通过
  - Task 4.8 验收后重构：`BattleActionLog` 改为只接收 `BattleActionResult` 并直接复制 Actor、Target 与 NextUnit 的稳定 ID，基础攻击失败 Result 保留目标身份，`BattleSession.RecordAction` 不再接收旁路 ID；临时 HUD 删除与构造函数非空契约冲突的就绪判断、Presentation 空值后备路径和无生产者的 `TargetAbilityUsed`；自动验证与 Play Mode 冒烟均通过，没有引入新抽象或提前实现 Task 4.9
  - Task 4.9 计划范围：建立与具体 UI 布局和行动来源解耦的显式交互状态，只区分机兵行动列表、行动已选待目标、目标已选待行动，以及行动与目标均已选择待确认，不建立武器专用或技能专用状态；当前行动选项接入基础武器攻击与主动技能，并预留未来增加左右手武器、道具、支援、交互、警戒或防御时无需重写状态流；行动目标模型预留单位目标、地格目标和自身/无目标类别，但 Sprint 4 只实际接入现有单位目标技能；支持行动优先和目标优先两种选择顺序；锁定状态下禁止点击空地移动，只允许确认或取消；取消返回机兵行动列表，成功行动交由 Task 4.8 自动结束当前机兵行动；补齐状态转换和失败无副作用测试
  - Task 4.9 完成内容：新增与具体 HUD 解耦的 `BattleActionSelection`、`BattleActionTargetSelection`、四状态 `BattleActionSelectionStateMachine` 和 Battle 权威查询驱动的 `BattleActionSelectionCoordinator`；基础攻击与主动技能共用行动选择、单位目标、确认和取消契约，目标模型预留地格、自身与无目标类别；`BattlePresentationController` 支持行动优先与目标优先，待选择或待确认期间锁定移动、行动者切换、待机和结束阵营回合，取消后保留当前位置并恢复固定锚点移动范围，成功确认继续复用 Task 4.8 自动推进；行动优先只按对局、行动者、行动归属和资源判断行动能否选取，点击技能后始终先显示技能范围，不依赖范围内已有目标，无目标时确认保持禁用且只能取消；目标优先仍由 Battle 权威查询过滤兼容行动；临时 HUD 增加最小通用取消入口但未提前实现 Task 4.10 正式双路径布局；新增 18 项状态转换、行动查询、无目标选择、失败无副作用、执行重置和高亮测试
  - Task 4.10 计划范围：基于 Task 4.9 状态驱动临时 UI Toolkit HUD，并验证行动优先与目标优先两条 UI 路径；选中机兵后，右侧偏上显示一个默认选中的基础武器栏，右下水平显示技能列表与大号“待机”；点击基础武器栏按场景 SO 转换得到的 `BasicAttackData` 射程进入目标选择，点击技能按配置技能射程进入目标选择；先选目标时，右侧纵向显示基础武器攻击及该目标可用技能；目标与行动均有效时同时显示取消和确认/攻击；锁定后隐藏移动范围，取消时恢复行动开始位置锚定的固定移动范围；“待机”结束当前机兵行动，左下全局面板只保留结束当前阵营回合；本任务不提前实现左右手、装备槽或通用武器系统
  - Task 4.10 当前内容：新增纯 C# `TempBattleActionPanelState`，将 Task 4.9 状态、显式 `ActionFirst` / `TargetFirst` 路径和技能武器依赖映射为 HUD 区域显隐，不保存 HUD 平行选择状态；临时 UI Toolkit HUD 参考《钢岚》布局改为无外层说明面板的独立基础武器栏、右下横向技能图标与大号菱形待机，技能槽按“图标在上、AP 在下”排列并在悬停时显示说明；常态默认武器栏只表示当前装备选择，Sprint 4 无第二把武器时点击仅保留动效，不进入攻击范围或行动状态；目标优先选择武器范围内目标后隐藏常态武器栏，右上以统一技能项样式纵向列出独立的基础武器技能及依赖当前武器的技能，并默认选中基础武器技能，右下立即显示攻击/取消且允许继续切换技能；技能优先路径锁定已选技能，仅显示确认/取消，并只在 `RequiresBasicWeapon` 为真时显示右上依赖武器栏；任一路径进入待确认后，当前行动释放前仍可在其合法目标间切换，非法目标不修改原选择；`RequiresBasicWeapon` 只表达依赖条件和目标优先候选资格，`BattleActionSelection.RangeSource` 显式保证目标优先使用基础武器范围、技能优先使用技能自身配置范围，查询、高亮、确认与结算共用同一来源；当前场景八个技能均依赖基础武器；`BasicAttackData` 的配置 AP 消耗已统一接入查询、实际扣费、Result/ActionLog 与失败无副作用；左下全局控制只保留结束阵营回合
  - Task 4.10 验收后重构：`BattleActionSelection` 统一校验技能范围来源与基础武器依赖组合，并提供唯一行动身份比较；Coordinator 合并行动已选/待确认的目标校验分支并复用默认行动查询，StateMachine 在清除部分选择时同步规范化路径；Controller 将行动优先、目标优先和待确认目标点击合并到单一入口；临时 HUD 适配仅改为消费统一纯 C# 契约，没有调整 UI Toolkit 结构、布局或样式，为后续 UGUI 替换保留稳定逻辑边界
  - Task 4.11 计划范围：在 Presentation 增加独立于范围底色的地格焦点表现，使用四角开放式选中框而非完整闭环；选中机兵时定位到机兵格，普通地格点击时定位到所点地格，锁定攻击或技能目标时固定到目标格，取消时回到当前机兵格，结束行动或清除选择时隐藏；选中框不得覆盖移动、攻击、技能范围和合法目标颜色，不参与射线检测；首版使用高对比静态颜色，不加入脉冲动画、贴图资源或正式美术特效
  - Task 4.11 完成内容：新增 `BattleGridFocusIndicator`，以独立程序化 MeshRenderer 绘制四个开放角（共 8 个矩形段），复用现有高亮材质但通过属性块使用独立高对比颜色，不修改范围底色；焦点对象无 Collider 且位于 Ignore Raycast 层；Presentation 在选中/切换机兵、普通地格点击、行动与目标锁定、取消、行动结束、清除选择和对局结束路径统一刷新或隐藏焦点；新增 3 项 EditMode 测试覆盖位置、显隐、网格结构、射线隔离与参数校验
  - Task 4.12 计划范围：使用 Task 4.7 的聚合场景 SO 将 `Test.unity` 从硬编码 1v1 调整为固定 2v2 战斗实验场景，双方机兵均可手动选择和操作，能够完整经历多机兵行动顺序、多轮阵营切换、移动、基础武器攻击、现有主动技能、死亡和一方全灭；HUD 显示胜负结果并提供重新开始，重新开始后机兵、耐久、AP、位置、回合、选择、高亮、日志和对局状态全部从同一 SO 恢复初始值；建立可供纯 C# 对局级回归测试使用的等价固定构建方式，但不把临时场景 SO 升级为正式关卡 Schema；本任务不接敌方 AI、TEngine Procedure、Luban、部署、关卡选择、剧情、掉落或正式结算 UI
  - Task 4.12 完成内容：`TempGameEntry` 拆出可重复的场景构建、运行时创建与释放流程，重新开始时销毁旧单位与血条、清空输入映射和选择反馈，再从同一聚合场景 SO 创建全新的 BattleSession、四台机兵、能力映射、单位表现、输入、控制器和 HUD 适配；临时 HUD 根据 Battle 权威 Outcome 显示“我方胜利/敌方胜利”结果卡及重新开始按钮；新增固定 2v2 对局级测试，覆盖多轮移动、待机、基础攻击、主动技能、双方行动、死亡与阵营全灭，并断言重建后第 1 回合、TeamA、首台机兵、四台单位初始位置、满耐久/AP、空 ActionLog 和未结束 Outcome 全部恢复
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

- [~] Sprint T1（P1.5）：TEngine + UGUI 正式战斗表现迁移
  - 状态：进行中；开发者已确认入口流程跑通，尚未验收返回/再次进入及完整战斗闭环
  - 排期位置：Sprint 4 完成后、Sprint 5 开始前；本阶段完成前不继续扩展临时 UI Toolkit HUD
  - 总目标：复用 Sprint 4 已稳定的 Battle / Presentation 纯 C# 契约，将固定 2v2 战斗闭环迁入 TEngine 正式流程与项目自有 UGUI Prefab，建立“主场景 -> 战前确认 -> 战斗提示 -> 战斗 HUD/镜头控制 -> 结果/统计 -> 重战或返回”的完整灰盒流程
  - 完成标准：启动后停留在伪主场景；点击战斗按钮进入战斗场景并显示固定队伍预览；点击开始战斗后依次确认全灭敌方的胜利条件和当前阵营回合提示；默认选中当前阵营首台可行动机兵并进入战斗；选择、移动、待机、八档环绕镜头（每档45°）、拖拽平移、限幅缩放、行动者居中、双路径攻击/技能、目标切换、确认/取消全部对齐；结束后显示相对玩家阵营的胜利/失败结果，可查看每单位造成伤害、承受伤害和击杀统计，并可重新战斗或返回主界面；重复进出和流程退出均正确释放 UI 与战斗 Runtime，完成最小 Luban 配置等价迁移
  - 实施原则：
    - Battle 继续持有权威规则，UGUI View 不复制合法性判断、范围计算、AP 扣费、回合推进或胜负规则
    - TEngine 侧通过 `GameModule.Scene`、`GameModule.UI`、`UIWindow` 生命周期和 YooAsset 地址进入正式流程，不绕过框架直接加载
    - Presenter / View 或等价 UI Adapter 只消费 Presentation 契约；战斗 Runtime 的创建、重启与释放有单一所有者
    - 无正式美术素材时使用 UGUI 灰盒 Prefab、纯色 `Image`、文本和几何占位，不以运行时代码临时拼装作为最终 Prefab 方案
    - `UIWidget` 只在子区域确有独立刷新或生命周期时拆分，不为形式提前增加空壳层
    - 现有 `BattleMainUI.prefab` 仅视为 TEngine 示例资源，不作为最终战斗 Prefab；正式 UI 使用 MechStorm 自有命名、层级和生命周期
    - 战前阶段只预览同一场景配置中的固定 2v2 队伍和站位，不实现角色库滑动、上下阵、拖拽布阵或队伍合法性规则
  - 计划任务：
    1. **Task T1.1 伪主场景与战斗入口按钮**
       - 搭建或改造现有 `main.unity` 为最小伪主场景，保留 TEngine 启动所需的 `UIRoot` 和框架对象，不在主场景创建战斗 Runtime
       - 新增 UGUI 主界面灰盒窗口与“进入战斗”按钮，游戏启动后先停留在主场景，由玩家主动点击进入战斗
       - 按钮通过正式流程入口请求战斗切换，切换期间锁定重复点击；验收启动停留、按钮显示、单次跳转和重复点击隔离
    2. **Task T1.2 战斗入口与生命周期基线**
       - 明确并实现 `GameApp / Procedure -> Battle Scene -> BattlePreparationUI -> BattleHUDUI` 的进入、退出和异常回收顺序
       - 确认正式战斗场景、持久 `UIRoot`、YooAsset 场景/UI Collector 与资源地址；保留 TEngine 示例 `BattleMainUI.prefab`，新建 MechStorm 自有灰盒 Prefab
       - 建立可重复进入、返回和再次进入的最小流程切片，验证不会重复创建窗口、残留场景对象或持有失效引用
    3. **Task T1.3 战斗 Runtime Host 抽取**
       - 从 `TempGameEntry` 抽取固定 2v2 的配置读取、Session 创建、单位表现创建、输入装配、重启和释放职责
       - 正式 Host 统一拥有 BattleSession、Presentation Controller、场景单位与表现资源；临时入口只作为迁移期适配，不成为正式 UI 依赖
       - 保持同一 `TempBattleScenarioConfig` 全量重建语义，并补齐创建/释放/重建隔离测试
       - 按以下顺序逐项执行，不一次性迁入所有临时表现和调试工具：
         - [~] **T1.3.1 场景与固定镜头基线**：进入 Battle 后能看到灰盒地面；复用现有 10×10、单格 1、原点 (-5,0,-5) 的布局，配置地面 Renderer / Collider 和与临时场景一致的固定斜俯视相机。先不创建单位、格子高亮或镜头控制脚本。验收：地面构图与临时场景一致，战斗相机与 UI 相机独立。
           - 2026-10-10 已写入场景：Board/Plane 地面与 MeshCollider、Cameras 下 45° 斜俯视相机、地面中心归零、战斗相机剔除 UI 层。仅静态核对，等待开发者横屏运行观察；尚无格线或单位，不标记运行验收完成。
           - 根据开发者反馈拉近镜头：位置改为与 Test 场景相同的 (0,4.65,-6.68)，保留 45° 俯角与 FOV 60°；10×10 地面不变，不再强求全部边界同时入镜。
         - [~] **T1.3.2 正式 Runtime Host**：从 TempGameEntry 抽取场景装配、Session 创建与释放，显式接收场景地面、相机及单位根节点；不复制战斗规则，不带入 UI Toolkit HUD 和临时调试面板。验收：创建/释放职责单一，离场和初始化失败均可回收。
           - 2026-10-11 已写入 `MechStorm/Runtime/BattleRuntimeHost.cs`（GameLogic 程序集）：复用现有 Builder / Validator，持有独立 Session、坐标转换、战斗相机和本场单位根；校验场景依赖与布局参数，创建异常回收、幂等释放并还原地面变换和颜色覆盖。
           - `TempGameEntry` 保留序列化字段与临时调试工具，改为复用 Host；首次初始化、重建和退出统一清理单位、映射、高亮与焦点，缺失依赖中止初始化，重建重新读取同一配置创建全新 Session。单位/血条创建与输入编排尚留在临时入口，后续分项迁移，不代表整个 T1.3 已完成。
           - 仅完成源码静态核对与差异空白检查，未编译、测试、运行或 Unity 刷新，未手写 `.meta`。等待开发者在 Test 场景检查原有战斗操作、扣血后重建恢复初值、初始化失败与退出无残留；正式 Battle 的创建与释放在 T1.3.3/T1.3.4 接入。
         - [~] **T1.3.3 固定 2v2 初始战场恢复**：先读取现有 TempBattleScenarioConfig，单向转换为 Battle Data / Runtime，显示四台灰盒机兵与血条，并把同一战斗相机提供给表现及后续输入。验收：10×10、双方各两台、原始站位与满耐久/AP一致；重建后得到全新 Session。
           - 2026-10-11 已将单位表现创建从 `TempGameEntry` 迁入 `BattleRuntimeHost`：同一份配置经现有 Builder / Validator 单向构建 BattleSession 与四台 CombatUnit，Host 创建单位 Cube、Collider、HealthBarAnchor 和 `BattleUnitPresenter`，统一使用 Host 的坐标转换与战斗相机。
           - `BattleUnitPresenter` 维护四台单位的耐久、AP 血条和朝向刷新；临时调试入口改为消费 Host 提供的单位表现、Collider 映射和 Presenter，未复制单位或血条创建逻辑。输入控制仍暂留临时入口，正式 Procedure 接入放在 T1.3.4。
           - 初始化失败、重建和退出沿用 Host 的幂等清理；单位根禁用后销毁，重新构建得到全新 Session、单位表现、Collider、血条和映射。配置仍是现有临时 ScriptableObject，Luban 替换按 T1.12 执行。
           - 发现正式 `Battle.unity` 原先没有任何组件创建 Host，导致只显示地面。2026-10-11 已将迁移期 `TempGameEntry` 挂到 `BattleWorld`，显式绑定 Battle Plane、战斗相机和同一份配置，并关闭临时调试工具；进入正式场景后会直接创建四台单位与血条。该组件只是 T1.3.4 完成前的场景适配入口，仍待后续由 Procedure 统一接管生命周期。
           - 2026-10-11 正式场景已打开基础 Presentation：进入后自动选中 `CurrentCombatUnit`，通过 `BattleSession` 查询并显示移动范围、普通攻击覆盖范围和当前合法目标；高亮模板由迁移期入口运行时创建，不新增场景资源。主动技能范围继续由 `SelectAbility` 的统一行动契约驱动，待正式 HUD 提供技能选择入口后显示，不在入口代码中复制技能选择状态。
           - 2026-10-11 修复正式场景关闭临时调试工具后输入轮询被一并关闭的问题：正式模式现在由 `TempGameEntry.Update()` 独立调用 `BattlePresentationController.Tick()`，点击合法移动格会提交现有 Battle 移动入口；未复制移动规则。
           - 仅完成源码静态核对，未编译、测试、运行或 Unity 刷新；正式 Battle 场景尚未挂接 Procedure 生命周期，待 T1.3.4 接入。
         - [~] **T1.3.4 正式流程生命周期接入**：由 ProcedureBattle 控制 Runtime 的准备/释放，与 T1.4 的“确认后创建 Runtime”交接；将棋盘、单位等必要准备纳入 LoadingUI 完成条件。配合 T1.2 补返回及再次进入，避免销毁后的引用或重复对象。验收：重复进出无残留、失败不留下半场战斗。
           - 2026-10-18 按用户确认方案将 `BattleRuntimeHost` 更名为 `BattleRuntime`（保留原 GUID），正式 Runtime 统一装配棋盘输入、选择协调和高亮。`ProcedureBattle` 已编写场景依赖检查、Runtime/HUD 创建、就绪等待、更新、失败回收与退出清理；仍沿用现有场景配置，当前加载后直接启动，战前确认时机留待 T1.4 接入。
           - 新增只保存场景引用的 `BattleSceneBindings`；开发者已通过 Unity 完成正式 Battle 场景迁移。随后用户明确要求硬切：删除 `TempGameEntry`、专用 Inspector 和已用完的迁移菜单及各自 meta，Procedure 不再引用旧入口；Test 场景保留原地图/相机/配置参数，改用同一绑定组件，不再保留直接 Play 时自行启动战斗的兼容行为。战斗统一由正式 Procedure 启动，现有配置/Builder/Validator 继续复用，不属于旧入口兼容。
           - 静态检查：Assets 内无旧入口/迁移菜单类型或已删除脚本 GUID 引用；Test 仅修改旧组件及根节点命名两块，31 唯一锚点、本地引用闭合；用户已迁移的 Battle 场景未再改动。未自动编译、测试、运行或 Unity 刷新，重复进出/失败回收与 HUD 刷新仍待运行验收。
           - 开发者运行反馈超时定时器触发 `ObjectDisposedException`：已修正 ProcedureBattle 两个窗口等待中忽略 `CancelAfterSlim` 返回定时器的问题，使用嵌套 using 先释放定时器再释放取消源，保留15秒超时及父流程取消。仅静态检查通过，等待重新运行超过15秒及离场/失败路径验收。
           - 随后用户要求取消额外超时：已移除 Loading/HUD 两个15秒定时器及专用 linked CTS，直接保留流程 token 的加载/就绪等待与退出清理；未修改 TE 框架。若初始化失败且既未就绪也未关闭，将等待至流程取消，不再自动超时回退。源码静态检查通过，未运行验收；本条覆盖上一条超时保留方案。
       - 固定镜头只是 T1.8 的前置；八档转向（每档45°）与行动者居中在 T1.8 实现，拖拽平移/限幅缩放在 T1.13 实现；正式输入与 UGUI 操作仍在 T1.7 / T1.9 实现。
    4. **Task T1.4 固定队伍战前确认**
       - 新建 `BattlePreparationUI` 灰盒 Prefab，读取同一战斗场景配置并显示固定 2v2 的我方队伍、敌方队伍和预设站位摘要
       - 点击“开始战斗”后锁定重复输入、关闭战前窗口并创建全新战斗 Runtime；取消或返回时不残留 Session 和场景单位
       - 本任务不接角色库和真实布阵，只保留未来扩展列表与站位编辑的 Presenter / View 边界
    5. **Task T1.5 `BattleHUDUI` 灰盒 Prefab 与只读状态 MVP**
       - 新建项目自有 `BattleHUDUI` 灰盒 Prefab，建立回合、当前阵营、当前单位、耐久和 AP 等基础状态区
       - 建立 `BattleHUDUI : UIWindow` 与 Presenter / View（或 UI Adapter）边界，使用框架生命周期完成绑定、刷新、解绑和销毁
       - 当前 HUD 内部组件边界确定为：`BattleUnitStatusWidget` 负责顶部单位状态；`BattleActionAreaWidget` 负责行动区域组合与状态布局；其下包含 `BattleActionPreviewWidget`、`BattleWeaponListWidget`、`BattleAbilityListWidget`、`BattleCommandListWidget` 和 `BattleActionControlWidget`
       - `BattleWeaponListWidget` 负责武器列表与当前发动武器选择，子项为 `BattleWeaponItemWidget`；`BattleAbilityListWidget` 负责当前武器主动技能列表，子项为 `BattleAbilityItemWidget`；`BattleCommandListWidget` 负责角色指令列表，子项为 `BattleCommandItemWidget`
       - `BattleAbilityItemWidget` 与 `BattleCommandItemWidget` 不复用同一 Item Prefab 或 Widget 类型；两者虽然都表示可选行动，但结构和视觉交互不同，分别维护独立的预制体与表现职责
       - `BattleActionPreviewWidget` 内部复用同一个 `BattleActionUnitStatusWidget` 显示行动单位和目标单位，分别以 `m_item_ActorUnitStatus`、`m_item_TargetUnitStatus` 作为实例语义；目标侧只制作 `UnitStatusWidget_Mirrored` Prefab 变体，不新增镜像专用 C# 类型
       - 上述状态、预览、行动、指令和控制区域均作为 `BattleHUDUI` 下的 `UIWidget`，不因存在按钮输入而拆成独立 `UIWindow`；只有具备独立打开/关闭生命周期的结果、统计或模态弹窗才建立独立窗口
       - Widget 只负责只读数据显示、视觉状态和输入意图回调；Battle 合法性、资源检查、目标范围、行动确认和状态修改继续由 Presenter / Battle 权威层处理，不把 Battle 对象直接散落绑定到各个控件
       - 2026-10-18 已编写姓名/AP 最小只读闭环：`BattleHUDPresenter` 位于 GameLogic 的 `UI/Battle`，直接引用本场 Session 与具体 `BattleHUDUI`，不新增 View 接口或通用 ViewModel；流程在输入后进行差异刷新，HUD 关闭时解绑。状态组件不再在 OnCreate 调用 Clear，仅有真实来源的姓名/AP 被刷新，等级/头像/职业图标/多部位耐久保留 Prefab 默认值；未接线的列表、交战信息和操作区暂隐藏。本轮仅静态检查，未编译、测试或运行；正式场景已迁移，等待从主界面进入战斗后验收。
    6. **Task T1.6 胜利条件、阵营回合提示与默认选角**
       - 开始战斗后先显示 `BattleObjectiveUI`，本阶段固定说明“全灭敌方单位”；确认后再显示当前阵营和回合数提示
       - 每个阵营回合开始时显示一次 `BattleRoundUI`，弹窗存在期间锁定棋盘和 HUD 操作，确认关闭后才允许继续
       - 提示结束后默认选中 `CurrentCombatUnit`；若当前对象不可行动，则只从当前阵营存活且可行动单位中确定后备选择，不在 UI 中复制回合规则
    7. **Task T1.7 基础棋盘操作迁移**
       - 对齐单位选择、地格选择、移动范围、移动提交、待机、确认、取消与输入锁定
       - UGUI 与棋盘射线输入明确分层，点击 HUD 不穿透到战场，行动锁定和取消后的焦点/范围反馈与 Sprint 4 契约一致
       - 补齐 Presenter / View 状态测试与 Play Mode 基础操作冒烟
    8. **Task T1.8 八档环绕镜头与行动者居中（2026-10-22 修订，待实现）**
       - 在 Presentation 层管理观察方位；以配置的初始方位为基准，共8档、每档45°，第8次切换回初始方位。不修改角色逻辑朝向、位置或战斗状态。
       - 俯角默认45°且仅由配置调整，不提供运行时俯角输入；缩放由 T1.13 控制，缩放不改变俯角。
       - 每个角色开始行动时居中，包括首次开战、同阵营内切换和阵营交接；居中保留角度与缩放。手动平移后不每帧强制回中，下一个角色开始行动再回中；选择敌方目标不改行动者焦点，角色移动后的跟随方式开工时确认。
       - 在 BattleHUDUI 提供离散旋转入口；验证八档循环、边缘角色居中、棋盘射线/高亮/血条朝向与 UI 输入隔离。焦点边界不得阻止边缘行动者居中，不要求画面永不露出棋盘外。
       - 拖拽平移与限幅缩放归 T1.13，二者随本次排期纳入 T1；不加入自由轨道旋转、镜头碰撞或 BattleDirector 演出。
    9. **Task T1.9 双路径行动面板迁移**
       - 用户补充顶部单位状态区与 CombatInfo 互斥：HUD 初始化显示单位状态区，SetCombatInfo 显示交战信息时隐藏顶部状态区，取消目标/执行后随交战信息关闭恢复。未改数据契约、Prefab 或 Gen，待开发者运行验收。
       - 2026-10-19 用户纠正武器交互顺序，已修正为互斥阶段：无目标时仅显示默认选中的武器卡，点击武器不再提交普攻/进入行动锁定；棋盘选中武器范围内合法目标后隐藏武器、显示既有目标兼容查询返回的普攻/技能，沿用协调器自动默认行动与确认。取消/行动结束后恢复武器阶段；无目标时不能从 HUD 提前选技能，指令仍暂空。移除两列并显布局，两阶段共用右侧区域；Presenter 直接保留查询返回的行动身份与范围来源，不再自行构造技能目录或持有重复技能映射。静态四种显隐组合、点击索引与调用链核对通过，未编译、测试、运行或刷新，等待开发者验收。本条覆盖早前同时显示两列/支持直接点技能的错误实现说明。
       - 用户随后明确现阶段不需要不可用变淡反馈：已从 WeaponItem/AbilityItem 移除 CanvasGroup 依赖与 alpha 修改，仅保留点击禁用、选中标记和显隐；交互反馈优化后置。本条覆盖下方 CanvasGroup 修复与变淡方案，未修改资源或生成代码。
       - 2026-10-19 开发者运行报 WeaponItem 缺少 CanvasGroup，初始化回退主界面。已将 WeaponItem/AbilityItem 的 `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()` 改为显式 Unity `== null` 判断后补组件，避免缺失组件包装对象绕过 `??`。两处源码初始化顺序与尾空白静态核对通过；未改 Prefab/Gen/meta，未编译、测试、运行或刷新，等待重新进入战斗验收。
       - 2026-10-19 已按最新授权编写正式 HUD 接线：新增无绘制 `RaycastGraphic`，使用 Unity 正常生成的 meta/GUID；三个条目根均补齐 Graphic/CanvasRenderer，保留 AbilityItem 已有 Button，为 WeaponItem/CommandItem 补 Button，TargetGraphic 指向根命中区域，装饰元素不接收射线。武器/技能条目旧绑定类名已统一，Gen 与既有布局/绑定索引未改。
       - 当前配置每单位只有一份基础攻击，用户确认所有现有主动技能均作为武器技能，指令暂空，不新增分类字段或虚构多武器。Loading 阶段异步准备一张武器卡与最大所需技能槽位（含普攻），切换单位复用；两个空挂点通过运行时两列布局避开彼此及操作区，视觉效果待验收。条目显示名称/AP、基础攻击伤害/射程、选中与不可用反馈；未知等级、头像、持握、多部位耐久和预测数据不刷新。
       - Presenter 显式接收本场 Session、Controller、单位 Presenter、技能映射和具体 HUD，转发选择、确认、取消、待机；复用 ActionFirst / TargetFirst、RequiresBasicWeapon / RangeSource 与行动身份校验，不新增状态机。行动优先锁定后需确认或取消，目标优先可切换兼容技能；AP 消耗预览、交战双方姓名/AP、单位血条和行动后的默认选角同步刷新。指令控件仍隐藏，不声称已完成角色指令路径。
       - 静态核对通过：三份 Prefab 相对本轮备份无删除、原布局/绑定顺序保留，根点击组件唯一且本地引用闭合；13 个实现文件空白/分隔符、事件解绑、异步取消后回收、条目就绪顺序与程序集边界已核对。本轮新增差异空白检查通过；相对 HEAD 的 Prefab 检查仍会报告先前已有的 Unity 空字段尾空格。未自动编译、测试、运行、刷新、生成或提交；等待开发者验证整卡点击不穿透、两种选择顺序、AP 不足、合法/非法目标、取消保留站位、待机/成功行动推进、战斗结束禁用及离场/重入。
       - 对齐默认武器栏、基础武器技能、依赖基础武器技能、技能优先/目标优先、合法目标切换与释放前确认
       - 继续使用 `BattleActionSelectionPath`、`RequiresBasicWeapon`、`AbilityTargetRangeSource` 和统一行动身份契约，不在 UGUI 内重新推导
       - 右侧 `BattleWeaponListWidget` 与 `BattleAbilityListWidget` 分别显示武器列表和当前武器主动技能列表，底部 `BattleCommandListWidget` 显示角色指令列表；各列表使用独立的 `BattleWeaponItemWidget`、`BattleAbilityItemWidget` 和 `BattleCommandItemWidget`
       - 武器主动攻击路径为“角色行动界面已默认选择武器 → 点击合法目标 → 自动选择默认技能 → 显示攻击/取消 → 执行或返回角色行动界面”；指令路径为“点击指令 → 按需默认选择右手武器或切换可用武器 → 提示请选择目标 → 点击合法目标 → 显示确认/取消 → 执行或返回角色行动界面”
       - 两条路径共同抽象为“行动方案逐步补全”：最终补齐行动者、行动方式、发动武器（可选）和目标；差异仅保留在入口顺序、默认填充、技能自动补齐和确认样式，不按武器流程/指令流程重复创建整套状态对象
       - `BattleActionControlWidget` 统一承载待机、攻击、确认和取消；`BattleActionAreaWidget` 根据行动方案状态组合列表、提示、预览和控制布局，Widget 只提交行动意图，Presenter / Battle 负责权威校验、资源检查、范围判断和执行
       - 选中行动和目标后显示 `BattleActionPreviewWidget`：左右两侧分别显示行动单位与目标单位状态，中间区域预留伤害/命中/暴击等行动预测；当前尚无对应属性时不提前拼接或伪造这些字段
       - 下方 `BattleCommandListWidget` 显示角色当前可用的指令技能；下方 `BattleActionControlWidget` 提供待机、取消、确认/攻击等行为按钮；两者都属于当前 HUD 的交互区域，不直接等同于未来的 `BattleCommand` 核心对象
       - Widget 的按钮监听只提交行动意图并刷新本地选中反馈，实际行动由 Presenter 调用 Battle 契约；对局、行动者、目标、AP、范围和锁定状态由 Battle / Presentation 状态机返回
       - 对齐技能说明、可用/禁用/选中反馈和输入锁定，并覆盖两条路径的 EditMode 与 Play Mode 验收
    10. **Task T1.10 结果、伤害统计与后续选择**
       - [ ] **T1.10.1 胜利/失败结果页**：消费 Outcome 映射玩家结果，停止操作、不弹下一阵营提示；程序异常不当作战败，不新增奖励/经验/评级。
       - [ ] **T1.10.2 重战/返回/再次进入**：统一释放旧场后全新创建，恢复初始状态和空日志/选择/高亮；开工前确认正常返回是否实际切回 main 场景，不能把当前只打开 MainUI 的异常返回当作场景回切已完成。
       - [ ] **T1.10.3 统计**：实际造成/承受伤害和击杀的只读聚合，覆盖过量伤害；统计关闭返回结果页。按 .1 → .2 → .3 执行，形成 Luban 迁移前的闭环基线。
       - 根据 Battle 权威 Outcome 和固定玩家阵营映射胜利/失败，显示 `BattleResultUI`；结果页提供“查看统计”“重新战斗”“返回主场景”
       - 从 ActionLog 构建只读统计快照，按每台单位聚合实际造成伤害、实际承受伤害和击杀数；使用耐久前后差计算实际伤害，避免过量伤害污染统计
       - `BattleStatisticsUI` 由结果页按需打开，关闭后返回结果页；统计聚合使用纯 C# 测试覆盖普通攻击、技能、致死、过量伤害和双方单位
       - 验证重新开始恢复四台单位、初始位置、满耐久/AP、第 1 回合、TeamA、首台机兵、空日志、空选择与空高亮
       - 验证窗口关闭、返回伪主场景、再次进入战斗、场景切换和应用退出均按单一顺序释放 UI 事件、战斗 Runtime、场景单位与资源句柄
    11. **Task T1.11 功能对齐验收与临时 HUD 退役**
       - 2026-10-19 按用户授权提前完成旧 UI Toolkit HUD、专属测试和主题资源的静态清理；配置链正式化，见顶部记录。以下完整功能对齐与运行回归仍未验收，不能据此标记整个 T1.11 完成。
       - 完成“伪主场景 -> 点击进入战斗 -> 固定队伍确认 -> 胜利条件 -> 阵营回合提示 -> 固定 2v2 -> 结果/统计 -> 重战或返回 -> 再次进入”的完整回归验收
       - 覆盖默认选角、八档镜头/拖拽/缩放/行动者居中、双路径、焦点、弹窗输入锁定、棋盘/HUD 隔离、胜负、统计、重启、配置加载错误与重复进出战斗，确保不同流程窗口不会同时响应输入；指令隐藏不视为完成，新机制缺口另拆任务。
       - 在功能对齐后移除 UI Toolkit HUD 及仅为其服务的适配代码，保留仍被正式 Presentation 复用的纯 C# 契约
       - 运行 Battle、Presentation、GameLogic、Editor 编译与 Unity EditMode / Play Mode 验证，形成进入 Sprint 5 的稳定基线
    12. **Task T1.12 最小 Luban 战斗配置迁移**
       - 2026-10-19 当前 SO 入口已正式命名为 `MechStorm.Config.BattleScenarioConfig`，仍是同一聚合数据格式；去 Temp 命名不等于 Luban 数据迁移，以下任务继续保留。
       - 2026-10-10 用户授权提前迁移；2026-10-22 重排后改为 T1.10 结果/重战/统计闭环之后、T1.8/T1.13 镜头与 T1.11 完整回归之前。覆盖旧“T1.4 前迁移”顺序；保留 T1.1～T1.12 编号，迁移后各流程使用同一份正式配置数据。
       - [ ] **T1.12.1 最小数据工程**：定位/建立项目 Luban 配置工程，只覆盖当前关卡、初始单位阵营/站位、驾驶员、机兵、普攻与技能定义；沿用现有数值，不新增属性、装备、Buff 或养成规则。验收：ID 与引用关系明确，原始四台单位和技能均可表达。
       - [ ] **T1.12.2 配置加载**：生成代码归 GameProto、二进制数据由 YooAsset 管理；业务入口异步加载且配对释放资源，配置不可用时阻止战斗初始化并明确报错。生成/导表操作先与开发者确认，不自动运行。
       - [ ] **T1.12.3 装配替换**：在 GameLogic 侧将 Luban 行转换为现有 Battle Data，再创建独立 Runtime；Battle 不引用 Luban、Unity 或 GameProto。场景对象引用、材质与镜头调试参数留在表现层。
       - [ ] **T1.12.4 等价与隔离验收**：迁移前后的棋盘、站位、属性、技能与初始快照一致；运行时扣耐久/AP不污染配置，重战恢复初值；缺失引用、非法站位/数值不能留下半成品。等价验收后才退役正式路径中的临时配置，保留仍被测试使用的夹具。
       - 此任务仅提前建立现有数据的最小闭环；Sprint 11 的完整配表与实体装配范围不变，不把整个长期配置系统提前搬入 T1。
    13. **Task T1.13 战斗镜头拖拽、缩放与输入整合（2026-10-22 新增，待实现）**
       - [ ] **T1.13.1 拖拽平移**：在 T1.8 八档镜头基础上沿棋盘平面移动视野，配置焦点边界并保证边缘行动者可居中；开工确认拖拽键位/阈值。若用左键，先区分点击/拖拽再提交棋盘输入，不能按下就执行动作、拖完再撤销。
       - [ ] **T1.13.2 限幅缩放与输入整合**：滚轮在配置的最近/最远范围内缩放，俯角不变；UI 上的手势不穿透，明确提示/结算/离场启停与重战恢复规则。结合8个方位、多级缩放及平移验证选择/移动/攻击确认准确，无战斗动作误提交。
       - 先完成 T1.12，再按 T1.8 → T1.13.1 → T1.13.2 → T1 补漏/T1.11 回归推进；不包含角色拖拽部署、自由旋转、镜头碰撞或技能演出。
  - 扩展规则：T1.12 用于最小 Luban 迁移，T1.13 用于镜头交互扩展，后续新增任务从 T1.14 起追加；任务编号不代表执行顺序，以顶部已批准排期为准。硬前置可插入，但须同步依赖、验收标准及编号影响。
  - 执行边界：AI 负责按顺序实现；默认仅源码/资源静态核对，不自动编译、测试、运行、刷新或更新，不预埋自动化。上述验收标准不代表已执行，未经开发者验收不标记完成。
  - 非目标：本阶段不新增角色库滑动选人、上下阵、拖拽布阵、地形 Cost、路径动画、`BattleDirector` 动画时序、完整 Luban 配表体系（仅允许 T1.12 最小迁移）、正式美术皮肤、Buff / Modifier / Trigger 或新的战斗规则

- [ ] Sprint 5：地形 Cost 与路径查询
  - 状态：未开始
  - 总目标：在占格与范围规则稳定后引入最小地形数据、加权移动范围和单目标最优路径
  - 完成标准：格子具备地形类型、移动 Cost 和通行性；Dijkstra 计算全部可达格；A* 返回到指定目标的最低 Cost 路径；算法结果可通过纯 C# 测试和最小调试表现观察
  - 计划任务：
    1. Task 5.1 `GridCell`、`TerrainType`、`MovementCost` 与 `IsWalkable`
    2. Task 5.2 Dijkstra 加权可达范围
    3. Task 5.3 A* 单目标路径查询
    4. Task 5.4 地形 Cost 与路径调试可视化
  - 前置与配置：T1 完整回归验收后开始；复用 T1.12 的 Luban 工程，仅在地形规则确定后补充实际需要的地形数据，不在最小迁移时预建完整地形 Schema。
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
  - 备注：对应历史提交 `dbd147e 接入 TEngine 框架以及依赖库`；Sprint T1 只接正式战斗流程、UI 和资源边界，不重复执行框架接入

- [ ] Sprint 9：战前部署与表现导演
  - 计划内容：在 Sprint T1 的正式流程、项目自有 `BattleHUDUI` 和 Presenter / View 基线上增加 BattlePreparation / Deployment、可配置战斗入口与 `BattleDirector` 表现时序
  - 前置条件：Sprint T1 已完成 TEngine 正式战斗流程迁移并退役临时 UI Toolkit HUD，不在本 Sprint 重复接入框架或重做基础 HUD
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
| 主计划旧 Sprint 4～5 | 正式 TEngine 战斗流程、Presenter、State / Attribute / Modifier / Trigger、Luban、装配、Command、悔棋、续战和回放 | 正式流程、UGUI 与 Presenter 提前进入 Sprint T1；部署和表现导演保留 Sprint 9；其余拆入 Sprint 10～12 |
| 主计划旧 Sprint 6+ | 背包、元件、天赋、追加攻击、行动配额、养成、热更新资源流、分享录像和 PVP | Sprint 13+ 或满足前置条件后再单独排期 |
| 编辑器与参考分析 | TargetValidationReport、Breakdown、Schema、Validator、沙盒和内容编辑器 | 保留 P4～P6 工具链路线，不进入近期机制 Sprint |

主计划保留为历史蓝图，不回写其旧 Sprint 编号；ReferenceAnalyses 继续作为职责和风险参考，不作为任务状态来源。上述映射与后续调整统一以本文件为准。

## 决策记录

| 日期 | 决策 | 原因 | 影响 |
|------|------|------|------|
| 2026-10-22 | 批准“结果 → 重战/返回 → 统计 → 最小 Luban → 基础镜头 → T1 补漏/回归 → 地形寻路 → 后续机制”；镜头为8档、每档45°，固定配置俯角、平移、限幅缩放及行动者居中 | 先建立可重复战斗基线，再更换配置来源和完成观察/输入工具，降低后续地形与技能数据返工 | T1.10 拆 .1～.3；T1.12 移至其后；T1.8 改八档并追加 T1.13 镜头交互；旧四档与90°讨论方案不再适用，部署仍在 Sprint 9；既有待验收项不变，尚未开始新实现 |
| 2026-10-10 | 历史：在入口流程已通后，按“灰盒地面与固定俯瞰镜头 → 正式 Runtime Host 与固定 2v2 → 最小 Luban 配置迁移”逐步执行；剩余顺序由2026-10-22重排覆盖 | 当时 Battle 只有占位节点和默认平视相机；不能将场景加载成功等同于战斗初始化完成，也不应一次性迁入全部临时表现 | 保留 T1.3.1～T1.3.4 与 T1.12 编号；当前 Luban 排在 T1.10 闭环后，镜头执行八档45°及 T1.13；入口已通不代表整个 T1 验收，AI 默认不自动运行验证或刷新 |
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
| 2026-07-30 | Sprint 4 完成后优先接入 TEngine 正式战斗流程与 UGUI，不继续扩展临时 UI Toolkit HUD | 当前双路径战斗交互和纯 C# 契约已稳定，继续扩展临时 HUD 会增加未来迁移成本；缺少美术素材不阻塞灰盒 UGUI 验证 | Task 4.11 / 4.12 仍按当前临时场景完成；阶段验收后先拆分 `BattleMainUI`、`UIWindow / UIWidget`、Procedure 与 Presenter / View 迁移任务，功能对齐后停止维护 UI Toolkit 版本，再继续 Sprint 5 |
| 2026-07-30 | 新增独立 P1.5 / Sprint T1 承载 TEngine + UGUI 正式战斗表现迁移 | 迁移工作横跨正式入口、场景、Runtime 生命周期、UGUI、双路径操作与退役临时 HUD，不应作为 Sprint 4 尾项，也不应混入 Sprint 5 地形机制；独立阶段也便于继续追加用户扩展任务 | Sprint T1 排在 Sprint 4 与 Sprint 5 之间，使用 Task T1.x 独立编号；原 Sprint 9 缩减为战前部署、可配置战斗入口和表现导演 |
| 2026-07-30 | Sprint T1 先搭建伪主场景和战斗入口按钮，再进入正式战斗场景 | 需要验证真实的游戏入口与场景切换链路，避免应用启动后直接打开战斗 UI，也为后续扩展主界面功能保留稳定入口 | 该入口固定为 Task T1.1；最终验收覆盖进入战斗、返回主场景和再次进入 |
| 2026-07-30 | Sprint T1 使用项目自有战斗 UI Prefab，并补齐固定队伍确认、胜利条件、阵营回合提示、默认选角、结果与按需伤害统计流程 | TEngine 示例 `BattleMainUI.prefab` 不代表最终项目结构；当前已有固定 2v2、Outcome、ActionLog 与重建能力，可以以中等可控成本建立正式灰盒流程，而无需提前实现角色库和真实布阵 | 新增 `BattlePreparationUI`、`BattleHUDUI`、`BattleObjectiveUI`、`BattleRoundUI`、`BattleResultUI`、`BattleStatisticsUI` 职责；回合提示每个阵营回合一次；统计按单位记录造成伤害、承受伤害和击杀；结果页按需打开统计 |
| 2026-07-30 | 历史：Sprint T1 增加围绕当前操控机兵的四档战斗镜头，已由2026-10-22决定覆盖 | 当时仅需基础观察方位，不需要自由相机和复杂镜头导演 | 原0°、45°、90°、135°及固定距离方案不再作为当前验收依据；当前执行 T1.8 八档45°与 T1.13 平移/限幅缩放 |
| 2026-09-08 | 战斗 HUD 的行动、指令和目标信息采用 Widget 组合，不为按钮输入单独建立 UIWindow | 右侧行动列表、上方行动预览、下方指令列表和确认控制都跟随同一场战斗持续存在，共享 `BattleHUDUI` 生命周期；输入监听不改变其只读显示职责 | `BattleHUDUI` 下新增 `BattleActionPreviewWidget`、`BattleActionPanelWidget`、`BattleCommandPanelWidget`、`BattleActionControlWidget`；预览内部复用 `BattleActionUnitStatusWidget`，目标侧使用 `UnitStatusWidget_Mirrored` Prefab 变体；Widget 只发出行动意图，Presenter / Battle 负责权威校验与执行 |
| 2026-09-08 | 武器主动攻击与指令行动统一抽象为“行动方案逐步补全”流程 | 两条路径最终都需要确定行动者、行动方式、发动武器（可选）和目标；差异仅在入口顺序、默认填充字段、技能是否自动补齐以及确认样式 | 统一维护行动方案与选择状态：武器路径从“默认武器行动已预选”进入目标选择，目标确定后自动选择默认技能并显示“攻击”；指令路径从指令选择开始，按需默认选择右手武器，随后选择目标，合法目标确定后才显示“确认”；取消均回到角色行动界面；UI 可复用同一行动控制 Widget，Battle / Presenter 保持权威校验与执行 |

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

1. 保留既有异常退出的 Unity 待验收项；下一实施从 T1.10.1 胜利/失败结果页开始，随后 T1.10.2 重战/返回/再次进入、T1.10.3 统计。开始实现前核对当前资源及正常返回场景策略。
2. 执行 T1.12 最小 Luban 数据工程、异步加载、装配来源替换与等价/隔离验收，不改变现有数值或规则。
3. 执行 T1.8 八档镜头（每档45°、配置俯角、行动者居中），再执行 T1.13 拖拽平移、限幅缩放及输入整合；开工确认键位与角色移动后的跟随规则。
4. 补齐 T1.4/T1.7/T1.9 的实际缺口，按开发者明确授权完成 T1.11 全流程回归；旧临时 HUD 已静态退役，不重复清理，也不把历史待验收项自动标记通过。
5. T1 验收后进入 Sprint 5 地形 Cost、Dijkstra、A* 与调试可视化，仍保持瞬移；后续机制按 Sprint 推进，角色拖拽部署仍留 Sprint 9。
