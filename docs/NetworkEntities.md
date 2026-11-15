# 网络角色生成

保留原有同步 `SpawnBNpc`、`SpawnPlayer`（`ClientObjectManager` 本地角色）作为简便备用方式。新增 `SimEntityManager.SpawnNetworkBNpc` 和 `SpawnNetworkPlayer`，分别通过客户端的 SpawnNpc、SpawnPlayer 收包处理函数进入 `CharacterManager` 战斗实体列表。两者共用槽位预留、就绪检查和回收，现有预设使用网络生成，本地生成保留为备用。

## 界面手动生成

从“菜单 → 调试功能 → 添加实体”进入，普通点击时 NPC 默认使用网络生成，场景物件继续使用 EObj 包。完成初始化并开启防火墙后，右键点击该菜单项可以选择网络 NPC、网络玩家；“本地生成（备用）”保留旧入口。

- “生成网络 NPC…”：输入基础编号、名字编号和等级；后两项可省略，分别默认为 0 和 1。此入口需要完成 客户端游戏数据加载。
- “生成网络玩家…”：选择职业、等级、名字和“优先使用职业预设装备”开关。名字留空时使用“模拟 + 职业名”；表单会提示当前职业使用预设还是复制自身装备。
- “清理调试与手动生成实体”：停止当前调试测试，并回收手动生成的实体。

右键菜单生成的角色出现在自身前方 3 单位处；左键通用入口保留在自身位置生成的习惯。就绪后会写入日志并持续保留，不会像“调试”流程那样自动移动和回收。再次生成可以继续添加角色；手动清理、关闭防火墙、切换区域、重新初始化或卸载插件时统一清理。此入口便于反复观察外观与选中状态。

停止后的手动实体管理器若仍有回收失败项，会保留原记录并拒绝新的生成请求。再次选择“清理调试与手动生成实体”即可重试；成功项不会重复回收，全部完成后才能继续生成。实体测试留下的失败记录也由此菜单清理，或再次点击“调试”重试。

## 在时间轴中生成

```csharp
EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
{
    BNpcBaseId = 4909,
    BNpcNameId = 3765,
    Level = 90,
    Pos = new Vector3(0, 12, 0), // ACT 坐标：X/Y 为平面，Z 为高度。
    Heading = 0
}, actor =>
{
    // 此处角色已经就绪，可以发送控制和移动包。
    actor.Entity.ActorMove(new Vector3(0, 0, 0), 0, speedParam: 40);
});
```

生成是异步就绪的，但接口不创建异步任务：每个实体管理器只有一个 16 毫秒检查节点，挂在原来的共享调度器上。默认超时 5 秒，可通过 `timeoutSeconds` 调整。就绪回调同步运行在调度线程中，不能传 `async void`；可在其中继续排时间轴节点。ActorMove 本身也由客户端后续消费，调用返回不表示已经移动到终点；不要同时对同一个角色运行直接写坐标的 `MoveTo`。

需要到达回调、途中停止或替换目标时，使用 `actor.MoveNetworkTo(target, speedParam: 30, onArrived: ...)`。它约每 100 ms 发送沿途位置并检查实际到达，接入 `IsMoving`、`StopMoving()` 与解除关联清理。直接调用这里的 `actor.Entity.ActorMove(...)` 则不登记控制器，仍由调用者管理。`EntityManager.Delete(actor)` 成功回收实体后，其原生移动也会结束。

预设 Boss、技能假人和模拟队员均使用上述网络生成。技能假人保持 `Targetable = false`；小队成员就绪前不会开始分散或机制。`SpawnBNpc`、`SpawnPlayer` 和本地插值 `MoveTo` 继续作为备用 API，专用本地三实体测试仍验证这条路径。

## 网络模拟玩家

```csharp
EntityManager.SpawnNetworkPlayer(new NetworkPlayerSpawnSpec
{
    Job = Job.WHM,
    Name = "模拟白魔",
    Pos = new Vector3(0, 12, 0),
    Heading = 0
}, player => player.Entity.ActorMove(new Vector3(0, 0, 0), 0, 40));

// 只要求职业时也可简写；位置默认为原点，在回调中再设置所需行为。
EntityManager.SpawnNetworkPlayer(Job.PLD, player => { });
```

只必须指定有效职业。`Level`、`Name` 未指定时继承外观来源；`AppearanceSource` 默认是自身，也可指定其他有效玩家。HP/MP 默认当前与最大值均为 10000，可覆盖；坐标仍使用 ACT 约定，`Targetable` 默认为 true。复制的是外貌、装备、两种染色、眼镜与服务器编号的快照，不使用真实账号编号或 ContentId。玩家包不提供 NPC 专用的 BaseId、NameId、Battalion、LinkRange 或导演绑定参数。

当前复制不包含隐藏头盔、收起时隐藏武器和面罩开关。这三项在 [FFCS DrawDataContainer](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs#L21-L64) 中是独立状态，另有原生切换函数；本项目的网络玩家路径尚未应用它们，也未确认其在国服 7.57 生成包中的位映射。

`UseJobEquipment` 默认 true，武器应用装等 795 的“幻境××·秘影”系列，头、身、手、腿、脚仍使用 99 级、装等 690 的职业蓝装，覆盖包括学者、暗黑骑士、蝰蛇剑士和绘灵法师在内的 21 个普通战斗职业。主副武器和五件护甲一起替换并清除染色，保留来源玩家的饰品、脸型、发型、种族和眼镜。这里只应用装备外观，不修改真实装备、背包或属性，也不按模拟角色等级限制显示。青魔没有这套幻境武器；青魔、基础职业及生产采集职业继续复制来源装备。设置 `UseJobEquipment = false` 可对任何职业使用复制外观。

职业装备外观固定数据见 [JobEquipment.cs](../Simulant/Core/Entity/JobEquipment.cs)。调整时分别核对职业、装备槽、主副手模型与变体，不把物品 ID 当作模型 ID。

原生装备每项占八字节，而包内装备模型每项只有四字节，第二染色单独存放；读取时一次抓取 DrawData 快照再拆分，避免逐槽跨进程读。玩家包使用 Pc / SubKind 4 / DisplayFlags 0x40020，生成器在就绪后补发 ZoneIn 入场控制再初始化可选中。整个初始化仍在回收记录保护下，失败时可以取消或删除。

## 槽位与回收

### 出生圈与场地就绪

出生圈碰撞来自地图中的 SharedGroup，不会因为本地没有 EObj 就自动消失。公共进图流程读取 `planmap.lgb`，按出生圈资源 `bgcommon/world/lvd/shared/for_vfx/sgvf_w_lvd_b0095.sgb` 的绑定寻找 EObj，而不是按坐标或 BaseId 猜测。

EObj 的 BaseId 描述对象种类；LayoutId、GimmickId 将其关联到具体场地实例。同一 BaseId 可以用于不同机关，不能将所有同号对象都按出生圈处理。仅读取资源文件也不会改变已加载的碰撞体。

地图就绪后生成关联 EObj，等 `IsSharedGroupLoaded()` 成功，再通过 `DirectorEObjMod`（0x6A）发送状态 7。对象保留至离开地图，停止或重开机制不重新关闭出生圈。地图就绪与 EObj 绑定就绪是两项独立检查；天气、BGM、MapEffect 同样应在对应地图及控制器就绪后提交。

默认出生位置取出生圈中心，特殊场地通过 `PhaseData.Spawn` 覆盖。机制开始后的预站位由预设自行安排。

### 其他实体

- SpawnIndex 是当前客户端战斗列表的槽号，不是全局唯一身份；不同客户端可以把同一实体放在不同槽，删除后也可复用。模拟角色使用 1–99，0 留给自身玩家；对应对象表偶数槽 `2 * index`，紧邻奇数槽留给附属对象。本地生成使用的 200–448 范围保持原样。
- 发包前检查两个表的空槽及附属槽，全表避让实体 ID，并提前登记预留。发包后必须核对 ID、类型、槽位和两个表指针，才能创建受管理的实体引用。不会因为未立即出现就创建本地备用角色占槽。
- 删除与生成成对使用 Despawn。即使角色还没出现，也要取消原生生成队列。只有确认相关槽位清空后才释放预留；失败记录保留供再次清理，陌生占用不会被按槽位直接删除。
- `Clear()` 保留删除失败的实体和取消失败的请求，只移除成功项。后续清理重试这些保留项；尚有保留项或正在清理时，管理器不能绑定新调度器。预设会话保留“停止模拟”供重试，清理完成前禁用“开始模拟”，并且不重复调用该轮预设的 `OnStop`。
- 停止会话取消未领取的就绪回调；已经领取的回调允许完成。从调度线程外停止时，先等待调度线程退出，再清理实体；若在回调内停止会话，调用后应立即返回，不再访问已清理的实体。切换到新世界后只丢弃旧记录，不再访问旧指针。
- 正常调试要求已初始化并启用防火墙；开发者模式放宽操作限制。关闭防火墙仍须恢复网络入口，实体或标点清理失败不能阻止网络恢复。

## 普通战斗 NPC 的初始参数

这是一组有公开工作实现依据的默认值，**不是已经证明对所有模型、地区版本和副本都适用的最小必填集合**。

| 参数 | 当前选择及来源 |
| --- | --- |
| 自身实体 ID / SpawnIndex | 生成器分配；自身 ID 作为处理函数参数传入，SpawnIndex 写入包。两者不能混用 |
| BaseId / NameId / Level | 调用方明确提供；等级默认 1，必须大于 0 |
| ModelChara / Battalion / Name | 从当前客户端的 BNpcBase、BNpcName 强类型行读取，也可显式覆盖 |
| ObjectKind / SubKind / Mode | BattleNpc / 5（战斗对象）/ Normal；`Common.ObjectType` 保持 0，不把 SubKind 写到那里 |
| HP / MP | 默认当前值与最大值均为 10000，可覆盖；HP 必须为正且不超过最大值。这些是模拟默认值，不是协议固定值 |
| CharacterDataFlags / DisplayFlags / LinkRange | 普通可见战斗实体采用 3 / 0x4000B / 0x14，可覆盖 |
| Targetable | 默认 true；完成战斗列表登记后通过 ActorControl 0x36 初始化。DisplayFlags 的可见状态不能代替可选中状态 |
| TargetId / OwnerId / TetherTargetId | 无目标时使用 0xE0000000；64 位 TargetId 零扩展。CombatTaggerId 保持 0，可覆盖 |
| EventId | 默认 0，不自动关联副本导演；需要事件联动时显式提供 EventHandler 或 Director 的编号。它不是网络实体身份或槽号 |
| LayoutId / GimmickId | 默认 0，按实际副本需求设置；生成一个可见战斗对象不等于已实现副本事件关联 |
| ModelScaleId | 默认为 0 的字节索引，不是 BNpcBase 的浮点 Scale |
| 外观 | 可选主副手模型、10 个装备槽、26 字节 Customize；名字最多 31 个完整 UTF-8 字节，保留结尾零字节 |

特殊模型可能需要额外的外观、形态、关联实体或场地参数；普通 NPC 默认值不会自动推断这些内容。

## 核对来源与验证范围

下列源码用于解释字段与调用关系；不同地区和版本的偏移仍需单独核对。

- [FFXIVClientStructs：SpawnPackets](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Network/SpawnPackets.cs) 用于核对当前本地布局：SpawnNpc 大小 0x290、Common 位于 0x10、Common 大小 0x278。
- [FFXIVClientStructs：CharacterManager](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Character/CharacterManager.cs) 与 [EventHandler](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Event/EventHandler.cs) 用于核对战斗列表及 EventId；该提交的国际服数据版本不等于已验证当前运行客户端。
- [AnoMech：BattleCharas](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/BattleCharas.cs)、[BattleCharaProxy](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/BattleCharaProxy.cs)、[PacketDispatcherPointers](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/Pointers/PacketDispatcherPointers.cs) 提供默认包参数、Spawn/Despawn 路径及删除签名；[CharacterManagerHelper](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/Interop/CharacterManagerHelper.cs) 记录了延迟生成覆盖同槽角色的问题。
- [Sapphire 的 NpcSpawnPacket](https://github.com/SapphireServer/Sapphire/blob/aab9cb2347eb0ec42e85ee3e117a0c177b9b3070/src/world/Network/PacketWrappers/NpcSpawnPacket.h) 和 [SpawnIndexAllocator](https://github.com/SapphireServer/Sapphire/blob/aab9cb2347eb0ec42e85ee3e117a0c177b9b3070/src/common/Util/SpawnIndexAllocator.h) 仅参考字段语义与槽位分配；此版本面向 3.3，不复制旧包偏移。
- [Machina 的消息类型](https://github.com/ravahn/machina/blob/9b1c21d64618a8b1bef58a162d9cc9d58c4bcb98/Machina.FFXIV/Headers/Opcodes/Server_MessageType.cs) 包含 NpcSpawn、ActorMove 名称，但没有可用于补全此原型的对应 payload 定义。
- 玩家外观布局参考 [FFCS DrawDataContainer](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs)，入场控制参考 [AnoMech SimPartyNpc](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/SimObjects/SimPartyNpc.cs)。职业装备表字段定义见 [EXDSchema CharaMakeClassEquip](https://github.com/xivdev/EXDSchema/blob/a1e24a0c72058353f30f23beb996792cdb926868/CharaMakeClassEquip.yml) 和 [Item](https://github.com/xivdev/EXDSchema/blob/a1e24a0c72058353f30f23beb996792cdb926868/Item.yml)，本项目固定使用上述本地导出数据，不在运行时查询网络；字段定义用于解释数据含义，不代替本地版本的字段顺序。

[AnoMech 的 SimEnemy.OnPacketSpawned](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/SimObjects/SimEnemy.cs#L193-L207) 明确说明敌人初生不可选中，需要在就绪后单独设置。生成器采用其 [ActorControl.SetTargetable](https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Game/ActorControl.cs) 的 0x36 类别，先登记回收责任再初始化；初始化失败仍可回收。实机测试固定验证默认可选中、关闭、开启及移动和读条后的状态，同时检查标志位和客户端 GetIsTargetable 结果。

EventId 与战斗实体并非完全无关：[FFCS EventHandler](https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Event/EventHandler.cs#L108-L114) 的 UpdateTargetableStatus 会标记其所属对象更新可选中状态。默认 0 是独立模拟实体不主动接入副本脚本的选择，不代表所有真实副本 NPC 都应为 0，不能据此推断 EventId 是所有可选中问题的原因。

纯测试覆盖包大小、关键偏移、默认值、覆盖值、参数验证、生成等待/取消的会话生命周期，以及批次失败后的回收重试和重启限制；它们不执行游戏原生代码，不能证明签名、原生队列取消或画面效果正确。[实机步骤](EntityTests.md) 覆盖这些剩余检查。

## 模拟命令日志入口

插件初始化成功前必须订阅 ACT 的 `OnLogLineRead`。这与 Triggernometry 的普通文本日志入口一致，读取 `LogLineEventArgs.logLine`，不是原始网络收包事件。绑定失败使初始化失败；重新初始化先退订再绑定，卸载时取消订阅，导入日志不触发命令。

`ACT/SimCommandListener.cs` 使用以下正则提取 `cmd`，空白命令忽略；通过内部事件交给相应命令处理者：

```regex
^.{15}\S+ 00:0038::(?i) *sim *(?<cmd>.+?) *$
```

插件内部通过 `host.Commands.Received += OnCommand` 订阅，结束使用时以 `-=` 解除。回调参数为字符串，例如默语 `sim ac 亲疏自行` 对应 `ac 亲疏自行`。事件在 ACT 日志回调线程同步触发；处理者应快速返回，把模拟操作交给已有调度器，不阻塞日志线程。单个处理者异常会记录错误并继续通知其他处理者，不向 ACT 传播。
