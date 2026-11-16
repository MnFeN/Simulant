# 实体与模拟角色

实体按职责分成三层，模拟角色通过组合使用游戏实体。

| 层 | 类型 | 职责 |
| --- | --- | --- |
| Native | `Game/FFCS` 下的结构、指针与包 | 游戏内存布局、字段读写、原生函数调用。 |
| Entity | `EntityBase`、`Character`、`EventObject` | 实体身份和有效性、坐标转换、属性、立即执行的游戏操作。 |
| Simulation | `SimActor` | 角色在本次模拟中的读条和移动。通过会话共享的控制器安排持续行为。 |

`Character` 不持有宿主、游戏数据服务、调度器或预兆绘制逻辑。`SimActor.Entity` 暴露底层角色，以便使用不需要重复转发的即时操作，例如 `actor.Entity.PlayBlendTimeline(id)`。没有通用工厂、组件容器或额外实体继承树。

## 创建与借用

在 `SimLogicBase.BuildTimeline` 或时间轴节点内使用会话的 `EntityManager`：

```csharp
EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
{
    BNpcBaseId = 15725,
    BNpcNameId = 12256,
    Level = 90,
    Pos = new Vector3(100, 100, 0),
    Heading = (float)Math.PI
}, boss =>
{
    var mechanic = new SimTimeline();
    mechanic.At(0, () => boss.Cast(0x7BA2));
    mechanic.At(10, () => EntityManager.Delete(boss));
    Play(mechanic);
});
```

现有预设的 Boss、技能假人和队员统一使用网络角色；先开启防火墙，再开始预设。生成回调拿到就绪实体后才启动机制时间轴，避免把模型登记的等待时间算入读条。技能假人通过 `Targetable = false` 保持不可选中，寿命从就绪并开始施法时计算。

`CharacterSpawnSpec` 和 `EventObjectSpawnSpec` 只描述创建参数。`EntitySpawner` 负责创建和回收资源，`SimEntityManager` 记录其中哪些资源属于当前会话。

网络角色使用 `EntityManager.SpawnNetworkBNpc(spec, onReady)` 或 `SpawnNetworkPlayer(job/spec, onReady)`，在客户端完成登记和必要入场控制后交付角色；同步本地入口保持可用。两种来源由生成器配对回收，参数、职业套装及使用边界见 [网络角色生成](NetworkEntities.md)。

`Spawn` 取得回收责任；`Attach` 只借用。对同一实体重复 `Attach` 返回本会话现有的活动 `SimActor`。`Detach` 或 `actor.Dispose()` 停止行为，保留底层实体；`Delete` 仅回收本会话生成的实体，借用玩家不会被删除。即便先 `Detach`，本会话生成的角色仍会在停止时回收。

`GeneratePartyMembers(myPartyIdx, onReady)` 每次模拟会话只准备一次小队，按原职能顺序等待全部网络玩家就绪，登记原生小队列表后再回调；自身玩家仍然借用。停止并完成清理后，可以在下一轮重新准备。

## 原生小队列表

`SimEntityManager.Party` 保留机制使用的职能顺序；`EntityManager.ShowParty(_host.PartyService)` 把自己和其余网络玩家登记到原生主小队，人数为 2-8 人。原生记录将自己放在第一位，界面最终显示顺序仍受游戏的小队排序设置影响。现有 `GeneratePartyMembers` 已自动完成这一步。

名单使用客户端 `HandleUpdatePartyPacket` 收包入口更新。小队状态图标可以按实体 ID 从战斗实体读取，但只改角色的 `HP` 字段不足以同步小队血条，需要血量／状态收包管线。不新增界面绘制、不周期性重发整张名单，也不向服务器发送邀请或离队操作。生成队员必须使用网络管线，本地备用角色不能入队。

结构按项目的 `Ptr/MemoryField` 方式迁入，参考固定版本 [FFCS GroupManager](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Group/GroupManager.cs)、[FFCS Hate](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/UI/Hate.cs) 及 [AnoMech 原生小队收包用法](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/PartyHud.cs)。这些参考不是国服布局实测结果，因此提交后会读回人数与身份确认名单；图形界面仍需实机验证。

网络角色使用 `member.Entity.SetNetworkVitals(hp, mp)` 更新血蓝；省略 `mp` 保留现值。它发送完整 `StatusEffectListDouble`，保留 60 个状态的槽位、时间、参数和来源实体 ID，以及角色最大血蓝、职业、等级和护盾。状态也可继续使用原生 `StatusManager.SetStatus/RemoveStatus`。原有 `HP` 属性仍是即时内存访问，保留给明确需要裸写的场合。

这与 [Sapphire 的血量更新流程](https://github.com/SapphireServer/Sapphire/blob/aab9cb2347eb0ec42e85ee3e117a0c177b9b3070/src/world/Actor/Chara.cpp) 一致：ActorControl 处理飘字，另发 HudParam 完整血蓝／状态；当前客户端使用已核对的 60 槽收包布局，不能直接复制私服旧版 30 槽协议。

机制可以按 `Party` 的顺序提供当前目标的仇恨百分比：

```csharp
EntityManager.SetPartyEnmity(boss, 100, 80, 20, 15, 40, 35, 30, 25);
```

该调用不计算技能伤害或自动生成仇恨；数值为 0-100，目标必须是本地模拟生成的有效战斗 NPC。发送前按仇恨降序排列，通过原生 UpdateHate 收包入口更新。会话共用一个每 250 ms 的检查节点，仅在选中相应 Boss 且目标切换或仇恨数据变化时发送，不按角色分配计时器。它不替用户选择目标，也不自动改变 Boss 的攻击目标。

当前国服客户端的 UpdateHate 收包函数只更新成员 ID、百分比和人数，不写 `HateTargetId`（`Hate + 0x104`）。读回校验不能要求该目标 ID 与发包参数一致；发送前单独确认选中目标即可。显示数据暂时未同步时留待下一次补发，不抛异常中断模拟。只需假队员攻击、Boss 固定点名的预设直接用 `SetCombatTarget` 和技能目标，无须建立仇恨列表。

自身原先未入战时，通过 `ActorControl SetBattle (0x04, 1)` 临时入战，停止时恢复；原先已入战则保持原状态。此流程参考 [Sapphire Player.cpp](https://github.com/SapphireServer/Sapphire/blob/aab9cb2347eb0ec42e85ee3e117a0c177b9b3070/src/world/Actor/Player.cpp)，当前目标仇恨包和函数入口使用 [国服 KodakkuAssist 的定义](https://github.com/moewcorp/KodakkuAssist/blob/e5bd79f841fe483e14a3dd22a04f845eab239026/KodakkuAssist/Module/GameEvent/Types/EnmityUpdate.cs)。原生血条与仇恨显示需要游戏内验证，步骤见 [实体实机测试](EntityTests.md)。

首次显示前保存原小队名单与仇恨；恢复通过同一个原生收包入口完成，再补回全部 60 个状态及其计数标志、成员姓名、坐标、护盾和标志。快照不复制原生对象指针。只允许一个会话借用原生小队，暂不覆盖团队名单。

停止、关闭防火墙、插件禁用以及插件主动切图均先恢复小队，再回收模拟角色。恢复失败会保留所有权并阻止删除队员，下次停止重试。主动删除或解除关联队员时也先更新名单。外部换图、退出或切换游戏进程使旧世界失效；随后不会向新世界写入旧快照。切换到另一个仍在运行的客户端时，不尝试跨进程恢复旧客户端，应先停止当前模拟。

## ActorControl 分类

`Simulant.Game.ActorControlCategory : ushort` 统一保存控制子类别。`EntityBase.ActorControl` 与 `PacketDispatcher.HandleActorControl` 都直接接收这个枚举，原生调用前统一转换成 `ushort`，不把枚举对象交给 GreyMagic。已有业务调用的类别编号、参数顺序及数值保持原样。

```csharp
using Simulant.Game;

boss.Entity.ActorControl(ActorControlCategory.SetBattle, 1);
boss.Entity.ActorControl(ActorControlCategory.TargetIcon, markerId);
eventObject.ActorControl(ActorControlCategory.SetEventState, eventState);
eventObject.ActorControl(ActorControlCategory.SetSharedGroupTimelineState, timelineState);
```

普通实体入口提供四个参数。需要 `ActorControlSelf` 的额外参数、或 `ActorControlTarget` 的目标 ID 时，使用完整的 `PacketDispatcher.HandleActorControl` 入口；例如 `SetTarget` 的目标应传入 `targetId`，不能误放到 `a1`。未知类别可显式转换为 `(ActorControlCategory)rawCategory`，方便后续回放解析；是否能够执行仍取决于对应版本的参数布局。

协议参考：

- [BossMod 控制分类](https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod/Network/ServerIPC.cs)：用于交叉核对类别编号。
- [Sapphire 控制分类](https://github.com/SapphireServer/Sapphire/blob/master/src/common/Network/CommonActorControl.h)：用于核对常用操作名称和参数说明；它对应的客户端版本不能直接等同于国服 7.57。
- [FFXIV-Packet-Dissector 枚举](https://github.com/zhyupe/FFXIV-Packet-Dissector/blob/3d9c3b6b0e0067e626a0b75f958c5ac21e65a835/src/ffxiv_enum.lua)：核对历史分类名称与数值。
- [Machina 国际服定义](https://github.com/ravahn/machina/blob/2bd9a7d291e1e54ce523a4c31daa93919e1c44fa/Machina.FFXIV/Headers/Global/Server_ActorControl.cs)：注明持续治疗与持续伤害从 7.10 起使用 `0x604`、`0x605`。[同提交的国服定义](https://github.com/ravahn/machina/blob/2bd9a7d291e1e54ce523a4c31daa93919e1c44fa/Machina.FFXIV/Headers/Chinese/Server_ActorControl.cs) 仍保留旧 `0x17`；因此保留三项，不据此宣称新参数已在国服实测。

不同来源的命名冲突保留区分：`SetMaxGearSets = 0x230` 与 `SetMaxGearSetsSapphire = 0x320` 不是别名；Sapphire 所称的 `SetDutyActionActive = 0x5EA` 对应这里的 `SetDutyActionPresent`，这里的 `SetDutyActionActive` 使用 `0x5EB`。`GainEffect` 等提示类别不能代替真实状态列表或血量同步。枚举属于包内子类别，不是回放文件中需要按版本识别的顶层 opcode。

## 会话回收

从调度线程外停止时，顺序是：取消时间轴并等待当前节点退出，停止所有角色行为，清除本次读条预兆，最后回收自有实体。调度回调内停止会话不能等待自身退出，调用后应立即 `return`，不要继续访问已清理的实体。停止后的管理器不再接受生成或关联；清理完成后重新开始会话时绑定新的调度器。

`Clear()` 逐项回收实体和取消尚未就绪的网络请求：成功项移出记录，失败项保留，单项异常不妨碍其余资源清理。再次调用只重试尚未完成的资源，不会重新删除已成功回收的实体。清理进行中或仍有失败记录时，不能更换调度器开始下一轮。

本地角色在交付前初始化失败、立即回滚也失败时，生成器会单独保留分配记录。任一共享此生成器的会话清理时都可重试这些未交付资源，不会因此回收另一会话的成功实体；清理完成前不能开始新会话。重试需确认原进程、对象管理器、槽位、地址及身份仍匹配；创建时始终未能确认身份的记录保留至世界重置，不认领后来出现的同槽对象。

会话的 `OnStop` 每轮只调用一次；后续 `Stop()` 或 `Dispose()` 只重试剩余实体清理。界面会保留清理失败的会话，禁用“开始模拟”，保留“停止模拟”按钮供再次重试。清理成功后可以重新开始；换图后旧世界记录失效，后续清理只移除旧记录，不访问旧指针。

## 读条与取消

`Cast` 未指定 `castTime` 时，以技能表时长减 0.3 秒作为实际咏唱，最低为零；显式时长直接使用，不再扣除。读条结束后默认间隔 0.3 秒播放结算，瞬发默认无间隔，`effectDelay` 可覆盖。

若机制判定节点已经负责技能演出，传 `executeOnFinish: false`，避免自动完成再次播放。自动战斗允许读条完成后移动，但下一招等待上一招结算；`StopCasting` 会取消尚待播放的结算。

```csharp
boss.Cast(0x7BA2);
boss.StopCasting();
```

`StopCasting()` 取消该角色在本会话安排的读条完成节点、延迟预兆和已显示的预兆，并按原技能编号结束读条；不会删除角色或停止其移动。没有本会话读条记录时，不改动角色的外部读条。

## 移动

`MoveForMechanic` 的目标时间只用于安排出发、删去绕路和按需冲刺。接令时来不及、途中迟到或尚未精确走到随机终点，都不会单独判为机制失败或中断模拟；队员继续在 1.3 倍速上限内赶路，直到到达、收到新指令或停止。分摊、踩塔等由机制根据判定时的实际位置确定参与情况。

需要击退后先突进再就位时，传 `gapClose: true`。实体等待强制移动结束后，T 和近战使用本职业突进，沿自身与 Boss 的连线移动到目标圈外 0.5 米，再继续原来选定的机制目的地。普通技能演出不负责更新逻辑坐标，因此突进另用 ActorMove 同步；这段技能位移不受普通跑步的 1.3 倍速度上限限制。远程、没有可用突进或 Boss 不可选中时直接正常走位；默认不启用，不会改变其他机制的路线，也不会代替真实玩家操作。

网络假队员需要持续对准玩家时，使用 `actor.FollowForMechanic(() => 动态站位范围, 结束时间)`。结束时间为会话绝对秒数；实体按共享行为更新读取最新范围，保持一次选定的随机偏移，并通过 ActorMove 连续修正路线。跟随立即开始，必要时使用最多 1.3 倍速冲刺；动态目标来不及追上由机制判定，不按固定移动窗口抛错。新移动指令、停止或击退会取消跟随，不会在强制移动结束后自动恢复旧目标。

模拟实体的非初始化移动和转向优先使用 ActorMove，由客户端处理模型插值和动画；普通走位使用 `MoveNetworkTo`，原地转向使用 `FaceNetwork`：

```csharp
boss.MoveNetworkTo(new Vector2(100, 90), speedParam: 30,
    onArrived: () => boss.Cast(0x7BA2));
boss.StopMoving();
boss.FaceNetwork((float)Math.PI); // 原地转向北。
```

`MoveNetworkTo` 约每 100 ms 按经过时间发送沿途位置的 `ActorMove`，由客户端完成插值和动画；包内位置是该时刻的位置，不是远端导航终点。控制器读取实际位置确认到达，到达后执行回调，30 秒未到达则报告超时。`speedParam` 是每秒距离乘以 10 后的协议字节。停止或替换时发送当前位置，并取消旧到达回调。此方式不接受自身或本地备用表中的角色。

`FaceNetwork` 先停止本会话移动，再发送当前位置与目标朝向。初始化应优先在生成参数中填写 `Pos` 和 `Heading`；直接写位置、朝向不产生移动动画，只用于初始布置、隐藏技能假人的演出定位或本地备用实体。直接设置时必须同步有效的 `DrawObject`，否则可能只改变逻辑坐标或目标圈，模型不会跟着移动或转向。不要用直接写 `Heading` 代替机制中的网络转向。

原有本地插值保留为备用：

```csharp
boss.MoveTo(new Vector2(100, 90), speed: 6);
boss.MoveTo(new Vector2(110, 90), speed: 8, faceMovement: false);
boss.StopMoving();
bool moving = boss.IsMoving;

// 指定适用于该模型的移动动画，到达后直接进入下一步。
boss.MoveTo(new Vector2(100, 90), speed: 6, animationId: 13,
    onArrived: () => boss.Cast(0x7BA2));
```

`IsMoving`、`StopMoving()` 同时管理本会话的 `MoveTo` 和 `MoveNetworkTo`；同一角色两种移动互斥。`Detach` 或 `actor.Dispose()` 也会停止它们。直接调用 `actor.Entity.ActorMove(...)` 不会登记到控制器，不在这些接口的管理范围内。对本会话拥有的玩家或 NPC，`EntityManager.Delete(actor)` 成功回收实体后，其原生移动也随之结束；借用实体不会被删除。

- 坐标沿用项目的 ACT 约定：`X/Y` 为水平面，`Z` 为高度。移动保持当前高度，速度为世界单位/秒，必须是有限正数。
- `MoveTo` 从当前位置向目标直线推进，途中再次调用会替换目标和速度。默认面向移动方向；到达目标自动停止，不会越过目标。
- 每个会话只有一个按需排队的更新节点，约每 16 ms 更新全部移动角色。使用实际经过时间计算距离，迟到时不会补排多帧；无人移动时没有移动节点。
- 插值写入在会话调度线程上执行。实体位置操作同时更新有效的显示对象，避免只改变逻辑坐标而画面不移动。

`animationId` 可选，由调用方选择适用于模型的动画。指定后，移动控制器在更新线程启动动画，在到达、主动停止、替换或释放时结束自己的动画。移动期间开始施法会让出动画，位置仍继续推进，本次移动不会再次抢占动画。没有指定动画时只更新位置。

`onArrived` 在调度线程上执行一次，此时已经停止移动并清理所属动画，可以直接施法、开始下一段移动或删除角色。停止、改目标、删除和会话结束都会取消尚未领取的旧回调；已经领取的回调允许执行完毕，从调度线程外停止会话会等待它退出再回收实体。回调必须是短同步操作。回调中的实际延迟使用 `Scheduler.ScheduleAfter`，避免慢帧时把下一阶段当成已经过去。

底层移动接口不提供场地寻路和碰撞规避。假队员的绕路、滑步、贪刀与冲刺由通用行为控制器负责，机制只提交目的范围、目标时间和是否紧急。

## 状态造成的移动限制

通过 `EntityManager.SetStatus` 施加的状态会读取 `Status.LockMovement`，眩晕等状态无需在副本时间轴中额外锁定位置。生效时取消普通移动，期间拒绝新的普通移动指令；假队员保留机制走位意图，解除后继续规划。击退与 `WarpWithAnimation` 不属于自主移动，仍可执行。

本机使用 [FFXIVClientStructs 的 Conditions 定义](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Conditions.cs)及客户端原生设置函数，更新 `SufferingStatusAffliction`（28）。这与状态图标独立：正常收包按服务器条件位图更新该标志，`StatusManager.SetStatus` 本身不会更新它。国服当前客户端已核对步行输入入口检查权限 98、99、96，状态标志 28 在 `Permission` 表中禁止 98、99；没有拦截按键或反复写回玩家坐标。

本会话的限制状态合并生效；刷新状态延长期限，移除、被替换或达到期限后解除。只在存在限制时共用一个 50 ms 调度节点，不创建额外线程或每状态定时器。正常停止和错误中断先恢复原有的本机限制，再清理其他资源；错误中断仍保留实体与状态图标。换图或身份失效后丢弃旧记录，不将旧条件写到新玩家。副本直接调用原生状态函数或直接发送 `ActorMove` 不经过这套会话管理。

## 模拟防击退技能

`EntityManager.UseKnockbackImmunity(actor)` 按职业为假队员使用沉稳咏唱或亲疏自行，也可通过第二个参数指定。技能只产生本地演出及六秒状态，不向服务器请求施法，不计算冷却。按状态表的 `LockActions` 判断能否使用：眩晕禁止，止步仍可使用。

防火墙期间由宿主独立监听默语命令，`/e sim ac 沉稳咏唱`、`/e sim ac 亲疏自行` 和 `/e sim ac 冲刺` 始终作用于本机玩家，冲刺持续十秒。冲刺状态 50 的 `Param` 必须为 30，表示加速 30%；填 0 只会显示图标而不提高本机移速。该参数已从国服回放的完整状态包核对，也与 [AnoMech 的冲刺处理](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/UserActions/SprintHandler.cs)一致，由客户端原生状态逻辑改变移速。没有运行副本或副本已经停止时仍可使用；状态操作交给独立调度器，不阻塞日志线程。换图、关闭防火墙、切换进程或卸载时清理自己施加的状态。副本直接检测角色实际的防击退状态，不依赖施加者属于哪个会话。

`SimActor.Knockback` 默认检查这两种模拟状态；不受免疫影响的击退可传 `ignoreImmunity: true`。回返传送不属于击退，仍正常执行。状态到期、移除或槽位被替换后立即失去免疫，重复使用则刷新期限，不会被旧的到期节点提前清除。

## 身份与失效

宿主的 `EntityProvider`、`EntitySpawner` 共享一个 `EntityRegistry`。同一原生实例的查询和生成结果使用同一包装。`EntityIdentity` 由世界代次、对象槽位和实例代次构成；游戏 `Id` 和地址只是元数据，不单独用作实例身份。

本地角色、网络角色和 EObj 共用 ID 分配：创建前批读全局对象指针，并检查已有资源、等待中的网络请求和失败创建的已知身份。玩家使用 `0x10FFxxxx`，非玩家使用 `0x40FFxxxx`；分别找出当前客户端同前缀的最大编号，与本进程已经分配的编号取较大值，再从下一号继续。旧插件残留和隐藏 EObj 都会参与计算，不填补区间中的空洞；到达区间上限时明确失败，不回绕。扫描只在生成时执行，同一原生指针只读一次 ID；这不是跨客户端的全局身份分配。

访问实体前检查对象表中的地址、游戏 ID 和对象种类。槽位变化、显式回收或世界重置后，旧包装失效，`IsAlive` 为 `false`，即时内存操作拒绝执行。模拟控制器跳过失效角色，不再写入旧地址。本地生成会强制产生新代次，即便游戏复用了相同字段。

插件切换区域前先停止会话、回收资源并重置身份。ACT 报告进程变化或自然换图时，先使旧引用失效并同步禁止生成，再停止模拟；此时不会尝试访问上一世界的内存。旧会话停止、必要初始化完成后才恢复生成，避免 UI 尚未处理停止通知时旧节点继续创建对象。释放原生资源失败会保留生成器记录，后续清理仅在对象表仍匹配原实例时重试。

这些检查不能与游戏自身的对象销毁原子同步，也无法识别从未观测到空槽位、且地址/ID/种类完全相同的外部复用。它们防止已知失效引用被继续使用，不构成对任意原生竞态的保证。

EObj 通过 `HandleDespawnObjectPacket` 回收，确认原生回收成功后撤销身份；失败时保留记录供重试。移动画面、原生字段偏移、动作播放和实际回收效果仍需在游戏中验证；自动化回归覆盖身份、会话归属、调度和行为清理。
