# MechStorm Droid Instructions

## 语言

- 请使用中文写提案、分析和回答。

## 执行边界

- 默认只编写或修改逻辑，不自动执行编译、测试、运行验证、Unity 刷新或更新操作；仅在开发者发现问题并明确要求时执行，不预埋延迟执行的自动化任务。

## 必须先做

在回答任何 MechStorm 开发问题、设计方案、修改代码、提交或打标签前，必须先读取：

1. 当前开发进度：[`DEVELOPMENT_PROGRESS.md`](./DEVELOPMENT_PROGRESS.md)
2. TEngine 与项目工作流：[`CLAUDE.md`](./CLAUDE.md)
3. 项目架构与开发主计划：[`MechStorm (机兵风暴) - SRPG 战棋游戏架构与开发主计划.md`](../MechStorm%20%28%E6%9C%BA%E5%85%B5%E9%A3%8E%E6%9A%B4%29%20-%20SRPG%20%E6%88%98%E6%A3%8B%E6%B8%B8%E6%88%8F%E6%9E%B6%E6%9E%84%E4%B8%8E%E5%BC%80%E5%8F%91%E4%B8%BB%E8%AE%A1%E5%88%92.md)
4. 通用工程协作与编码原则：[`ENGINEERING_COLLABORATION_GUIDELINES.md`](./ENGINEERING_COLLABORATION_GUIDELINES.md)

其中 `DEVELOPMENT_PROGRESS.md` 是当前阶段、当前任务、验收状态、决策记录和下一步开发方向的唯一进度来源。

未读取这些文件前，不要开始设计、编码、提交或切换开发阶段。

## 当前任务工作台

- 本地文件 [`.factory/task-state/CURRENT_TASK.md`](./.factory/task-state/CURRENT_TASK.md) 用于记录当前任务的目标、范围、决策、执行清单、停留位置、验证结果和恢复入口。
- `DEVELOPMENT_PROGRESS.md` 仍是阶段、任务完成情况和正式验收结果的唯一长期来源；`CURRENT_TASK.md` 只保存尚未完成任务的执行细节，不能取代正式进度文档。
- 开始新的开发任务时，必须根据 `DEVELOPMENT_PROGRESS.md` 初始化或重写 `CURRENT_TASK.md`，再开始设计和编码。
- 新会话、上下文压缩或中途问题结束后，必须先读 `DEVELOPMENT_PROGRESS.md`，再读 `CURRENT_TASK.md`，从“当前停留位置”和“恢复工作时的第一步”继续。
- 完成步骤、发现阻塞、确认新决策、改变任务边界、运行验证或修改下一步时，必须同步更新 `CURRENT_TASK.md`；普通问答如果没有改变任务事实，则不记录完整聊天内容。
- 同一时间只保留一个当前开发任务；执行清单使用 `[x]`、`[~]`、`[ ]` 表示完成、进行中和未开始，并保持只有一项 `[~]`。
- 当前任务全部验收后，先把正式结果同步到 `DEVELOPMENT_PROGRESS.md`，再将 `CURRENT_TASK.md` 重置为“当前无活动任务”，等待下一个任务初始化。
- `CURRENT_TASK.md` 是本地工作文件，必须被 Git 忽略；不得写入密钥、密码、令牌或其他敏感信息。

## Unity 资源与 `.meta` 文件

- 默认由 Unity Editor 自动创建和维护 `.meta` 文件，禁止为了触发导入、补齐测试项目或方便脚本编译而手工创建、编辑、替换或删除 `.meta`。
- 新增资源或脚本后如果 `.meta` 尚未生成，等待开发者通过 Unity 正常刷新；只有明确要求时才主动触发刷新，不得自行伪造 GUID。
- 移动、重命名或删除 Unity 资源时必须保留资源与其 `.meta` 的配对关系，优先通过 Unity Editor 或项目已有的 Unity 工具执行，不能只处理其中一个文件。
- 只有在用户明确授权修复指定 `.meta`，或已确认存在 GUID 损坏、合并冲突等必须人工介入的问题时，才允许编辑；操作前必须读取最新文件、检查 Git 状态并说明将保留或修改哪个 GUID。
- 不得重新生成已有资源的 GUID，除非用户明确要求让引用失效并创建新的资源身份。

## 项目规范来源

- TEngine、HybridCLR、YooAsset、UI、资源加载、事件系统、热更边界等规范，以 `CLAUDE.md` 和 `.claude/skills/tengine-dev/references/` 为准。
- 架构分析、编码取舍、精准修改、目标驱动执行、沟通和 Git 通用原则，以 `ENGINEERING_COLLABORATION_GUIDELINES.md` 为准。
- 编写、重构和审查时，遵循 [4.4 避免过度防御](./ENGINEERING_COLLABORATION_GUIDELINES.md#44-避免过度防御)：优先信任框架契约和明确的生命周期所有权，不在业务层重复底层已覆盖的空值、异常或推测性兜底。
- UI 手写业务代码优先使用基类提供的职责钩子，不将成员绑定、事件注册一并堆进 `OnCreate`；具体分工见 [UI 生命周期职责规范](./.claude/skills/tengine-dev/references/ui-lifecycle.md#ui-业务代码的职责划分)。
- UI 运行时实例加载使用基类采用的 `UIModule.Resource` 或适用的 Widget 创建方法，不直接调用资源模块；边界见 [UI 资源加载入口](./.claude/skills/tengine-dev/references/ui-lifecycle.md#ui-运行时资源加载入口)。

## 通用工程原则（摘要）

以下是通用原则摘要，完整论述与判断依据见工程协作文档 4.4、4.5 节。

- 先核对框架契约。框架已保证的空值、异常、销毁、加载结果和幂等性，业务层直接使用，不再补同义判断或包装；只处理明确、实际可达且未被框架覆盖的风险。
- 框架保证幂等的清理直接调用，不另建“是否已请求”的标记。标记只用于记录框架不保证的副作用或独立事实。
- 等待外部异步结果时，确认框架在超时或失败后给出什么结果。若框架会返回未完成状态，业务必须显式检查并抛出具体错误，不能让后续等待无限挂起。
- 流程独占的对象不为“可能被外部随意关闭”增加分支或轮询。已有先取消再销毁的顺序时，直接传递所属生命周期的 token，不额外叠加销毁 token 或 linked CTS；只有真实独立中断、并发或抢占需求才增加相应机制。
- 已等待初始化完成且没有新的失效边界时，直接使用结果，不再保存就绪标记或重复检查销毁状态。所有者已统一停止、解绑和销毁时，不再通过子对象关闭通知建立第二条释放链；普通调用不为违约访问静默返回，释放入口仍保持幂等。
- 不重复初始化或阶段退出已经完成的状态设置；不在已按契约检查同一 token 的 await 后、没有新失效边界时紧接着重复检查取消。不支持取消的加载和旧场结果隔离另按实际契约处理。
- 明确、易改且会直接暴露的内部错误，不为推测中的触发路径增加守卫、存活标记、绑定预检查或恢复分支；真实外部边界和代价不可接受的失败另行处理。
- 默认不写 `try/catch`。程序错误直接暴露，由开发者定位修复；异步方法、独立任务入口、事件回调或拥有资源本身，都不是必须捕获异常的理由。只有承担明确的必要清理、已定义的恢复或补充有效诊断信息时才捕获，不能以“更安全”“防止报错”为由增加兜底。
- 不擅自把程序错误改成自动清理整场、返回入口、重试、弹窗或继续运行；失败退出必须有明确需求。正常取消和离场释放与异常恢复分开设计，不因删除失败捕获而删除必要的生命周期处理。
- 确需捕获时，只在能负责处理的边界处理，不层层包裹；不吞异常、不返回默认值伪装成功、不重复框架已有日志。需继续传播时用 `throw;` 保留堆栈；只补充上下文时保留原异常。必要的资源安全仍用 `using` / `finally` 或已定义的所有者释放契约保证，不借此自动增加整场恢复策略。完整判断见 [4.5 异常捕获时机](./ENGINEERING_COLLABORATION_GUIDELINES.md#45-异常捕获时机)。
- 新增防御前，应能说明它防止的具体失败、为什么上游契约没有覆盖，以及失败后由谁处理。说不清时不加。
- 战斗系统长期架构、机制扩展、阶段性演进，以 `BATTLE_ARCHITECTURE_ROADMAP.md` 为准。
- 当前 Sprint 进度、任务验收、阶段决策，以 `DEVELOPMENT_PROGRESS.md` 为准。
- 外部项目与文章的架构分析位于 `ReferenceAnalyses/` 的主题目录中，文件名直接表达分析主题；这些文档用于提供职责拆分、调用流程、优缺点和踩坑案例参考，不作为高于项目决策的规范来源。

## 逻辑任务强制参考流程

以下任务开始前，必须先查询与当前主题相关的引用分析文档和项目决策文档：

1. 编写或修改游戏代码逻辑。
2. 审查开发者编写的代码逻辑。
3. 规划、拆分或提示后续开发任务中的逻辑实现。

执行要求：

- 先根据任务主题在 `ReferenceAnalyses/` 中定位具体分析文档，只读取与当前主题有关的内容，避免无关资料扩大任务范围。
- 同时核对 `DEVELOPMENT_PROGRESS.md` 的当前任务与决策记录、`BATTLE_ARCHITECTURE_ROADMAP.md` 的长期约束，以及主计划中的对应机制边界。
- 引用分析用于借鉴职责拆分和已知风险；若引用分析与项目决策冲突，以项目决策文档为准，不得直接照搬参考项目实现。
- 设计、审查或实现结论应能说明其对应的项目任务要求、决策依据或引用分析启发，不能跳过该步骤直接给出代码。
- 同一会话内已经读取且主题未变化的文档可以复用；涉及新主题或文件已变更时必须重新查询。

### 引用分析主题路由

- 回合制战斗流程、单位注册表、回合队列：`ReferenceAnalyses/BattleArchitecture/TurnBasedCombatArchitecture.md`
- MechStorm 当前阶段的参考落地顺序：`ReferenceAnalyses/BattleArchitecture/MechStormArchitectureApplicationGuide.md`
- Gameplay Ability System、属性、状态、效果、Tag：`ReferenceAnalyses/AbilitySystems/GameplayAbilitySystemArchitecture.md`
- 复杂战斗系统职责拆分、逻辑与表现、回放边界：`ReferenceAnalyses/AbilitySystems/AbilityKitCombatArchitecture.md`
- Buff 生命周期、实例身份、叠层刷新、驱散、光环、来源和状态恢复：`ReferenceAnalyses/BuffSystems/ScalableBuffSystemArchitecture.md`
- C# 性能优化与 BenchmarkDotNet 工作流：`ReferenceAnalyses/Performance/BenchmarkDotNetAiOptimizationWorkflow.md`

## TEngine 规范路由

按任务类型读取对应精炼文档：

- 项目结构 / 启动流程：`.claude/skills/tengine-dev/references/architecture.md`
- 热更边界 / GameApp 入口：`.claude/skills/tengine-dev/references/hotfix-workflow.md`
- UI 生命周期 / UIWindow / UIWidget：`.claude/skills/tengine-dev/references/ui-lifecycle.md`
- UI 进阶模式：`.claude/skills/tengine-dev/references/ui-patterns.md`
- 资源加载 / 卸载：`.claude/skills/tengine-dev/references/resource-api.md`
- 事件系统：`.claude/skills/tengine-dev/references/event-system.md`
- 模块访问：`.claude/skills/tengine-dev/references/modules.md`
- Luban 配置：`.claude/skills/tengine-dev/references/luban-config.md`
- 命名规则 / 禁止模式：`.claude/skills/tengine-dev/references/naming-rules.md`

## C# 参数排版

- 方法、构造函数及调用参数默认尽量在同一行书写，不因参数数量机械换行。
- 只有整行明显过宽、影响阅读时才换行。
- 换行后按语义将多个相关参数放在同一行，不采用一个参数独占一行的排版。

## Skill 使用边界

- 默认只使用项目自身提供的 Skill，包括项目 `.factory/skills/`、`.claude/skills/` 中定义或由项目动态发现的 Skill。
- Factory/Droid 内置或其他工具层提供的通用 Skill，不得仅因为“可能有帮助”而主动调用。
- 工具层 Skill 仅在以下情况使用：
  1. 用户明确要求使用该 Skill 或其对应能力。
  2. 用户任务与该 Skill 的职责高度重合，并且项目自身没有等效流程或 Skill。
- 若项目文档、项目 Skill 或现有工作流已经覆盖任务需求，应优先遵循项目方案，不再叠加通用审查、简化或分析 Skill。
- 不得因主动调用工具层 Skill 而引入超出用户要求的子任务、额外 Agent 审查或扩大实现范围。

## 规范分层原则

- `AGENTS.md` 只作为 Factory/Droid 的入口索引，不复制完整项目规范。
- 具体 TEngine 开发规范放在 `CLAUDE.md` 和 `.claude/skills/tengine-dev/references/`。
- 具体战斗架构规划放在 `BATTLE_ARCHITECTURE_ROADMAP.md`。
- 具体进度、验收和阶段决策放在 `DEVELOPMENT_PROGRESS.md`。
