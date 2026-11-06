# 仿生石 · Simulant

基于 ACT、使用游戏内原生画面的 FFXIV 本地副本模拟插件。  
An ACT plugin for local FFXIV raid simulation using native in-game rendering.

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

开发中，欢迎合作。  
Work in progress.

<img width="2222" height="1170" alt="image" src="https://github.com/user-attachments/assets/87dc9104-2b5c-417f-8415-e783bc945b87" />


## 使用说明 · Instruction

项目仍处于早期开发阶段，出现报错、闪退等现象均不属于意外情况。

尚未加装完整的校验流程，请严格遵循下述步骤使用。

> [!TIP]
> **下载时请在右侧 Release 下载最新的发布版中的 dll，不要在上方下载源码。**

- 目前依赖以下 ACT 插件：

  - FFXIV 解析插件
  - [OverlayPlugin](https://github.com/OverlayPlugin/OverlayPlugin)
  - [Triggernometry](https://github.com/MnFeN/Triggernometry) (v2.0 以上)
  - [PostNamazu](https://github.com/Natsukage/PostNamazu)

  前两者为任意整合版本 ACT 自带的插件，后两者的安装说明见[此链接](https://www.bilibili.com/opus/998425290402168834)。

  如果你安装过我的 Triggernometry 整合，可以跳过此步骤。

- 将此插件 dll 加载至 ACT 的插件列表即可完成加载，可置于列表中比较靠后的位置。

<img width="800" alt="image" src="https://github.com/user-attachments/assets/b2fde36d-dde1-47d1-9f7c-857ea61c9b9f" />

- 插件加载后，在游戏开启状态下，点击“初始化插件”。

> [!WARNING]
> 此插件与卫月平台的一些插件存在兼容性问题。
> 
> 如果遇到游戏崩溃或初始化出错（如扫描不到某 Ptr），请尝试纯净启动，或仅开启卫月框架。

- 初始化完成后，进入任意旅馆地图，勾选“启用防火墙”。此功能与卫月插件 [Hyperborea](https://github.com/kawaii/Hyperborea) 类似。

  这会拦截除了心跳包以外的所有收发包，进入本地模式。

  目前此模式下请勿与场景实体交互（如旅馆的门），否则会卡住。

- 在左侧选择地图，或输入地图 ID；如果此地图有阶段/模拟的话可以在下拉列表中选择。

  如果要筛选目前已经实现的 demo，可在地图选择窗口界面中勾选 “仅显示含预设区域” 查看。

<img width="800" alt="image" src="https://github.com/user-attachments/assets/cc9d2ebe-1685-4b51-8d08-a4c237a5d865" />

- 点击“加载区域”，等待地图加载结束后在右侧预设界面点击“开始模拟”。可在模拟中途点击“停止模拟”；停止会取消后续时间轴节点，等待当前动作返回后清理模拟实体。

- 结束后，取消勾选“启用防火墙”，即可返回旅馆中的原位置。

  若关闭失败，请根据日志处理后再次取消勾选以重试；仍有勾选表示清理尚未完成，不一定表示网络仍被完整拦截。重新初始化前需完成清理。

  加载区域中途失败时也会保留进入前的地图与位置；再次加载不会覆盖返回点，关闭防火墙前会尝试恢复。

- 如果此过程中有任何一步导致游戏崩溃，请记录问题并重启 ACT。

- 可选：如果文字或界面排版有问题，可以安装字体：
  
  [更纱黑体](https://github.com/be5invis/Sarasa-Gothic/releases)

  在 Single Family TTF Package 中下载 Mono 的压缩包并安装字体。

## 开发文档 · Development

- [机制时间轴开发](docs/Timeline.md)：节点定义、子轴组合、取消与实体生命周期。
- [实体与模拟角色](docs/Entities.md)：Native / Entity / SimActor 边界、创建与借用、移动控制。
- [网络角色生成](docs/NetworkEntities.md)：实验生成入口、原生移动边界、回收与当前验证范围。
- [实体实机测试](docs/EntityTests.md)：测试入口、观察步骤和失败后的清理重试。
