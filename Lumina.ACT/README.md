# Lumina.ACT

Lumina.ACT 是面向 ACT 插件开发者的 FFXIV 本地资源读取库。它基于 Lumina 7.7.2，适配 .NET Framework 4.8 和 C# 7.3，可以集成到自己的 ACT 插件中。

通过它可以按 ID 查询技能、状态、区域等游戏数据，也可以按资源路径读取纹理、模型、场景等文件。库直接读取客户端的 `sqpack` 目录，游戏关闭时也可以使用。项目随附 Lumina.Excel 的 Stable 强类型表定义，可直接访问 `Action.Name`、`Status.Icon` 等字段。

## 接入项目

在插件的 `.csproj` 中添加对 `Lumina.Core.csproj` 的项目引用。下面的路径以插件目录和 `Lumina.ACT` 目录同级为例。

```xml
<ItemGroup>
  <ProjectReference Include="..\Lumina.ACT\Lumina.Core\Lumina.Core.csproj" />
</ItemGroup>
```

也可以从仓库根目录单独构建：

```powershell
dotnet build Lumina.ACT/Lumina.Core/Lumina.Core.csproj -c Release -p:CopyLocalLockFileAssemblies=true
```

生成的程序集是 `Lumina.ACT/Lumina.Core/bin/Release/net48/Lumina.ACT.Core.dll`，代码中的命名空间仍为 `Lumina`。

## 读取技能数据

`GameData` 接收客户端的 `sqpack` 目录路径。这个目录通常位于 `ffxiv_dx11.exe` 同级。`DefaultExcelLanguage` 决定默认读取的表语言，下面以简体中文客户端为例。

```csharp
using Lumina;
using Lumina.Data;
using ActionRow = Lumina.Excel.Sheets.Action;

// 以下代码放在方法内。
using (var gameData = new GameData(@"D:\FFXIV\game\sqpack", new LuminaOptions
{
    DefaultExcelLanguage = Language.ChineseSimplified
}))
{
    var actions = gameData.GetExcelSheet<ActionRow>();

    if (actions.TryGetRow(119, out var action))
    {
        string name = action.Name.ExtractText();
        ushort iconId = action.Icon;

        if (action.ActionCategory.TryGetValue(out var category))
        {
            string categoryName = category.Name.ExtractText();
        }
    }
}
```

在 ACT 插件中，可以在初始化时创建一个 `GameData` 实例供后续查询复用，并在 `DeInitPlugin()` 中调用 `Dispose()`。如果插件允许切换客户端目录或默认语言，请为新配置创建实例，并释放旧实例。

## 常用 API

| 用途 | API |
| --- | --- |
| 读取普通强类型表 | `gameData.GetExcelSheet<T>()` |
| 读取带子行的强类型表 | `gameData.GetSubrowExcelSheet<T>()` |
| 按行 ID 查询 | `sheet.TryGetRow(rowId, out var row)` |
| 读取跨表引用 | `rowRef.TryGetValue(out var target)` |
| 枚举当前客户端中的表 | `gameData.Excel.SheetNames` |
| 按表名读取原始数据 | `gameData.Excel.GetSheet("Action")`，通过 `Columns` 查看列定义，通过 `ReadColumn(index)` 读取行中的列值 |
| 按资源路径读取文件 | `gameData.GetFile(path)` 或 `gameData.GetFile<T>(path)`，例如 `gameData.GetFile("exd/root.exl")` |

强类型表依赖客户端的列布局。游戏更新后，如果表的列哈希与定义不匹配，加载该表时会抛出 `MismatchedColumnHashException`。遇到这种情况，请使用与目标客户端匹配的表定义。已附带的定义和列哈希可在 [TypedSchemas.json](Lumina.Core/Excel/Generated/TypedSchemas.json) 中查看。

## 随插件分发

普通 DLL 方式发布时，请将 `Lumina.ACT.Core.dll` 和它的运行时依赖一并放入插件发布目录。需要单 DLL 发布时，可以在构建阶段通过 ILRepack 合并，参考 [Simulant 的构建配置](../Simulant/ILRepack.targets)。

ACT 会在调用 `InitPlugin()` 前扫描插件程序集的类型，因此插件引用的程序集在扫描时就需要能够被加载。

## 上游与许可

资源读取代码源自 [Lumina](https://github.com/NotAdam/Lumina)，强类型表定义来自 [Lumina.Excel](https://github.com/NotAdam/Lumina.Excel) 和 [EXDSchema](https://github.com/xivdev/EXDSchema)。上游版本、许可证和第三方依赖见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
