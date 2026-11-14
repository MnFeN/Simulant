# 从回放读取网络包

当前先完成离线读取和包清单导出。读取器不依赖 ACT、游戏进程或外部解析库，也不执行回放中的动作。尚未把包转换成可运行的模拟时间轴。

## 使用入口

核心代码在 `Simulant/Replay`：

- `ReplayReader`：校验并流式读取头、章节和原始记录，保留未知 opcode 与完整负载。
- `ReplayPacketCatalog`：按指定版本的 opcode 表与已知长度识别，保留候选和冲突。
- `ReplayInspector.Export`：导出清单，计算源文件 SHA-256，不访问客户端。

命令行入口在 `tools/ReplayInspector/Program.cs`，已经加入解决方案。需要 .NET Framework 4.8 和 Visual Studio MSBuild，不需要启动 ACT：

```powershell
$msbuildPath = & "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe
& $msbuildPath tools/ReplayInspector/ReplayInspector.csproj /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw '回放工具编译失败' }

& ./tools/ReplayInspector/bin/Release/Simulant.ReplayInspector.exe `
  'C:\Program Files\SquareEnix\FFXIV_CN\game\My Games\FINAL FANTASY XIV - A Realm Reborn\replay\FFXIV_0040001087FA8E18_001.dat' `
  --profile CN7.57
if ($LASTEXITCODE -ne 0) { throw '回放读取失败' }
```

默认在工具旁的 `reports` 下创建带时间的新目录，也可用 `--output <新目录>` 指定。输出目录不能已经存在、位于源回放目录中，或经过目录链接。不会覆盖原文件或已有结果。源回放始终以只读方式打开，读取期间不允许其他进程写入或替换它。

输出包括：

| 文件 | 内容 |
| --- | --- |
| `summary.md` | 文件摘要、版本依据、异常元数据说明与包类型分布 |
| `packets.csv` | 每条记录的原始序号、偏移、毫秒时间、对象 ID、opcode、负载长度、识别结果和前 32 字节 |
| `opcodes.csv` | 按 opcode 与长度分别汇总次数、首次／末次时间、候选、依据与源码链接 |
| `chapters.csv` | 章节类型、时间与偏移，检查是否指向记录边界 |

CSV 使用带 BOM 的 UTF-8，文本列保留引号；对象 ID 是该记录的路由对象，不能对所有包都解释成攻击来源。完整负载仍可通过 `ReplayReader.ReadPackets()` 获得，CSV 预览不代替原始负载。

整个包流扫描完成后才发布最终文件，`summary.md` 最后发布。失败时的 `.partial` 文件不是完整清单；不会跳过损坏字节后猜测下一个记录起点。

## 容器格式与版本

当前明确支持回放格式 **5**，不把未知格式按同一布局硬读：

| 区域 | 大小／字段 |
| --- | --- |
| 文件头 | `0x68`，魔数 `FFXIVREPLAY\0`，包含格式、内部构建号、录制时间、匹配副本 ID 等 |
| 章节区 | `0x304`，一个 32 位数量和最多 64 个 `0x0C` 章节 |
| 第一条记录 | 文件偏移 `0x36C` |
| 记录头 | 12 字节：`ushort opcode`、`ushort payloadLength`、`uint timestampMs`、`uint objectId` |
| 记录负载 | 紧随记录头的 `payloadLength` 字节，没有额外记录对齐，不含实时网络传输头 |

整数按小端读取。依据 [Hypostasis 的 FFXIVReplay](https://github.com/UnknownX7/Hypostasis/blob/c230760c7bbc85a6e37b8477012dcd92ab700dc7/Game/Structures/FFXIVReplay.cs) 和 [ARealmRecorded 的读取代码](https://github.com/UnknownX7/ARealmRecorded/blob/679f25321d9459abc00e73fa2eade35f8b172043/Game.cs)，该容器直接保存包记录，不需要套用实时网络包的解压或解密流程。头字段另与 [FFCS ContentsReplayManager](https://github.com/aers/FFXIVClientStructs/blob/0adb2e545ac53cacc9341c1d030eb1a9495af49c/FFXIVClientStructs/FFXIV/Client/Game/ContentsReplayManager.cs) 核对。

部分文件没有写回完成时的长度、时长和章节。读取器保存这些原值，并按实际 EOF 扫描；声明长度失配、章节指向无效记录边界等问题在报告中说明。记录时间不要求单调，同刻与时间回退均保持原文件顺序，不提前排序或合并。

## opcode 识别

目前内置一份固定的 `CN7.57` 目录，保留源码版本，导出时不临时抓取随时可能变化的在线表：

- [FFXIVOpcodes 国服表](https://github.com/karashiiro/FFXIVOpcodes/blob/d85eaebec24b08948550e09c005fe5860d17c3f2/FFXIVOpcodes/Ipcs_cn.cs)。
- [7.57 补充表](https://github.com/zhyupe/ffxiv-opcode-worker/blob/06df8d0ddf52e230c73826006cfbc50e6c2891a6/json/7.57.json)，含 `UpdateParty`；[构建报告](https://github.com/zhyupe/ffxiv-opcode-worker/blob/06df8d0ddf52e230c73826006cfbc50e6c2891a6/reports/7.57.json) 给出 `2026.10.06.0000.0000` 及 CN/KR 共用信息。
- 长度参考 [BossMod 包结构](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Network/ServerIPC.cs)、[已有回放长度表](https://github.com/gamous/FFXIVNetworkOpcodes/blob/cd1a8b5704b92c60971fbd0189616691e8753f89/output/Global_2023.03.24/opcodes_record.json)、项目已迁入的 FFCS 生成包，以及 [Countdown 的新版长度](https://github.com/OverlayPlugin/OverlayPlugin/blob/5b16a642351e588fc729f3ea9cf345cb0862f9d4/OverlayPlugin.Core/NetworkProcessors/LineCountdown.cs)。旧长度资料可能不适用于新版，因此仍检查实测长度并保留冲突。

不传 `--profile` 时，所有网络包只显示候选。显式指定版本后，已知 opcode 与长度都一致才填入类型名称；长度未知仍是候选，已知长度不同则标冲突。同长度可能对应多个包，即使只剩一个候选也不能视为确认。

回放内部构建号与公开补丁号的自动对应关系目前没有可靠来源，不能把安装客户端版本当作所有历史录像的版本。后续增加版本时，应添加对应来源与长度证据，并在有独立证据时建立内部构建号映射。回放扩展记录 `0xF001/0xF002/0xF003` 与普通网络 opcode 分开识别。

## 本次只读样本

`FFXIV_0040001087FA8E18_001.dat`：

- SHA-256：`04EC70A2E4E27949F6EE0B3CC6FB96A69A63380D677CAFED20E2DFEB7D3CA139`。
- 大小 `11,006,988` 字节，格式 5，内部构建号 `0x00D72B67`。
- 录制时间为 `2026-11-01 23:01:16 +08:00`；匹配副本 ID `694`，对应亚历山大绝境战。
- 完整读取 `109,954` 条记录、`44` 种 opcode，包流最大时间 `24:23.849`，没有截断或时间回退，末条边界与 EOF 完全一致。
- 文件头的时长、声明数据长度和章节均为零；只报告此差异，不修补原录像。

按指定 `CN7.57` 目录，主要记录如下。这是录像中的出现次数，不等于一次完整战斗需要执行的次数，也不证明每条都应重放：

| 类别 | 本样本数量 | 模拟用途 |
| --- | ---: | --- |
| ActorMove / ActorSetPos | 63,526 / 262 | 连续移动、位置与朝向校正 |
| ActorCast | 948 | 读条和技能起始 |
| ActionEffect1 / 8 / 16 | 4,737 / 883 / 18 | 技能结算、目标与表现，需进一步拆分效果 |
| ActorControl / Target | 11,088 / 972 | 标记、连线、可选中、目标、状态等子事件 |
| ActorControlSelf 候选 | 795 | 实长 40 与现有 32 字节资料冲突，尚不能按旧布局全部解释 |
| NpcSpawn / PlayerSpawn / ObjectSpawn | 583 / 8 / 217 | 实体初始状态与场景对象 |
| DespawnCharacter / ObjectDespawn | 517 / 143 | 对应实体生命周期结束 |
| MapEffect | 92 | 场地效果和场景状态 |
| StatusEffectListDouble / StatusEffectList | 7,168 / 801 | 血蓝和状态快照，不能全部当作新增状态事件 |
| UpdateHate / UpdateHater | 4,150 / 1,097 | 仇恨与敌对列表显示 |

尚未命名的 opcode 包括 `0x0075/8`、`0x03A0/8`、`0x006A/136`、`0x00E1/24`、`0x0095/32`、`0x02C1/1408`，斜线后是负载字节数。`0x006A/136` 与 `ContentDirectorSync` 同长，`0x00E1/24` 与 `DirectorVars` 同长，但目录没有足够证据将它们直接命名。

## 到可运行时间轴还需处理

1. 确认长度冲突与关键未知包，解析 ActorControl 的子类别。`DirectorUpdate (0x6D)` 属于控制子事件，另外还存在独立 Director 包；只按顶层名字过滤会漏事件。
2. 识别每次开战、团灭、重置和阶段切换，保留起点所需的初始化快照。文件头没有章节时不能把整段录像自动当成一次战斗。
3. 建立录像实体与模拟实体的映射，处理 ID 复用和生成先后；原始技能及头标 ID 还可能带偏移，需要结合初始化或扩展记录确认，不能直接当成 客户端技能表 ID。
4. 区分机制、表现与状态同步：生成回收、位移、读条、关键 ActorControl、场地效果通常优先；血量、状态和效果结果用于一致性；小队仇恨、职业量谱、倒数、场地标记按所需界面能力选择。普通聊天、社交、物品栏等无需因此加入战斗执行层。
5. 把已确认事件转换为项目现有时间轴节点，而非按录像原始 ID 盲目回灌所有包；仍需实机对照客户端本地动画和场景脚本的结果。

回归测试位于 `Simulant.Tests/ReplayTests.cs`，使用合成数据覆盖完整容器、错误边界、短读、未收尾元数据、原始顺序、流生命周期与识别歧义，不提交真实回放数据。
