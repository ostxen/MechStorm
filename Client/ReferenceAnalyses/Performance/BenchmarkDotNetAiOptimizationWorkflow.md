# BenchmarkDotNet 与 AI 辅助 C# 性能优化工作流分析

文章来源：

- Medium 文章：<https://neuecc.medium.com/how-to-write-the-fastest-code-with-benchmarkdotnet-in-c-in-the-ai-era-6a585e634488>
- 标题：`How to Write the Fastest Code with BenchmarkDotNet in C# in the AI Era`
- 作者：Yoshifumi Kawai
- 本地文件：`C:\Users\admin\Downloads\How to Write the Fastest Code with BenchmarkDotNet in C# in the AI Era _ by Yoshifumi Kawai _ Jul, 2026 _ Medium.html`

本文记录文章的核心观点、BenchmarkDotNet 使用方法、AI 参与性能优化的工作流、示例拆解、风险和对 MechStorm / C# 项目的实践参考。

## 目录

- [A. 文章写了什么](#a-文章写了什么)
- [B. 设计意图和要解决的问题](#b-设计意图和要解决的问题)
- [C. BenchmarkDotNet 工作流](#c-benchmarkdotnet-工作流)
- [D. 示例一：数组求和优化](#d-示例一数组求和优化)
- [E. 示例二：MessagePack `TryWriteInt32`](#e-示例二messagepack-trywriteint32)
- [F. AI 在文章中的角色](#f-ai-在文章中的角色)
- [G. 优点](#g-优点)
- [H. 风险和局限](#h-风险和局限)
- [I. 对 MechStorm 和 C# 项目的实践参考](#i-对-mechstorm-和-c-项目的实践参考)
- [J. 一句话总结](#j-一句话总结)

## A. 文章写了什么

### A.1 核心主题

这篇文章讨论的是：在 AI 辅助写代码的时代，C# 性能优化应该如何用 BenchmarkDotNet 形成一套证据驱动的循环。

文章的核心观点是：

```text
不要只看 Mean 和 Allocated。
如果 AI 要参与性能优化，就应该把更多底层证据交给 AI。
```

作者认为，传统 BenchmarkDotNet 常看的数据是：

```text
Mean
Allocated
```

这只能回答：

```text
哪个实现更快？
哪个实现分配更少？
```

但无法充分回答：

```text
为什么更快？
JIT 生成了什么机器码？
有没有边界检查？
有没有多余指令？
分支预测是否失败？
代码体积是否暴涨？
```

因此文章建议默认把这些信息也纳入性能分析：

```text
JIT Disassembly
BranchMispredictions/Op
BranchInstructions
MemoryDiagnoser
JsonExporter.Full
```

### A.2 文章的主张

文章不是单纯讲 BenchmarkDotNet 入门，而是提出一种 AI 时代的性能优化方式：

```text
写 benchmark
-> 跑 BenchmarkDotNet
-> 导出时间、分配、反汇编、硬件计数器
-> 让 AI 阅读结果
-> 生成新的候选实现
-> 再 benchmark
-> 人类决定是否接受
```

重点不是“让 AI 猜优化方式”，而是让 AI 基于证据工作。

## B. 设计意图和要解决的问题

### B.1 解决“凭感觉优化”的问题

没有 benchmark 时，性能优化常见问题是：

```text
我觉得这样更快
我觉得 LINQ 慢
我觉得 unsafe 一定快
我觉得 SIMD 一定有用
```

文章反对这种方式。它要求每个优化都必须有证据：

```text
平均耗时
内存分配
JIT 汇编
分支误预测
代码大小
输入分布
正确性测试
```

### B.2 解决 AI 缺少上下文的问题

AI 可以生成代码，但如果只给它一句“帮我优化”，它可能会：

- 优化错热点。
- 生成不正确代码。
- 忽略真实输入分布。
- 写出难维护的黑盒。
- 用更复杂的代码换来很小收益。

文章的解决方式是：把 BenchmarkDotNet 输出的证据交给 AI，让 AI 分析实际瓶颈。

### B.3 解决“只知道快慢，不知道原因”的问题

`Mean` 告诉你结果，但不告诉你原因。

文章通过 `DisassemblyDiagnoser` 和 `HardwareCounter` 让优化者看到：

```text
JIT 是否向量化
是否有 bounds check
是否有 movsxd 这类多余指令
是否存在 scalar remainder loop
分支是否频繁误预测
```

这样 AI 和人类都可以围绕原因继续迭代。

## C. BenchmarkDotNet 工作流

### C.1 推荐基础配置

文章推荐 BenchmarkDotNet 配置中至少加入：

```csharp
var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(Job.ShortRun)
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
        maxDepth: 3,
        printSource: true,
        exportGithubMarkdown: true,
        exportCombinedDisassemblyReport: true)))
    .AddExporter(JsonExporter.Full);
```

这些配置的作用：

| 配置 | 作用 |
|---|---|
| `Job.ShortRun` | 快速跑一轮 benchmark，便于迭代 |
| `MemoryDiagnoser` | 输出内存分配 |
| `DisassemblyDiagnoser` | 输出 JIT 生成的机器码 |
| `JsonExporter.Full` | 导出完整 JSON，方便 AI 或工具读取 |

### C.2 分支相关分析

如果代码中有很多 `if / else`，文章建议加入：

```text
HardwareCounter.BranchMispredictions
HardwareCounter.BranchInstructions
```

它们用于观察：

```text
执行了多少分支
分支预测失败了多少次
每次操作大约有多少误预测
```

注意：

- Windows 上硬件计数器可能需要管理员权限。
- 不同终端、IDE、AI 工具是否继承管理员权限并不稳定。
- 不同 CPU 对硬件计数器支持不同。

### C.3 迭代循环

文章推荐的完整循环：

```text
1. 为一个小函数写 benchmark
2. 跑 BenchmarkDotNet
3. 收集 Mean / Allocated / asm / branch counter / JSON
4. 让 AI 分析瓶颈
5. 生成候选实现
6. 再跑 benchmark
7. 检查正确性、输入分布和可维护性
8. 决定是否采用
```

## D. 示例一：数组求和优化

### D.1 基线实现

文章第一个示例是数组求和：

```csharp
[Benchmark(Baseline = true)]
public int ForLoop()
{
    var d = data;
    int sum = 0;

    for (int i = 0; i < d.Length; i++)
    {
        sum += d[i];
    }

    return sum;
}
```

### D.2 优化路线

文章比较了多种实现：

```text
普通 for
foreach
LINQ Sum
Vector<T>
ref / unsafe 风格访问
Vector512<int>
循环展开
AVX-512 mask 尾部处理
TensorPrimitives.Sum<int>
```

大致结果趋势：

| 方法 | 大致耗时 |
|---|---:|
| ForLoop / ForEachLoop | 约 211 ns |
| LinqSum | 约 59 ns |
| VectorSimd | 约 36 ns |
| VectorRef | 约 32 ns |
| Vector512Unrolled | 约 22 ns |
| Vector512Final | 约 18 ns |
| TensorPrimitives.Sum | 约 13 ns |

### D.3 这个示例说明什么

这个示例的重点不是“所有项目都要手写 AVX-512”，而是说明：

- 普通循环不一定被 JIT 自动向量化。
- `Vector<T>` 能利用 SIMD，但可能仍有边界检查。
- `ref` / unsafe 风格访问可以减少部分边界检查。
- `nuint` 索引可能减少符号扩展指令。
- 循环展开和多个 accumulator 能减少依赖链。
- 标准库里的 `TensorPrimitives.Sum<int>` 已经非常强。

对普通项目来说，最重要的结论是：

```text
先看标准库有没有更好的实现，再决定是否手写底层优化。
```

## E. 示例二：MessagePack `TryWriteInt32`

### E.1 问题背景

第二个示例是优化 MessagePack 写入 `int` 的函数。

MessagePack 根据数值范围会写不同格式：

```text
positive fixint
negative fixint
uint8
uint16
uint32
int8
int16
int32
```

传统实现通常是很多 `if / else`：

```text
if value fits positive fixint
else if value fits negative fixint
else if value fits uint8
else if value fits uint16
...
```

这种写法在输入很规律时还可以，但输入混合随机时，CPU 很难预测下一次会走哪个分支。

### E.2 输入分布设计

文章设计了三种输入：

| 输入分布 | 含义 |
|---|---|
| `Small` | 全是 `-32` 到 `127` 的小整数 |
| `Large` | 全是大整数 |
| `Mixed` | 各种大小随机混合 |

其中 `Mixed` 最能暴露分支预测失败问题。

### E.3 关键结果

文章中的重点结果大致是：

| 方法 | Mixed 耗时 | 分支误预测 |
|---|---:|---:|
| Cascade | 约 8.53 ns | 约 1/op |
| CascadeUnsafe | 约 8.27 ns | 约 1/op |
| BranchlessTable | 约 1.72 ns | 约 0/op |
| Branchless2 | 约 1.64 ns | 约 0/op |

这里最重要的结论是：

```text
unsafe 本身没有解决根本问题。
减少分支预测失败才是主要收益来源。
```

### E.4 Branchless 方案做了什么

文章中的 branchless 方案大致使用：

```text
BitOperations.LeadingZeroCount
符号位
bit length
查表
打包 metadata
Unsafe.Add
unaligned write
```

它把多个 `if / else` 转成：

```text
算一个索引
查表得到格式信息
统一写入
```

优点：

- 分支少。
- Mixed 输入下更稳定。
- 分支误预测接近 0。

缺点：

- 可读性差。
- 维护成本高。
- 需要大量正确性测试。
- 需要理解底层原因才能安全修改。

## F. AI 在文章中的角色

### F.1 AI 不是替代 benchmark

文章中 AI 的价值不是“直接生成最快代码”，而是参与证据驱动循环。

AI 可以做：

- 生成 benchmark scaffold。
- 阅读 BenchmarkDotNet 输出。
- 阅读 JIT 汇编。
- 结合 branch counter 判断瓶颈。
- 生成下一版候选实现。
- 发现 benchmark 设计问题。

### F.2 人类仍然必须负责判断

文章强调，人类仍然需要负责：

- 判断 benchmark 是否测到了真实场景。
- 判断输入分布是否合理。
- 判断优化代码是否正确。
- 判断复杂度是否值得。
- 确认每一行 AI 生成代码都能被维护者理解。

尤其是涉及下面这些技术时，不能盲目合入：

```text
unsafe
SIMD
位运算
查表
非对齐写入
硬件指令
```

### F.3 文章对模型能力的观察

作者提到，Fable 5 在该任务中表现非常强，可以基于 asm 和分支预测数据生成比作者自己更快的实现。

但文章也指出，这类结果依赖：

- 问题范围小。
- benchmark 明确。
- 证据密度高。
- 输出可以反复验证。

这说明 AI 最适合做：

```text
小范围、强约束、可验证的性能优化任务。
```

## G. 优点

### G.1 方法论清晰

文章给出的是流程，不只是技巧：

```text
测量
解释
改写
再测量
再解释
```

这比记住某个性能技巧更有价值。

### G.2 强调证据

文章强调：

```text
Mean
Allocated
JIT asm
Code Size
Branch Instructions
Branch Mispredictions
```

这能避免凭感觉优化。

### G.3 强调输入分布

同一个函数在不同输入下可能表现完全不同。

文章通过 `Small`、`Large`、`Mixed` 说明：

```text
如果真实数据是混合随机的，就不能只测小整数或大整数。
```

### G.4 对 AI 定位清醒

文章没有说“AI 一定写得更好”，而是说：

```text
给 AI 更多证据，AI 才可能稳定地产出更好候选实现。
```

## H. 风险和局限

### H.1 Microbenchmark 可能误导

小函数 benchmark 只能说明：

```text
这个小片段在这个测试条件下更快。
```

它不能直接证明：

```text
真实项目整体也更快。
```

真实项目还会受到这些影响：

- 缓存命中。
- 调用频率。
- GC 压力。
- 线程调度。
- 数据布局。
- 上下游代码。
- 真实输入分布。

### H.2 极限优化可能牺牲可维护性

Branchless、SIMD、unsafe、查表、位运算这类优化可能非常快，但也更难理解。

如果不是极热路径，可能不值得。

### H.3 AI 可能生成黑盒代码

如果 AI 生成的代码维护者看不懂，就不应该合入。

尤其是开源库或长期项目，代码本身是资产。性能优化不能只看速度，还要看未来能不能维护。

### H.4 Benchmark 配置也会影响结果

文章提醒：

- `Job.ShortRun` 适合快速迭代，但精度有限。
- 硬件计数器在不同平台支持不同。
- Windows 上可能需要管理员权限。
- 分支预测器可能记住固定测试序列，导致误预测数据偏低。
- AVX-512 不一定在目标机器上可用。

## I. 对 MechStorm 和 C# 项目的实践参考

### I.1 当前 MechStorm 不需要极限优化

MechStorm 当前仍在 P1 / Sprint 2，重点是：

```text
BattleSession
多单位
移动
普通攻击
死亡
回合推进
Result
Snapshot
ActionLog
```

当前最重要的是：

```text
写对
写清楚
可测试
边界稳定
```

不应该现在就引入 unsafe、SIMD、硬件计数器等复杂优化。

### I.2 未来适合 benchmark 的模块

后续这些模块可能适合 BenchmarkDotNet：

```text
BFS / A* 移动范围
LOS 计算
AOE 覆盖格计算
目标筛选
Tag 查询
Modifier 聚合
DamageBreakdown 构建
BattleSnapshot 序列化
Command Replay 回放
AI Action-Location Pair 扫描
配置查询
```

### I.3 推荐项目内性能优化流程

MechStorm 后续如果要优化某个热路径，建议流程是：

```text
1. 先用真实场景确认它是热路径
2. 写最小 BenchmarkDotNet benchmark
3. 设计多组输入数据
4. 加 MemoryDiagnoser / DisassemblyDiagnoser / JsonExporter
5. 必要时加 HardwareCounter
6. 让 AI 基于结果提出候选实现
7. 补正确性测试
8. 比较可读性、维护性和收益
9. 决定是否采用
```

### I.4 对 AI 协作的启发

这篇文章给 MechStorm 的 AI 协作也有启发：

```text
不要只让 AI 写代码。
要让 AI 看测试、benchmark、日志、快照和诊断数据。
```

这和战斗系统的调试方向一致：

```text
BattleActionLog
BattleSnapshot
DamageBreakdown
TargetValidationReport
TriggerExecutionLog
```

AI 在有证据时更可靠。

## J. 一句话总结

这篇文章的核心价值是：

> AI 时代的高性能 C# 写法，不是让 AI 凭感觉生成“看起来很快”的代码，而是用 BenchmarkDotNet 产出足够多的证据，让 AI 和人一起基于证据迭代。

对 MechStorm 来说，当前不需要极限优化，但未来做寻路、目标筛选、Tag / Modifier、Replay、AI 扫描和序列化时，可以采用这套流程。
