# 战斗编辑器工具链策略

## 一、文档目的

本文记录围绕 MechStorm 后期“技能编辑器 / 战斗内容编辑器”的概念澄清和技术选型。

核心问题不是“编辑器一定写在 Unity 里还是 Web 里”，而是：

```text
如何用同一套 Battle Runtime、同一套 Schema、同一套校验器，支撑不同形态的编辑器和预览工具。
```

本文中的“编辑器”不只指技能编辑器。长期目标应是 **战斗内容编辑器**，覆盖角色、机兵、部位、武器、技能、状态、触发器、条件、AI 参数和表现 Cue 引用。

## 二、核心概念

### 2.1 Battle Runtime

`Battle Runtime` 指真实执行战斗规则的系统，不等于“游戏打包后的运行环境”。

在 MechStorm 中，它对应 `MechStorm.Battle`：

```text
Ability 数据
-> TargetRule 判定
-> Effect 执行
-> Damage 结算
-> State / Buff 变化
-> Trigger 触发
-> Result / Log 输出
```

它可以被多个入口调用：

```text
Unity 游戏内
Unity Editor
Web 后端
自动测试
战斗回放
沙盒模拟器
```

关键原则：

```text
真实战斗规则只能有一套
```

不要在 Web 前端、Unity Editor、测试工具中各自实现一套“看起来差不多”的技能结算逻辑。

### 2.2 Schema

`Schema` 指战斗配置数据的结构定义和约束规则。

它负责说明：

```text
一个 Ability 有哪些字段
一个 Effect 有哪些类型
每种 Effect 需要哪些参数
State / Buff 怎么描述
Trigger / Condition 怎么描述
字段类型是什么
哪些字段必填
哪些引用必须存在
哪些组合非法
```

在 MechStorm 中，Schema 可能同时体现在：

```text
Luban 表结构
C# Definition 类
JSON / YAML / 二进制导出格式
编辑器表单结构
校验规则
版本迁移规则
```

Schema 是 Web 编辑器、Unity 编辑器、校验器和 Battle Runtime 的共同合同。

### 2.3 Validator

`Validator` 指配置校验器。

它不负责执行战斗，而是检查数据是否安全、完整、可执行：

```text
Tag 是否存在
Ability 引用的 Effect 是否存在
ApplyStateEffect 引用的 State 是否存在
Trigger 是否形成无限循环
Condition 是否永远不可达
TargetRule 参数是否合法
DamageFormula 是否缺少变量
State 互斥关系是否冲突
```

Validator 应该尽量被所有工具复用：

```text
Luban 导表前
Web 编辑器保存前
Unity Editor 预览前
CI 自动检查
内容包发布前
```

### 2.4 Editor

`Editor` 指编辑数据的工具，不限定实现平台。

它可以是：

```text
Web 页面
Unity EditorWindow
UI Toolkit 工具
Odin Inspector 面板
命令行工具
```

编辑器的职责是：

```text
编辑数据
展示字段
提示错误
保存配置
导出数据包
调用预览或校验接口
```

编辑器不应该自己实现一套战斗结算规则。

### 2.5 Preview

`Preview` 指在编辑时试跑配置并展示结果。

例如：

```text
技能能否选择目标
AOE 覆盖哪些格子
LOS 是否被阻挡
每段 Hit 命中了谁
造成多少伤害
施加了哪些 Buff
触发了哪些 Trigger / Reaction
播放哪些 Cue
```

准确说法应是：

```text
编辑器内预览，调用真实 Battle Runtime 或真实 Presentation Runtime。
```

不要把“编辑器预览”和“运行时预览”理解成两个互斥概念。预览是编辑器功能，Runtime 是预览背后的真实计算或表现执行能力。

## 三、为什么要避免多套运行时

“多套运行时”指不同工具各自实现一套规则解释系统。

错误结构：

```text
Unity Battle Runtime
  负责真实战斗结算

Web Preview Runtime
  用 TypeScript 再实现一套伤害、Buff、Trigger、AOE 逻辑

Unity Editor Preview Runtime
  又写一套编辑器内模拟逻辑
```

这样会导致：

```text
新增一个 Effect，要改多处逻辑
Unity 和 Web 预览结果不一致
校验器认为合法，真实运行时报错
Web 能编辑的字段，Unity Runtime 不认识
Unity 支持的新机制，Web 编辑器不能正确预览
Bug 很难判断是数据错、预览错，还是 Runtime 错
```

正确结构：

```text
Web Editor
Unity Editor
CI Test Runner
Balance Simulator
        ↓
同一套 Schema
同一套 Validator
同一套 Battle Runtime
        ↓
BattleActionResult / BattleActionLog / Snapshot / Breakdown
```

## 四、Web 编辑器的优缺点

### 4.1 适合 Web 的内容

Web 适合处理纯数据、配置管理、分析和协作：

```text
技能数据编辑
状态 / Buff 数据编辑
Trigger / Condition 数据编辑
Tag 注册表
AI 评分参数
配置搜索
配置 Diff
版本管理
内容包 Review
校验报告
战斗日志中心
平衡数据面板
AI 辅助生成配置草案
```

### 4.2 Web 的优点

- UI 库丰富，开发效率高。
- AI 对 Web 代码支持好，生成和重构成本低。
- 跨平台访问方便，不依赖本机 Unity 环境。
- 更适合多人协作、权限管理、Review 流程和内容包管理。
- 更适合做日志检索、Diff、表格、图表、平衡分析。
- 可以部署到服务器，作为长期内容生产平台。

### 4.3 Web 的缺点

- 不天然具备 Unity 场景、Prefab、Animator、VFX、Shader 的所见即所得能力。
- 如果 Web 前端自己实现战斗预览，容易变成第二套运行时。
- 实机表现预览困难，尤其是弹道、命中点、逐帧 Hit Window、镜头、特效挂点。
- 需要处理数据包导入导出、权限、部署、服务端接口等额外工程。
- 新增机制时，编辑表单、Schema、校验器、Runtime 的同步要设计好，否则维护成本会上升。

### 4.4 Web 的推荐定位

Web 不应定位成“替代 Unity 的所有编辑能力”，而应优先定位为：

```text
战斗内容数据管理平台
配置校验中心
调试日志中心
平衡分析中心
AI 辅助内容生产平台
```

如果需要 Web 预览复杂战斗结果，推荐方式是：

```text
Web 前端
-> 调用后端 C# Battle Runtime
-> 返回 Result / Log / Breakdown
-> Web 只负责展示
```

不要在 Web 前端用 TypeScript 复刻完整战斗规则。

## 五、Unity 编辑器的优缺点

### 5.1 适合 Unity Editor 的内容

Unity Editor 适合强依赖实机表现和场景上下文的工具：

```text
技能实机预览
弹道轨迹预览
AOE 与模型位置校准
Hit Window 逐帧调试
特效挂点调试
镜头和动画时序预览
Prefab / Animator / VFX / Shader 引用检查
战斗沙盒场景
地图编辑中需要实时看 Unity 场景的部分
```

### 5.2 Unity Editor 的优点

- 可以直接调用 Unity 场景和真实表现层。
- 可以做到所见即所得。
- 适合逐帧调试技能特效、弹道、伤害区域、动画事件和命中窗口。
- 可以直接检查 Prefab、材质、Animator、VFX、Timeline、DOTween 等资源引用。
- 与 `MechStorm.Presentation` 集成最自然。

### 5.3 Unity Editor 的缺点

- UI 开发体验和组件生态不如 Web 丰富。
- 跨平台协作和远程访问不如 Web 方便。
- 做复杂表格、Diff、图表、权限、Review、搜索、批量分析会比较笨重。
- 工具代码容易和 Unity Editor API、项目场景、资源路径耦合。
- 如果把纯数据管理全部塞进 Unity，后期内容生产效率可能受限。

### 5.4 Unity Editor 的推荐定位

Unity Editor 不应承担全部内容生产平台职责，而应优先定位为：

```text
实机表现预览器
战斗沙盒调试器
资源引用检查器
逐帧表现调试工具
```

## 六、推荐混合方案

MechStorm 后期不应在 Web 和 Unity Editor 之间二选一。

推荐结构：

```text
Web
  纯数据编辑
  配置管理
  日志中心
  Diff
  平衡面板
  AI 辅助

Unity Editor
  实机表现预览
  技能特效调试
  弹道和命中窗口
  场景沙盒
  Prefab / VFX / Animator 校验

共享底层
  Battle Runtime
  Battle Data Schema
  Validator
  Content Package
  Result / Log / Snapshot / Breakdown
```

最重要的架构要求：

```text
Web 和 Unity Editor 可以有两套 UI
但不能有两套战斗规则
```

## 七、数据流建议

### 7.1 编辑数据流

```text
Web Editor / Unity Editor
-> 编辑 Battle Data
-> 按 Schema 保存
-> Validator 校验
-> 导出 Content Package
-> Unity / Server / Test Runner 加载
```

### 7.2 逻辑预览流

```text
Editor
-> 选择 Scenario
-> 加载 Ability / State / Unit 配置
-> 调用 Battle Runtime
-> 返回 BattleActionResult
-> 返回 BattleActionLog
-> 返回 DamageBreakdown / TriggerExecutionLog
-> Editor 展示结果
```

### 7.3 表现预览流

```text
Unity Editor
-> 加载 Content Package
-> 创建沙盒场景
-> 调用 Battle Runtime 得到 Result
-> Presentation 播放动画、弹道、特效、飘字
-> 调试 Hit Window、AOE、挂点和镜头
```

### 7.4 Web 调试中心流

```text
Unity / Server / Test Runner
-> 导出 BattleActionLog / Snapshot / Breakdown
-> 上传或写入日志中心
-> Web 展示检索、过滤、Diff、统计和异常项
```

## 八、技术选型建议

### 8.1 近期

当前阶段不做完整编辑器。

当前执行已进入 P1 / Sprint 3。Sprint 3～5 只实现范围 / 目标查询、高亮、技能结果日志、地形 Cost 和路径的最小可观察性；下列完整 Report、Breakdown、Schema 和面板仍属于后续工具链基建，不应为了近期机制验收提前搭建。

已完成的近期底座：

```text
BattleActionResult
BattleSnapshot
BattleActionLog
```

工具链 P4 再优先建设：

```text
TargetValidationReport
DamageBreakdown
StateChangeLog
TriggerExecutionLog
```

这些是未来 Web 和 Unity Editor 都要复用的底座。

### 8.2 P4

P4 应专注编辑器前置基建：

```text
Schema 定稿
Validator
复杂样例配置
沙盒预览接口
配置级回归测试
内部调试面板
```

此时可以先用：

```text
Luban 表
JSON
简单 EditorWindow
Odin Inspector
命令行测试
```

不急着做漂亮的 Web 编辑器或完整节点编辑器。

### 8.3 P5

P5 再做战斗内容编辑器 MVP。

建议拆成两个方向：

```text
Web Editor
  做纯数据编辑、配置搜索、Diff、校验、日志和平衡面板

Unity Editor
  做实机技能预览、表现调试和沙盒场景
```

如果资源有限，优先级建议：

```text
1. Web 数据编辑和校验
2. Unity 沙盒预览
3. Web 日志中心和平衡面板
4. Unity 逐帧表现调试
```

### 8.4 P6

P6 才进入内容生产工业化：

```text
内容包版本系统
自动回归 Runner
平衡模拟
配置 Diff 报告
内容 Review 工作流
AI 辅助内容草案
```

## 九、决策结论

1. 编辑器不必限制在 Unity 中实现。
2. Web 很适合纯数据、配置管理、日志中心、Diff、平衡分析和 AI 辅助。
3. Unity Editor 仍然适合实机表现预览、逐帧调试、弹道、命中窗口和特效挂点。
4. 不应让 Web 前端重写完整战斗运行时。
5. 应坚持同一套 `MechStorm.Battle`、同一套 Schema、同一套 Validator。
6. 编辑器预览不是另一套运行时，可靠预览必须调用真实 Runtime。
7. 长期最优解是 Web + Unity Editor 混合工具链，共享数据包和战斗核心。

一句话总结：

```text
编辑器可以多端，数据合同只能一套，战斗规则只能一套。
```
