# ActionNpcIndex

从 ACT 原始网络日志提取技能与施法 NPC 的对应关系，用于 Simulant 的「预览结束时间轴特效」。工具根据客户端的 `Action` 表筛选结束时间轴含 `[SKL_ID]` 的技能，将 `ActionId → BNpcBaseId` 和日志中观察到的 `BNpcBaseId → BNpcNameId` 分别写入 CSV。所有编号均为十进制。

## 更新索引

需要 Windows、Visual Studio MSBuild 和 .NET Framework 4.8 开发包。将示例路径替换为实际路径，在仓库根目录用 PowerShell 运行。脚本先还原依赖并构建工具，再扫描日志。

```powershell
.\tools\ActionNpcIndex\Update-ActionNpcIndex.ps1 `
    -GamePath 'C:\Games\FFXIV' `
    -Logs @('D:\ACT\FFXIVLogs', 'D:\ACT\Archive\FFXIVLogs.zip')
```

| 参数 | 用法 |
| --- | --- |
| `-GamePath` | 插件目标版本的客户端根目录、`game` 目录或 `sqpack` 目录。 |
| `-Logs` | 可同时传入多个 `.log`、`.zip` 和目录。目录下的日志和 ZIP 会递归扫描，ZIP 中的 `.log` 会直接读取。 |
| `-OutputPath` | 默认更新 [ActionCasters.csv](../../Simulant/Resources/NpcIndexes/ActionCasters.csv) 和同目录的 [BNpcNames.csv](../../Simulant/Resources/NpcIndexes/BNpcNames.csv)。可指定其他 `.csv` 路径检查输出，名称表保存在同目录的 `<输出文件名>.Names.csv`，父目录需已存在。 |
| `-PackageSource` | 可选的 NuGet 包源。依赖已缓存时可传入本地包目录，例如 `"$env:USERPROFILE\.nuget\packages"`。 |

扫描器读取以竖线分隔的原始网络日志，用 `03` 行关联 NPC 实体，并从 `20`、`21`、`22` 行提取施法记录。每份日志单独建立实体关联，切区和实体移除会更新关联。筛选日志时请保留 NPC 的 `03` 行，供工具识别技能来源。

## 合并与生效

已有索引会参与增量合并，自动去重并按第一列、第二列依次升序排列。技能记录以传入客户端的 `[SKL_ID]` 列表为准。控制台的「匹配」是本次输入命中的技能数，「索引共」是合并后的总数。

两张索引都排除占位实体 `BNpcBaseId 9020`。名称编号必须非零，并且存在于传入客户端的 `BNpcName` 表。每个 `BNpcBaseId` 只保留排序后第一条名称记录，即编号最小的 `BNpcNameId`。这些规则同时用于扫描日志和清理已有索引。固定骨架的结束时间轴根据客户端资源筛选兼容模型。

更新默认索引后，用 Simulant 的「本地发布」配置重新发布，再在 ACT 中加载生成的 `publish/Simulant.dll`。
