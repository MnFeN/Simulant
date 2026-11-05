# 机制时间轴开发

机制脚本通过 `SimTimeline` 描述“何时执行什么”，由每次模拟的 `SimScheduler` 执行。节点存放在按时间排序的队列中，同一个模拟的节点在一条专用后台线程上串行运行。等待下一个节点时线程休眠，不为每个延迟创建 `Task`、计时器或异步状态机。

## 从一个预设开始

逻辑类继承 `SimLogicBase`，实现 `BuildTimeline`。基类在调度线程上调用它，因此可以在这里初始化实体；返回后基类会自动播放传入的根时间轴，不需要再次调用 `Play(timeline)`。

以下示例展示定义方式，可以放在本项目的预设逻辑类中。技能 ID 和时间应按实际机制调整。

```csharp
using Simulant.Core;
using Simulant.Simulation;
using Simulant.Simulation.Timeline;
using System;
using System.Numerics;

internal sealed class ExampleTimelineLogic : SimLogicBase
{
    public ExampleTimelineLogic(PluginHost host, SimPresetBase preset)
        : base(host, preset)
    {
    }

    protected override void BuildTimeline(SimTimeline timeline)
    {
        var boss = EntityManager.SpawnBNpc(15725, 12256, 90);
        boss.Pos3D = new Vector3(100, 100, 0);
        boss.Heading = (float)Math.PI;
        boss.SetReadyToDraw();
        boss.EnableDraw();

        var mechanic = new SimTimeline();
        mechanic.At(0, () => boss.Cast(0x7BA2), "本体读条");
        mechanic.After(2, () => _host.LogSim("读条后 2 秒"), "阶段提示");
        mechanic.Repeat(4, 2, 3,
            i => _host.LogSim($"第 {i + 1} 次判定"), "重复判定");
        mechanic.At(10, () => EntityManager.Delete(boss), "回收本体");

        // 准备 3 秒，再播放机制；mechanic 中的秒数以机制开始为零点。
        timeline.At(3, mechanic);
    }
}
```

示例中的读条、提示、三次判定和回收，分别计划在根时间轴开始后的第 3、5、7/9/11、13 秒执行。基类在 `BuildTimeline` 返回后以当时的实际时钟建立根时间轴的原点，初始化实体的耗时不会挤占示例中的 3 秒准备阶段。初始化工作仍应保持简短，因为它与机制节点共用调度线程。

## 定义节点

所有时间参数单位为秒，必须是有限的非负数。

| API | 含义 |
| --- | --- |
| `At(seconds, action, name)` | 在当前时间轴的指定秒数执行动作。 |
| `After(seconds, action, name)` | 相对最后添加的节点时间，再向后指定秒数。 |
| `Repeat(first, interval, count, action, name)` | 展开为 `count` 个节点，时间为 `first + interval * i`，回调参数 `i` 从 0 开始。 |
| `At(seconds, child)` | 将子轴当前的节点快照复制到指定偏移处。 |

这些方法返回当前时间轴，可以连续调用。给节点命名能够帮助异常日志定位机制阶段。

`After` 的游标是**最后添加的节点**，不是所有节点中的最晚时间。例如：

```csharp
var timeline = new SimTimeline();
timeline.At(10, () => _host.LogSim("第 10 秒"));
timeline.At(2, () => _host.LogSim("第 2 秒"));
timeline.After(1, () => _host.LogSim("第 3 秒"));
```

组合子轴后，游标变为最后复制的节点在父轴中的时间；空子轴不改变游标。`Repeat` 后游标位于最后一个重复节点，`count` 为 0 时不改变游标。无需按时间顺序添加节点，执行时会按计划时间排序。

子轴组合和播放都取节点快照。组合后修改原子轴、或播放后继续修改定义，都不会改变已经入队的节点。快照保存的是回调引用，不会深复制回调捕获的实体或可变状态；不要把绑定旧实体的时间轴跨模拟复用。

## 运行时生成子轴

已知时间和位置时，优先在 `BuildTimeline` 中用 `At(offset, child)` 完成组合。需要在某个时刻读取玩家位置、随机选取目标或创建实体时，在节点内构造子轴并调用 `Play`：

```csharp
// 以下成员放在 SimLogicBase 的派生类中。
protected override void BuildTimeline(SimTimeline timeline)
{
    timeline.At(3, () =>
    {
        var position = _host.EntityProvider.GetMyself().Pos3D;
        Play(BurstTimeline(position));
    }, "记录位置并开始连续判定");
}

private SimTimeline BurstTimeline(Vector3 position)
{
    var burst = new SimTimeline();
    burst.At(0, () => DummyExecute(position, 0f, 0x7BA4, 3f), "第一击");
    burst.After(1.5, () => DummyExecute(position, 0f, 0x7BA4, 3f), "第二击");
    return burst;
}
```

这里两击的计划时间为第 3、4.5 秒。`DummyCast` 和 `DummyExecute` 已经是同步方法：调用时创建假人并开始技能，回收节点由内部安排，直接调用即可。

`Play(child, delaySeconds)` 还可以给整个子轴增加偏移。在节点回调中调用时，其原点为**当前节点的计划时间**。假设第 10 秒节点实际到第 10.2 秒才执行，在它里面播放第 2 秒有判定的子轴，判定仍计划在第 12 秒，不会顺延到第 12.2 秒。已经过期的节点会尽快执行，调度器不会跳过它们，也不能消除已经发生的延迟。

从调度线程以外调用底层 `SimScheduler.Play` 时，以调用时的实际时钟为原点。普通预设应尽量在节点内部完成运行时分支，避免外部线程直接操作会话实体。

## 机制时刻与实际生命周期

机制节点使用 `At`、`Repeat` 和 `Play` 表达计划时刻。底层调度器另有两个返回取消句柄的方法：

| API | 时间基准 |
| --- | --- |
| `Scheduler.Schedule(seconds, action, name)` | 相对调度器 `Start` 的绝对秒数。 |
| `Scheduler.ScheduleAfter(seconds, action, name)` | 相对调用时的实际经过时间，即使在节点内部调用也是如此。 |

例如迟到的节点现在才创建一个需要显示 3 秒的假人，应在实际创建后保留 3 秒，因此内部回收采用 `ScheduleAfter(3, ...)`。把机制的下一步写成 `ScheduleAfter` 则会把本次迟到带到后续步骤；这种情况应定义子轴并 `Play`。

经 `EntityManager` 管理的 `Character.Cast` 会使用所属会话的调度器安排读条完成，停止模拟时这些待执行动作也会取消。读条完成和假人回收按实际开始时间计时。

## 同一时刻的顺序

同一调度器内，节点先按计划时间排序，时间完全相同时按入队顺序执行。定义时间轴时，同刻节点保留添加顺序；组合子轴时，其节点按子轴的定义顺序复制。

节点回调里再 `Play` 的零秒节点会进入队列，不会在 `Play` 调用栈内立即执行。如果队列里已经有其他同刻节点，新节点排在这些节点之后。依赖“必须先发生”的动作可以放在同一个短回调中，或提前组合子轴。

## 取消与异常

`Schedule` 和 `ScheduleAfter` 返回 `SimScheduledAction`，可以取消单个尚未开始的节点。以下代码用于本项目内的预设：

```csharp
SimScheduledAction pending = null;

timeline.At(1, () =>
{
    pending = Scheduler.ScheduleAfter(5,
        () => _host.LogSim("后备动作"), "可取消的后备动作");
}, "安排后备动作");

timeline.At(3, () =>
{
    if (pending != null && pending.Cancel())
        _host.LogSim("后备动作已取消");
}, "取消后备动作");
```

`Cancel()` 成功移除待执行节点时返回 `true`；已被调度线程取走、已完成或已取消时返回 `false`。句柄的 `Dispose()` 等价于取消，`IsCompleted` 在执行完成或取消后为 `true`。不要用立即结束的 `using` 包裹仍希望执行的句柄。

目前 `At`、`Repeat` 和 `Play` 不返回整组节点的取消句柄；它们仍受会话整体停止控制。`Scheduler` 和 `EntityManager` 在逻辑基类中为项目内可访问成员，这些示例面向本仓库内的预设开发。

节点抛出同步异常时，调度器先取消所有待执行节点，再报告异常和节点名称；会话随后调用停止清理。初始化节点中的异常走相同流程。停止模拟会取消队列并等待当前同步节点返回，再回收实体，避免旧节点在清理后继续访问原生地址。取消不会强制中断已经运行的回调。

底层 `SimScheduler.Cancel()` 只取消队列，不等待当前回调；`Dispose()` 在外部线程调用时还会等待工作线程退出。取消后的调度器不可重新启动，下一轮模拟会创建新的调度器。

## 实体归属与停止清理

Boss、假人和临时场景对象应通过 `EntityManager.SpawnBNpc`、`SpawnPlayer`、`SpawnEObj` 创建。必须使用其他创建入口时，将返回对象交给 `EntityManager.Track(entity)`，以便会话统一回收。

正常时间轴可以用 `EntityManager.Delete(entity)` 提前回收对象。`OnStop` 用于移除额外的辅助线等外部效果，不必重复回收已登记实体；基类会在 `finally` 中移除 PictoACT 中标记为 `Simulant` 的技能预兆，再调用 `EntityManager.Clear()`。延迟出现的技能预兆也由会话调度器安排，停止时会取消。停止、重开模拟和节点异常都使用同一套清理流程。队列自然执行完毕不会自动结束会话，仍需由停止操作完成会话清理。

现有封装没有 `EventObject` 的原生删除接口。对 EObj 的清理目前停止其绘制，槽位等待后续生成对象的包或切换区域时复用；这不等于销毁游戏中的对象。中途停止、立即重开和不同区域的 EObj 表现仍需在游戏内验证。

## 节点保持短小、同步

所有节点共享一条调度线程，阻塞回调会推迟后续节点，也会增加停止操作的等待时间。不要在节点中 `Thread.Sleep`、忙等、等待 `Task.Result`、同步网络请求或执行长时间计算。用多个有时间偏移的节点表达延迟，确定的几何计算可以在构建子轴时预先完成。

回调类型是 `Action`。C# 允许向它传入 `async` lambda，这会隐式变成 `async void`；调度器无法等待它，`await` 之后的代码也会脱离节点的异常处理、执行顺序和停止边界。**不要向时间轴传入 `async` lambda 或 `async void` 方法。** API 会检查回调及多播委托中的条目，拒绝直接的 `async void` 回调；同步包装方法在内部启动异步工作的情况无法通过这一检查识别，同样禁止用于节点。

每个节点不创建独立任务，并不意味着节点中的游戏调用没有开销，也不保证实时执行精度。大批到期节点、昂贵回调和游戏本身的负载仍会影响实际表现。

## 后续扩展的边界

`SimTimeline` 负责节点定义和组合，`SimScheduler` 负责时钟、排序、执行与取消。这一分离为以后调整时钟策略或增加结构化节点提供了位置。

当前没有暂停、倍速、任意跳转、存档恢复、序列化时间轴或整组节点取消功能。节点仍是捕获运行时对象的 C# 委托，不能直接保存为可恢复的机制状态；新增这些能力需要同时设计状态、实体生命周期和迟到节点的处理规则。

现有迁移示例见：

- [宇宙天箭](../Simulant/Content/U6b/Presets/宇宙天箭.cs)：构建时组合多条完整子轴。
- [绝伊甸 P5 地火](../Simulant/Content/U7a/Presets/P5地火.cs)：重复分组节点中播放依赖运行时实体的子轴。
- [异三角](../Simulant/Content/S4b/Presets/异三角.cs)：运行时随机分配对象并安排动画与判定。
- [U7b P5 地火](../Simulant/Content/U7b/Presets/P5地火.cs)：交错子轴、固定间隔判定与收尾节点。
