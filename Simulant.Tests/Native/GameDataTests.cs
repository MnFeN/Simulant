using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Simulant.Game.Data;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Tests.Native
{
    /// <summary>只读当前客户端，验证强类型行、关联引用与数据生命周期；测试目录仅需正式插件 DLL。</summary>
    internal static class GameDataTests
    {
        private static int _checks;

        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("请传入客户端根目录、game 或 sqpack 目录。");
                return 1;
            }
            try
            {
                // ACT 先从字节加载并扫描所有类型，再创建入口；这里不能提前执行模块初始化。
                var plugin = Assembly.Load(File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Simulant.dll")));
                var entry = plugin.GetTypes().Single(type => type.GetInterfaces().Any(i => i.FullName == "Advanced_Combat_Tracker.IActPluginV1"));
                Check(entry.FullName == "Simulant.PluginMain" && Activator.CreateInstance(entry) != null,
                    "只提供插件 DLL 时，ACT 应能先扫描类型，再创建唯一插件入口。");
                Check(!plugin.GetManifestResourceNames().Any(name => name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)),
                    "正式插件不应再嵌入游戏表 CSV 资源。");
                Run(args[0]);
                Console.WriteLine("客户端数据验证通过：" + _checks + " 项。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("客户端数据验证失败：" + error);
                return 1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string path)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using (var data = new GameDataService(path))
            {
                Console.WriteLine("读取并校验常用表：" + watch.ElapsedMilliseconds + " 毫秒。");
                var actions = data.GetSheet<ActionRow>();
                Check(ReferenceEquals(actions, data.GetSheet<ActionRow>()), "重复查表应复用缓存。");
                Check(actions.GetRow(3).Name.ExtractText() == "冲刺", "中文技能名称。");
                Check(!actions.TryGetRow(uint.MaxValue, out _), "不存在的行应返回 false。");
                Check(actions.GetRow(31).ClassJob.Value.Name.ExtractText() == "斧术师", "技能跨表引用。");

                Check(GameDataService.ResolveSqPackPath(Path.GetDirectoryName(data.SqPackPath)) == data.SqPackPath, "游戏目录解析。");
                using (var raw = new GameData(data.SqPackPath))
                {
                    try { raw.GetExcelSheet<WrongAction>(); throw new Exception("错误布局未被拒绝。"); }
                    catch (Lumina.Excel.Exceptions.MismatchedColumnHashException) { _checks++; }
                }
                data.Dispose();
                try { data.GetSheet<ActionRow>(); throw new Exception("释放后仍能查表。"); }
                catch (ObjectDisposedException) { _checks++; }
            }
            using (var reopened = new GameDataService(path))
                Check(reopened.GetSheet<ActionRow>().GetRow(3).Name.ExtractText() == "冲刺", "重新打开客户端。");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            _checks++;
        }

        [Sheet("Action", 0)]
        private struct WrongAction : IExcelRow<WrongAction>
        {
            public ExcelPage ExcelPage => null;
            public uint RowOffset => 0;
            public uint RowId => 0;
            public static WrongAction Create(ExcelPage page, uint offset, uint row) => default(WrongAction);
        }
    }
}
