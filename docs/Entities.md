# 实体与模拟角色

实体按职责分成三层，模拟角色通过组合使用游戏实体。

| 层 | 类型 | 职责 |
| --- | --- | --- |
| Native | `Game/FFCS` 下的结构、指针与包 | 游戏内存布局、字段读写、原生函数调用。 |
| Entity | `EntityBase`、`Character`、`EventObject` | 实体身份和有效性、坐标转换、属性、立即执行的游戏操作。 |
| Simulation | `SimActor` | 角色在本次模拟中的读条和移动。通过会话共享的控制器安排持续行为。 |

`Character` 不持有宿主、CSV、调度器或预兆绘制逻辑。`SimActor.Entity` 暴露底层角色，以便使用不需要重复转发的即时操作，例如 `actor.Entity.PlayBlendTimeline(id)`。没有通用工厂、组件容器或额外实体继承树。

## 创建与借用

在 `SimLogicBase.BuildTimeline` 或时间轴节点内使用会话的 `EntityManager`：

```csharp
var boss = EntityManager.SpawnBNpc(new CharacterSpawnSpec
{
    BNpcBaseId = 15725,
    BNpcNameId = 12256,
    Level = 90,
    Pos = new Vector3(100, 100, 0),
    Heading = (float)Math.PI
});
boss.SetReadyToDraw();
boss.EnableDraw();

// 也保留简短写法：SpawnBNpc(baseId, nameId, level)、SpawnPlayer(job)。
var player = EntityManager.Attach(_host.EntityProvider.GetMyself());

timeline.At(0, () => boss.Cast(0x7BA2));
timeline.At(10, () => EntityManager.Delete(boss));
```

`CharacterSpawnSpec` 和 `EventObjectSpawnSpec` 只描述创建参数。`EntitySpawner` 负责创建和回收本地资源，`SimEntityManager` 记录其中哪些资源属于当前会话。

`Spawn` 取得回收责任；`Attach` 只借用。对同一实体重复 `Attach` 返回本会话现有的活动 `SimActor`。`Detach` 或 `actor.Dispose()` 停止行为，保留底层实体；`Delete` 仅回收本会话生成的实体，借用玩家不会被删除。即便先 `Detach`，本会话生成的角色仍会在停止时回收。

停止顺序是：取消时间轴并等待当前节点退出，停止所有角色行为，清除本次读条预兆，最后回收自有实体。停止后的管理器不再接受生成或关联；重新开始会话时绑定新的调度器。

## 移动

```csharp
boss.MoveTo(new Vector2(100, 90), speed: 6);
boss.MoveTo(new Vector2(110, 90), speed: 8, faceMovement: false);
boss.StopMoving();
bool moving = boss.IsMoving;

// 指定适用于该模型的移动动画，到达后直接进入下一步。
boss.MoveTo(new Vector2(100, 90), speed: 6, animationId: 13,
    onArrived: () => boss.Cast(0x7BA2));
```

- 坐标沿用项目的 ACT 约定：`X/Y` 为水平面，`Z` 为高度。移动保持当前高度，速度为世界单位/秒，必须是有限正数。
- `MoveTo` 从当前位置向目标直线推进，途中再次调用会替换目标和速度。默认面向移动方向；到达目标自动停止，不会越过目标。
- 每个会话只有一个按需排队的更新节点，约每 16 ms 更新全部移动角色。使用实际经过时间计算距离，迟到时不会补排多帧；无人移动时没有移动节点。
- 插值写入在会话调度线程上执行。实体位置操作同时更新有效的显示对象，避免只改变逻辑坐标而画面不移动。

`animationId` 可选，由调用方选择适用于模型的动画。指定后，移动控制器在更新线程启动动画，在到达、主动停止、替换或释放时结束自己的动画。移动期间开始施法会让出动画，位置仍继续推进，本次移动不会再次抢占动画。没有指定动画时只更新位置。

`onArrived` 在调度线程上执行一次，此时已经停止移动并清理所属动画，可以直接施法、开始下一段移动或删除角色。停止、改目标、删除和会话结束都会取消尚未领取的旧回调；已经领取的回调允许执行完毕，会话停止会等待它退出再回收实体。回调必须是短同步操作。回调中的实际延迟使用 `Scheduler.ScheduleAfter`，避免慢帧时把下一阶段当成已经过去。

当前实现不含寻路、碰撞、重力、路径曲线或自动选择跑步动画。后续路径或追踪行为可扩展移动控制器，保持现有 `MoveTo` 调用方式。

## 身份与失效

宿主的 `EntityProvider`、`EntitySpawner` 共享一个 `EntityRegistry`。同一原生实例的查询和生成结果使用同一包装。`EntityIdentity` 由世界代次、对象槽位和实例代次构成；游戏 `Id` 和地址只是元数据，不单独用作实例身份。

访问实体前检查对象表中的地址、游戏 ID 和对象种类。槽位变化、显式回收或世界重置后，旧包装失效，`IsAlive` 为 `false`，即时内存操作拒绝执行。模拟控制器跳过失效角色，不再写入旧地址。本地生成会强制产生新代次，即便游戏复用了相同字段。

插件切换区域前先停止会话、回收资源并重置身份。ACT 报告进程变化或自然换图时，先使旧引用失效并同步禁止生成，再停止模拟；此时不会尝试访问上一世界的内存。旧会话停止、必要初始化完成后才恢复生成，避免 UI 尚未处理停止通知时旧节点继续创建对象。释放原生资源失败会保留生成器记录，后续清理仅在对象表仍匹配原实例时重试。

这些检查不能与游戏自身的对象销毁原子同步，也无法识别从未观测到空槽位、且地址/ID/种类完全相同的外部复用。它们防止已知失效引用被继续使用，不构成对任意原生竞态的保证。

目前没有 EObj 的原生销毁接口，回收只停止绘制并使包装失效，槽位等待下次生成包或换图复用。移动画面、原生字段偏移、动作播放和实际回收效果仍需在游戏中验证；自动化回归覆盖身份、会话归属、调度和行为清理。
