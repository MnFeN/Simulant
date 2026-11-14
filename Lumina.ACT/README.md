# Lumina.ACT 读取核心

复用 Triggernometry 项目“移植 Lumina”会话保存的 2026-10-30 备份。保留 Lumina 7.7.2 的资源读取核心，以及 Stable 的 1,198 张强类型表；不包含独立 ACT 浏览器、构建产物或客户端文件。

这里使用 C# 7.3 / .NET Framework 4.8，兼容库版本与 Simulant 保持一致。上游提交和授权见 `THIRD_PARTY_NOTICES.md`，生成表的来源与列校验信息见 `Lumina.Core/Excel/Generated/TypedSchemas.json`。迁入的上游源码保留原有注释。

Simulant 通过 `GameDataService` 打开配置中 `FfxivPath` 指定的 `ffxiv_dx11.exe` 所在客户端，使用简体中文资源，不依赖解析插件提供路径或语言。配置入口为“杂项 → FFXIV 安装路径”；路径默认为空，插件启动和手动初始化时枚举当前 `ffxiv_dx11` 进程，找到唯一安装目录后保存完整可执行文件路径。没有进程时提示手动设置或开启游戏后重新初始化，不持续扫描；不同安装目录多开时必须指定路径。已有路径时无需启动游戏即可加载资源；切换游戏进程不会切换资源目录，卸载时释放资源。

业务代码使用 `GetSheet<T>()`、`TryGetRow`、`RowRef<T>` 读取数据。表结构不兼容会明确报错，不绕过校验。游戏数据统一读取客户端资源，不再内嵌游戏表或提供旧数据源回退。

核心和运行依赖在构建时通过 ILRepack 合并进 Simulant.dll，发布只需这一个 DLL。ACT 会先扫描程序集类型再创建插件，不能依赖模块初始化后才注册的内嵌依赖加载器。ACT、解析插件及 Machina 仍由宿主提供，不参与合并。调试符号使用 Portable PDB，以便合并时保留源码定位。

日常构建不需要生成器；以后更新表定义时，需使用原备份的 TypedGenerator 和匹配客户端的 EXDSchema，仅保留 Stable 定义及对应清单，不能只改变字段名而保留错误的偏移。
