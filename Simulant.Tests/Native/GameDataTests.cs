using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Simulant.Game.Data;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
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
                var memoryType = plugin.GetType("Simulant.Game.GameConnection", true).GetProperty("Memory").PropertyType;
                Check(memoryType.FullName == "SimuMagic.ExternalProcessMemory" && memoryType.Assembly.GetName().Name == "SimuMagic",
                    "正式插件应直接加载内嵌的 SimuMagic 类型。");
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
                VerifyEntranceBarriers(data);

                Check(GameDataService.ResolveSqPackPath(Path.GetDirectoryName(data.SqPackPath)) == data.SqPackPath, "游戏目录解析。");

                using (var raw = new GameData(data.SqPackPath))
                {
                    try
                    {
                        raw.GetExcelSheet<WrongAction>();
                        throw new Exception("错误布局未被拒绝。");
                    }
                    catch (Lumina.Excel.Exceptions.MismatchedColumnHashException)
                    {
                        _checks++;
                    }
                }

                data.Dispose();

                try
                {
                    data.GetSheet<ActionRow>();
                    throw new Exception("释放后仍能查表。");
                }
                catch (ObjectDisposedException)
                {
                    _checks++;
                }
            }

            using (var reopened = new GameDataService(path))
                Check(reopened.GetSheet<ActionRow>().GetRow(3).Name.ExtractText() == "冲刺", "重新打开客户端。");
        }

        private static void VerifyEntranceBarriers(GameDataService data)
        {
            var spawnData = typeof(GameDataService).Assembly.GetType("Simulant.Core.Zone.TerritorySpawnData");
            var scan = spawnData.GetMethod("Scan", BindingFlags.Static | BindingFlags.NonPublic);
            var ids = spawnData.GetProperty("BarrierLayoutIds", BindingFlags.Instance | BindingFlags.NonPublic);
            var points = spawnData.GetProperty("Points", BindingFlags.Instance | BindingFlags.NonPublic);
            var pointType = points.PropertyType.GetElementType();
            var position = pointType.GetProperty("Position", BindingFlags.Instance | BindingFlags.NonPublic);
            var circle = pointType.GetProperty("Circle", BindingFlags.Instance | BindingFlags.NonPublic);
            var circleId = circle.PropertyType.GetProperty("LayoutId", BindingFlags.Instance | BindingFlags.NonPublic);
            var territories = data.GetSheet<TerritoryType>();
            (uint[] LayoutIds, Vector3? SpawnPosition, uint?[] CircleIds) LoadBarriers(uint territory)
            {
                var result = scan.Invoke(null, new object[] { data, territories.GetRow(territory) });
                var candidates = ((Array)points.GetValue(result)).Cast<object>().ToArray();
                return ((uint[])ids.GetValue(result), candidates.Length == 0 ? null : (Vector3?)position.GetValue(candidates[0]),
                    candidates.Select(point => circle.GetValue(point) is object entrance ? (uint?)circleId.GetValue(entrance) : null).ToArray());
            }

            // 回放的入口生成包，坐标按模拟器的 XY 地面、Z 高度表示。
            var samples = new[]
            {
                new
                {
                    Territory = 1238u,
                    Wall = 0xA58C07u,
                    X = 100f,
                    Y = 115f,
                    Z = 0f
                },
                new
                {
                    Territory = 887u,
                    Wall = 0x7C190Bu,
                    X = 100f,
                    Y = 115f,
                    Z = 0f
                },
                new
                {
                    Territory = 1363u,
                    Wall = 0xBC174Cu,
                    X = 100.4117f,
                    Y = 115.7509f,
                    Z = 0f
                },
                new
                {
                    Territory = 777u,
                    Wall = 0x700D76u,
                    X = 100f,
                    Y = 116f,
                    Z = 0f
                },
                new
                {
                    Territory = 968u,
                    Wall = 0x875BE0u,
                    X = 100f,
                    Y = 118f,
                    Z = 0f
                },
                new
                {
                    Territory = 1036u,
                    Wall = 0x3E82C2u,
                    X = 361.8817f,
                    Y = -225.1813f,
                    Z = 46.09275f
                },
                new
                {
                    Territory = 1045u,
                    Wall = 0x3FBFD2u,
                    X = -16f,
                    Y = 0f,
                    Z = 0f
                },
                new
                {
                    Territory = 1069u,
                    Wall = 0x8E29C2u,
                    X = 5f,
                    Y = 246f,
                    Z = -3.9955f
                }
            };

            foreach (var sample in samples)
            {
                var (layoutIds, spawnPosition, circleIds) = LoadBarriers(sample.Territory);
                Check(layoutIds.Length == 1 && layoutIds[0] == sample.Wall,
                    $"区域 {sample.Territory} 应选中回放中 EObj 绑定的出生圈。");
                Check(spawnPosition.HasValue &&
                    Vector3.Distance(spawnPosition.Value, new Vector3(sample.X, sample.Y, sample.Z)) < 0.001f,
                    $"区域 {sample.Territory} 的出生点必须与回放一致。");
                Check(circleIds[0] == sample.Wall, $"区域 {sample.Territory} 的推荐出生点应保留圈的 LayoutId。");
            }

            var diadem = LoadBarriers(656);
            Check(diadem.LayoutIds.Length == 9 && diadem.LayoutIds.Contains(6600219u),
                "云冠群岛应识别九个入场圈，包括北侧的 _fly 版入场圈。");
            Check(diadem.CircleIds.OfType<uint>().OrderBy(id => id).SequenceEqual(diadem.LayoutIds.OrderBy(id => id)),
                "云冠群岛九个入场圈都应出现在候选出生点中。");
            Check(diadem.SpawnPosition.HasValue &&
                Vector3.Distance(diadem.SpawnPosition.Value, new Vector3(-766.9147f, 513.2558f, 359.8997f)) < 0.001f,
                "云冠群岛应使用首个 EObj 的场景坐标作为回退出生点。");
            var quest = LoadBarriers(496);
            Check(quest.LayoutIds.Length == 0 && quest.SpawnPosition == new Vector3(224.01f, -137.108f, 300.739f),
                "任务战斗应使用本区域的 Level 落点，并排除共享场景里属于其他副本的圈。");
            Check(LoadBarriers(223).CircleIds.OfType<uint>().SequenceEqual(new[] { 4253304u }) &&
                LoadBarriers(298).CircleIds.OfType<uint>().SequenceEqual(new[] { 4258415u }) && !quest.CircleIds.Any(id => id.HasValue),
                "共用库尔札斯中央高地地图的区域应各自列出所属入场圈。");

            var guildhest = LoadBarriers(214);
            Check(guildhest.LayoutIds.SequenceEqual(new[] { 4133115u }) &&
                guildhest.SpawnPosition == new Vector3(-165.3214f, -213.3883f, 44.63107f),
                "行会令应匹配其事件图层的出生点和圈。");
            var field = LoadBarriers(134);
            Check(field.LayoutIds.Length == 0 && field.SpawnPosition == new Vector3(224f, -261f, 113.1f),
                "野外应落在主传送水晶附近的玩家出生范围。");
            Check(LoadBarriers(399).SpawnPosition == new Vector3(-488.7798f, 734.6368f, 137.5155f),
                "没有传送水晶的野外地图应使用 Warp 指定的玩家出生范围。");

            var jeuno = LoadBarriers(1248);
            Check(jeuno.LayoutIds.Length == 1 && jeuno.LayoutIds[0] == 10918242,
                "朱诺：第一巡行应识别 b0286 入场圈。");
            var onsal = LoadBarriers(888);
            Check(onsal.LayoutIds.Length == 3 && onsal.LayoutIds.Contains(8131163u),
                "昂萨哈凯尔应识别 EObj 跨文件绑定的三个入场圈。");
            foreach (var territory in new[] { 936u, 937u })
            {
                var delubrum = LoadBarriers(territory);
                Check(delubrum.LayoutIds.Length == 1 && delubrum.LayoutIds[0] == 8548023,
                    $"区域 {territory} 应识别女王古殿入场圈。");
            }
            var crescent = LoadBarriers(1346);
            Check(crescent.LayoutIds.Length == 0 && crescent.SpawnPosition == new Vector3(888.4536f, 882.024f, 258.5f),
                "新月岛北部应落在入口区域，并保留活动圈。");
            Check(LoadBarriers(1252).SpawnPosition == new Vector3(850.9487f, -704.1298f, 73.05965f),
                "新月岛南部应优先使用 PublicContent 指定的出生范围。");

            var palace = LoadBarriers(561);
            Check(palace.LayoutIds.Length == 42 && palace.LayoutIds.Contains(6264186u) && !palace.SpawnPosition.HasValue,
                "死者宫殿应识别房间模板里的 b0945 圈，并由实际楼层决定出生点。");
            var pilgrim = LoadBarriers(1289);
            Check(pilgrim.LayoutIds.Contains(11781484u) && pilgrim.LayoutIds.Contains(11936290u) && pilgrim.SpawnPosition.HasValue,
                "朝圣交错路应同时识别房间模板和 EObj 绑定的圈，并从 EObj 获取回退出生点。");

            foreach (var territory in new[] { 755u, 1122u })
                Check(LoadBarriers(territory).LayoutIds.Length == 1,
                    $"现有预设区域 {territory} 的场地资源也应能识别出生圈。");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);

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
