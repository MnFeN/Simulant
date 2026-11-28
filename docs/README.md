# Simulant 自定义副本

仿生石是运行在 64 位 ACT 中的 FFXIV 本地副本模拟器。玩家从旅馆进入本地模式，选择并加载地图，再启动该地图下的练习。插件能在客户端创建模拟队友和敌人，按时间轴播放游戏原生的读条、动作和特效。具体预设还可以判定玩家的站位和机制处理结果。[项目使用说明](../README.md)

## 导入与使用

1. 下载示例 JSON，或准备作者分享的文件。等插件载入客户端游戏数据后，点击“阶段 / 模拟”旁的**导入本地 JSON**。当前练习结束并清理后即可导入下一份。
2. 校验通过后，插件自动选中对应的地图和条目。在旅馆进入本地模式，点击**加载区域**。带时间轴的文件再点**开始模拟**；只想看场地时，到加载完成即可。
3. 导入的副本保存在 ACT 安装目录的 `Config/Simulant.Encounters`，重启后可以再次选择。同一 ID 的新版文件会提示替换。可以在 ACT 关闭时删除该目录下对应的 JSON 来移除条目。

| 可导入的文件 | 观察内容 |
| --- | --- |
| [O8S 异三角开场](examples/o8s-trine-opening.json) | 区域 755，本体就绪 3 秒后播放异三角读条。 |
| [绝神兵 P1 场地预览](examples/uwu-garuda-arena.json) | 区域 777，直接查看暴风天气下的迦楼罗场地。 |
| [绝神兵 P1 迦楼罗练习](examples/uwu-garuda-opening.json) | 从开场练到狂暴，可选职能和七名队友。刺羽锁首个接怪者，姐妹双线可截取，消层和觉醒随实际处理变化。[分工与验收](uwu-garuda.md) |
| [光暗未来绝境战 · 复乐园南塔](examples/fru-south-tower.json) | 等复乐园实际结算后开塔，可选治疗或近战示范。首人进塔启动倒数，双人成功、人数错误和无人进塔分别收尾。 |

编写新副本时，作者手册列出了 v1–v3 的全部 JSON 接口、每个操作的必填字段和最低版本。可以从 O8S 的完整示例开始，也可以直接查要用的操作。[JSON 作者手册](encounter-format.md) · [时间轴操作速查](encounter-format.md#operations)

## 和内置副本的关系

| 玩家看到的内容 | 当前来源 | 对文件导入的影响 |
| --- | --- | --- |
| 地图和副本名 | 地图选择器读取本机游戏的 `TerritoryType` 表。“仅显示含预设区域”合并内置区域标记与已导入区域。 | 新文件可以引用游戏已有地图。两份引用相同区域的文件出现在同一地图下。[地图选择器](../Simulant/UI/TerritoryForm.cs) |
| 阶段环境 | `PhaseData.GetPhases` 从内置区域类的静态字段读取阶段，区域加载时应用天气、BGM 和场地效果。 | 仅描述场地也可以加载。省略环境时使用地图默认值。[阶段发现](../Simulant/Core/Environment/PhaseData.cs) · [环境应用](../Simulant/Core/Zone/ZoneService.Phase.cs) |
| 可开始的模拟 | 界面按区域列出内置和本地预设，由 `SimSession` 创建会话并调度练习。 | 通用预设生成声明的敌人，全部就绪后开始文件时间轴。[本地预设](../Simulant/Content/Local/LocalPreset.cs) · [模拟会话](../Simulant/Simulation/Runtime/SimSession.cs) |

O8S 把“本体 P1”的天气和音乐写在区域类里，把“异三角”的敌人、读条和后续三角机制写在另一个预设里。本地示例复现其中一个读条，两个条目可以同时使用。绝神兵的示例完全通过 JSON 进入同一个选择器。[O8S 环境](../Simulant/Content/S4b/O8s.cs) · [异三角预设](../Simulant/Content/S4b/Presets/异三角.cs)

仓库里的 ReplayInspector 可以把回放片段导出为 `timeline.json`，再生成供开发者复核的 C# 草稿。它可以帮助作者查证时间和资源 ID。分享给玩家的本地描述文件使用作者手册中的 JSON 格式。[回放转换工具](../tools/ReplayInspector/ReplayConversion.cs)
