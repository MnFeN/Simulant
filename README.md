# 仿生石 · Simulant

- 基于 ACT、使用游戏内原生画面的 FFXIV 本地副本模拟插件。    
An ACT plugin for local FFXIV raid simulation using native in-game rendering.

- 在游戏内本地创建模拟队友和敌方，利用游戏原生的技能释放、动作和特效播放，由插件内的时间轴脚本驱动副本流程演出。  
Spawns simulated party members and enemies locally in the game client.   
Its timeline scripts drive the encounter using the game's built-in abilities, animations, and visual effects.

- 运行原理类似 Hyperborea、Brio 等卫月插件，副本演出均在客户端本地运行，无需搭建或连接私服。  
It works in a similar way to Dalamud plugins such as Hyperborea and Brio.   
Encounters run entirely on the client, with no need to set up or connect to a private server.

## 功能 · Features

- 本地副本模拟  
  Local duty simulation

- 本地生成假队友与敌人  
  Spawn local party members and enemies

- 原生动画与特效控制  
  Native animation and visual effect control

- 机制时间轴脚本  
  Timeline scripting for mechanics

- 玩家行为判定  
  Player behavior validation

## 状态 · Status

开发于 2026 年 2 月，仍处于开发阶段，欢迎合作。  

<img width="2222" height="1170" alt="image" src="https://github.com/user-attachments/assets/87dc9104-2b5c-417f-8415-e783bc945b87" />


## 使用说明 · Instruction

项目仍处于开发阶段，可能出现报错、闪退等现象，请严格遵循下述步骤使用。

> [!TIP]
> **下载时请在右侧 Release 下载最新的发布版中的 dll，不要在上方下载源码。**

- 需要 64 位 ACT 和 FFXIV 解析插件（不再需要 Triggernometry、PostNamazu 等插件）。

- 将此插件 dll 加载至 ACT 的插件列表即可完成加载，可置于列表中比较靠后的位置。

  <img width="800" alt="image" src="https://github.com/user-attachments/assets/957070e3-1255-4859-8cc0-6a21c782e84b" />

- 插件启动后会准备已配置的客户端文件。当 FFXIV 解析插件绑定游戏进程时，仿生石会自动连接。连接失败时可点击“重试连接”。

> [!WARNING]
> 此插件与卫月平台的一些插件存在兼容性问题。
> 
> 如果遇到开启防火墙或进入副本时游戏崩溃、初始化出错等情况（如扫描不到某 Ptr），请尝试纯净启动，或仅开启卫月框架。

- 初始化完成后，进入任意旅馆地图，勾选“启用防火墙”。此功能与卫月插件 [Hyperborea](https://github.com/kawaii/Hyperborea) 类似。

  这会拦截除了心跳包以外的通用收发包，进入本地模式。

- 在左侧选择地图，或输入地图 ID；如果此地图有阶段/模拟的话可以在下拉列表中选择。

  要加载自定义副本，点击阶段旁的“导入本地 JSON”，选择描述文件。导入后会自动选中对应地图和阶段。场地预览使用“加载区域”，带时间轴的练习加载后可点“开始模拟”。[导入与示例](docs/README.md) · [JSON 作者手册](docs/encounter-format.md)

  如果要筛选目前已经定义的模拟，可在地图选择窗口界面中勾选 “仅显示含预设区域” 查看。

  <img width="800" alt="image" src="https://github.com/user-attachments/assets/042e777e-ca15-42cc-8b17-47abe33355c7" />

- 点击“加载区域”，等待地图加载结束后在右侧预设界面点击“开始模拟”。可在模拟中途点击“停止模拟”；停止会取消后续时间轴节点，等待当前动作返回后清理模拟实体。

- 结束后，取消勾选“启用防火墙”，即可返回旅馆中的原位置。

  若关闭失败，请根据日志处理后再次取消勾选以重试；仍有勾选表示清理尚未完成，不一定表示网络仍被完整拦截。重新初始化前需完成清理。

  加载区域中途失败时也会保留进入前的地图与位置；再次加载不会覆盖返回点，关闭防火墙前会尝试恢复。

- 如果此过程中有任何一步导致游戏崩溃，请记录问题并重启 ACT。

## 致谢 · Acknowledgements

此插件的开发借鉴了大量 Dalamud 仓库，建立在前人在 FFXIV 客户端、游戏数据和模拟工具方面研究的基础之上。

特别感谢以下 Dalamud 及 ACT 项目的作者与贡献者：

- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs)提供了客户端结构与原生函数定义。
- [Lumina](https://github.com/NotAdam/Lumina) 和 [EXDSchema](https://github.com/xivdev/EXDSchema) 支撑了游戏资源读取与数据表解析。
- 网络包结构与字段语义参考了 [Sapphire](https://github.com/SapphireServer/Sapphire)、[Machina](https://github.com/ravahn/machina) 和 [BossMod](https://github.com/awgil/ffxiv_bossmod)，部分技能范围判定也参考了 BossMod。
- [Kodakku Assist](https://github.com/Karlin-Z/KodakkuAssistScriptLibrary) 、[NyaDraw](https://github.com/NiGuangOwO/DalamudPlugins/tree/main/plugins/NyaDraw)、[Daily Routines](https://github.com/Dalamud-DailyRoutines/DailyRoutines) 为游戏原生 VFX 绘制与其他游戏资源的调用提供了重要参考。 
- 卫月端模拟器 [AnoMech](https://github.com/anomek/AnoMech) 的实现帮助完善了小队更新等底层逻辑。 
- 在客户端本地进入副本的实现参考了 [Hyperborea](https://github.com/kawaii/Hyperborea)。
- 项目早期的本地实体生成参考了 [Brio](https://github.com/Valentine-Tools/Brio)。
- [Advanced Combat Tracker](https://advancedcombattracker.com/) 及国服维护者 [萌新科技研究所](https://act.diemoe.net/) 提供了插件宿主的支持。
- [PostNamazu](https://github.com/Natsukage/PostNamazu) 为通过 ACT 与游戏进程交互提供了重要的早期参考。
- 回放读取参考了 [Hypostasis](https://github.com/UnknownX7/Hypostasis) 和 [ARealmRecorded 的读取代码](https://github.com/UnknownX7/ARealmRecorded)。

## 开源许可

Simulant 的原创代码使用 GPLv3（`GPL-3.0-only`）开源协议。使用者可自由使用、修改此项目。

再次分发时，需同样遵从 GPLv3 开源协议，提供对应版本的完整源码；如有修改，需注明修改。

第三方内容说明见 [COPYING.md](COPYING.md)，完整条款见 [LICENSE](LICENSE)。

