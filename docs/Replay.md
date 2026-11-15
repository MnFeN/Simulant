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
  'D:\Replay\example.dat' `
  --profile CN7.58 --output .local/replay/example
if ($LASTEXITCODE -ne 0) { throw '回放读取失败' }
```

示例路径需替换为实际录像路径，并先创建 `.local/replay` 父目录。示例将调查结果保存在被忽略的 `.local` 中。省略 `--output` 时，默认在工具旁的 `reports` 下创建带时间的新目录。输出目录不能已经存在、位于源回放目录中，或经过目录链接。不会覆盖原文件或已有结果。源回放始终以只读方式打开，读取期间不允许其他进程写入或替换它。

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

目前内置 `CN7.57` 与 `CN7.58` 目录，导出时不临时抓取在线表。7.57 的来源如下：

- [FFXIVOpcodes 国服表](https://github.com/karashiiro/FFXIVOpcodes/blob/d85eaebec24b08948550e09c005fe5860d17c3f2/FFXIVOpcodes/Ipcs_cn.cs)。
- [7.57 补充表](https://github.com/zhyupe/ffxiv-opcode-worker/blob/06df8d0ddf52e230c73826006cfbc50e6c2891a6/json/7.57.json)，含 `UpdateParty`；[构建报告](https://github.com/zhyupe/ffxiv-opcode-worker/blob/06df8d0ddf52e230c73826006cfbc50e6c2891a6/reports/7.57.json) 给出 `2026.10.06.0000.0000` 及 CN/KR 共用信息。
- 长度参考 [BossMod 包结构](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Network/ServerIPC.cs)、[已有回放长度表](https://github.com/gamous/FFXIVNetworkOpcodes/blob/cd1a8b5704b92c60971fbd0189616691e8753f89/output/Global_2023.03.24/opcodes_record.json)、项目已迁入的 FFCS 生成包，以及 [Countdown 的新版长度](https://github.com/OverlayPlugin/OverlayPlugin/blob/5b16a642351e588fc729f3ea9cf345cb0862f9d4/OverlayPlugin.Core/NetworkProcessors/LineCountdown.cs)。旧长度资料可能不适用于新版，因此仍检查实测长度并保留冲突。

7.58 来源为 [opcode-worker 7.58.json](https://github.com/zhyupe/ffxiv-opcode-worker/blob/master/json/7.58.json)。`ActorControlSelf` 接受 32 和 40 字节记录并保留完整正文；分类成功不代表全部参数已经解码。

不传 `--profile` 时，所有网络包只显示候选。显式指定版本后，已知 opcode 与长度都一致才填入类型名称；长度未知仍是候选，已知长度不同则标冲突。同长度可能对应多个包，即使只剩一个候选也不能视为确认。

回放内部构建号与公开补丁号的自动对应关系目前没有可靠来源，不能把安装客户端版本当作所有历史录像的版本。后续增加版本时，应添加对应来源与长度证据，并在有独立证据时建立内部构建号映射。回放扩展记录 `0xF001/0xF002/0xF003` 与普通网络 opcode 分开识别。

## 到可运行时间轴还需处理

1. 确认长度冲突与关键未知包，解析 ActorControl 的子类别。`DirectorUpdate (0x6D)` 属于控制子事件，另外还存在独立 Director 包；只按顶层名字过滤会漏事件。
2. 识别每次开战、团灭、重置和阶段切换，保留起点所需的初始化快照。文件头没有章节时不能把整段录像自动当成一次战斗。
3. 建立录像实体与模拟实体的映射，处理 ID 复用和生成先后；原始技能及头标 ID 还可能带偏移，需要结合初始化或扩展记录确认，不能直接当成 客户端技能表 ID。
4. 区分机制、表现与状态同步：生成回收、位移、读条、关键 ActorControl、场地效果通常优先；血量、状态和效果结果用于一致性；小队仇恨、职业量谱、倒数、场地标记按所需界面能力选择。普通聊天、社交、物品栏等无需因此加入战斗执行层。
5. 把已确认事件转换为项目现有时间轴节点，而非按录像原始 ID 盲目回灌所有包；仍需实机对照客户端本地动画和场景脚本的结果。

回归测试位于 `Simulant.Tests/ReplayTests.cs`，使用合成数据覆盖完整容器、错误边界、短读、未收尾元数据、原始顺序、流生命周期与识别歧义，不提交真实回放数据。

## 读条、预兆与结算

回放导入应分别保存 ActorCast 的 `CastTime`、`OmenDelay` 与 ActionEffect 的时间，不把读条结束直接当作结算。FFCS 的 [ActorCastPacket](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Network/ActorCastPacket.cs) 将偏移 0x03 定义为 `byte OmenDelay`（单位 0.1 秒），0x08 为独立的 `float CastTime`。

模拟层查表读条默认减 0.3 秒，结束后再间隔 0.3 秒结算。已经从 ActorCast 取得的实际时长不能重复扣除；有回放证据时分别采用原始读条和结算时刻。完整的机制导入与自动生成时间轴尚未实现。
