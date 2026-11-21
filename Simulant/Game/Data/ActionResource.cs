using Lumina;
using Lumina.Data;
using Simulant.Utils.Maths;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Game.Data
{
    /// <summary>供检索界面使用的独立快照，绘制和筛选时不再访问游戏文件。</summary>
    internal sealed class ActionResource
    {
        public uint Id { get; set; }
        public string Name { get; set; } = "";
        public float CastTime { get; set; }
        public byte Shape { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public byte Category { get; set; }
        public sbyte Attack { get; set; }
        public byte Aspect { get; set; }
        public uint OmenId { get; set; }
        public string Omen { get; set; } = "";
        public int AnimationEndId { get; set; }
        public string AnimationEnd { get; set; } = "";
        public uint CastVfxId { get; set; }
        public bool IsPlayer { get; set; }
        public bool IsNew => false;
        public string[] VfxPaths { get; set; } = Array.Empty<string>();
        public bool HasTimelineAnimation { get; set; }
        public string HexId => Id.ToString("X", CultureInfo.InvariantCulture);

        internal bool HasModelTimeline => AnimationEndId != 0 && ActionTimelineModel.IsModelTimeline(AnimationEnd);
        internal bool CanPlayOnNpc => HasModelTimeline || AnimationEndId != 0 && HasTimelineAnimation &&
            ActionTimelineModel.IsSharedTimeline(AnimationEnd);
        internal bool CanPlayOnSelf => AnimationEndId != 0 && HasTimelineAnimation && ActionTimelineModel.IsPlayerTimeline(AnimationEnd);
        internal bool CanPreviewAnimation => VfxPaths.Length != 0 || CanPlayOnNpc || CanPlayOnSelf;

        public string DisplayName
        {
            get
            {
                var end = Name.StartsWith("_rsv_", StringComparison.Ordinal) ? Name.IndexOf('_', 5) : -1;
                return end > 5 ? Name.Substring(0, end) : Name;
            }
        }

        public static ActionResource[] Load(string sqPackPath, Language language, CancellationToken cancellation, Action<string> warning = null)
        {
            // 使用独立读取器，插件重新初始化或切换客户端时不会释放本次后台读取正在使用的资源。
            using (var data = new GameData(sqPackPath, new LuminaOptions
            {
                DefaultExcelLanguage = language
            }))
            {
                var timelines = new Dictionary<string, (string[] VfxPaths, bool HasAnimation)>(StringComparer.OrdinalIgnoreCase);
                var rows = new List<ActionResource>();

                foreach (var row in data.GetExcelSheet<ActionRow>())
                {
                    cancellation.ThrowIfCancellationRequested();
                    var name = row.Name.ExtractText();

                    if (row.Icon == 0 || (name.Length == 0 && row.ActionCategory.RowId == 0 && row.Aspect == 0 &&
                        unchecked((sbyte)row.AttackType.RowId) == -1 && row.EffectRange == 0 && row.XAxisModifier == 0 &&
                        row.Cast100ms == 0 && row.Omen.RowId == 0))
                        continue;

                    var timeline = row.AnimationEnd.ValueNullable?.Key.ExtractText() ?? "";
                    if (!timelines.TryGetValue(timeline, out var content))
                    {
                        content = (Array.Empty<string>(), false);
                        if (!ActionTimelineModel.IsSkeletonTemplate(timeline) && (ActionTimelineModel.IsPlayerTimeline(timeline) ||
                            ActionTimelineModel.IsSharedTimeline(timeline) || ActionTimelineModel.IsModelTimeline(timeline) ||
                            timeline.IndexOf("gimmick", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            try
                            {
                                var file = data.GetFile("chara/action/" + timeline + ".tmb");

                                if (file != null)
                                    using (file.Reader)
                                        content = ReadTimeline(file.Data);
                            }
                            catch (Exception error)
                            {
                                warning?.Invoke($"读取时间轴 {timeline} 失败：{error.Message}");
                            }
                        }

                        timelines.Add(timeline, content);
                    }

                    rows.Add(new ActionResource
                    {
                        Id = row.RowId,
                        Name = name,
                        CastTime = row.Cast100ms / 10f,
                        Shape = row.CastType,
                        X = row.XAxisModifier / 2f,
                        Y = row.EffectRange,
                        Category = (byte)row.ActionCategory.RowId,
                        Attack = unchecked((sbyte)row.AttackType.RowId),
                        Aspect = row.Aspect,
                        OmenId = row.Omen.RowId,
                        Omen = row.Omen.ValueNullable?.Path.ExtractText() ?? "",
                        AnimationEndId = unchecked((short)row.AnimationEnd.RowId),
                        AnimationEnd = timeline,
                        CastVfxId = row.VFX.ValueNullable?.VFX.RowId ?? 0,
                        IsPlayer = row.IsPlayerAction,
                        VfxPaths = content.VfxPaths,
                        HasTimelineAnimation = content.HasAnimation
                    });
                }

                return rows.ToArray();
            }
        }

        private static readonly Regex AvfxPaths = new Regex(@"(?:vfx|chara)/[a-zA-Z0-9_./-]+\.avfx",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        internal static string[] ExtractVfxPaths(byte[] data)
            => AvfxPaths.Matches(Encoding.GetEncoding(28591).GetString(data)).Cast<Match>().Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        private static (string[] VfxPaths, bool HasAnimation) ReadTimeline(byte[] data)
        {
            var paths = ExtractVfxPaths(data);
            if (data.Length < 12 || Encoding.ASCII.GetString(data, 0, 4) != "TMLB")
                return (paths, false);

            var hasAnimation = false;
            var offset = 12;
            for (var remaining = BitConverter.ToInt32(data, 8); remaining > 0; remaining--)
            {
                if (offset > data.Length - 8)
                    return (paths, false);
                var type = Encoding.ASCII.GetString(data, offset, 4);
                var length = BitConverter.ToInt32(data, offset + 4);
                if (length < 8 || length > data.Length - offset)
                    return (paths, false);

                hasAnimation |= type == "C010" || type == "C002" || type == "C053";
                offset += length;
            }
            return (paths, hasAnimation);
        }

        internal static string ShapeName(int shape)
        {
            switch (shape)
            {
                case 0:
                    return "无 (0)";
                case 1:
                    return "单体 (1)";
                case 2:
                    return "圆形 (2)";
                case 3:
                    return "扇形+ (3)";
                case 4:
                    return "矩形+ (4)";
                case 5:
                    return "圆形+ (5)";
                case 8:
                    return "矩形至 (8)";
                case 10:
                    return "月环 (10)";
                case 11:
                    return "十字 (11)";
                case 12:
                    return "矩形 (12)";
                case 13:
                    return "扇形 (13)";
                case 14:
                    return "三角 (14)";
                case 15:
                    return "矩形穿 (15)";
                case 16:
                    return "右矩形至 (16)";
                case 17:
                    return "左矩形至 (17)";
                default:
                    return $"未知 ({shape})";
            }
        }

        private static string NamedValue(int value, string[] names)
            => value == 0 ? "" : $"{(value > 0 && value < names.Length ? names[value] : "未知")} ({value})";

        internal object CellValue(int column, bool copying = false)
        {
            switch (column)
            {
                case 0:
                    return Id;
                case 1:
                    return HexId;
                case 2:
                    return copying ? Name : DisplayName;
                case 3:
                    return copying ? CastTime.ToString("0.0", CultureInfo.InvariantCulture) :
                    CastTime == 0 ? "瞬时" : CastTime.ToString("0.0", CultureInfo.InvariantCulture) + " s";
                case 4:
                    return ShapeName(Shape);
                case 5:
                    return !copying && X == 0 ? "" : X.ToString("0.#", CultureInfo.InvariantCulture);
                case 6:
                    return !copying && Y == 0 ? "" : Y.ToString("0.#", CultureInfo.InvariantCulture);
                case 7:
                    return NamedValue(Category, CategoryNames);
                case 8:
                    return Attack == -1 ? "无 (-1)" : NamedValue(Attack, AttackNames);
                case 9:
                    return NamedValue(Aspect, AspectNames);
                case 10:
                    return copying ? Omen : OmenId == 0 ? "" : $"{Omen}  ({OmenId})";
                case 11:
                    return copying ? AnimationEnd : AnimationEndId == 0 ? "" : $"{AnimationEnd}  ({AnimationEndId})";
                default:
                    throw new ArgumentOutOfRangeException(nameof(column));
            }
        }

        internal IComparable SortKey(int column)
        {
            switch (column)
            {
                case 0:
                case 1:
                    return Id;
                case 2:
                    return Name;
                case 3:
                    return CastTime;
                case 4:
                    return Shape;
                case 5:
                    return X;
                case 6:
                    return Y;
                case 7:
                    return Category;
                case 8:
                    return Attack;
                case 9:
                    return Aspect;
                case 10:
                    return OmenId;
                case 11:
                    return AnimationEndId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(column));
            }
        }

        private static readonly string[] CategoryNames =
        {
            "",
            "普攻",
            "魔法",
            "战技",
            "能力",
            "物品",
            "采集",
            "生产",
            "事件",
            "LB",
            "系统",
            "系统2",
            "坐骑",
            "特殊",
            "交互",
            "LB2",
            "未知",
            "炮击",
            "时尚"
        };
        private static readonly string[] AttackNames =
        {
            "",
            "斩击",
            "突刺",
            "打击",
            "射击",
            "魔法",
            "吐息",
            "音波",
            "极限"
        };
        private static readonly string[] AspectNames =
        {
            "",
            "火",
            "冰",
            "风",
            "土",
            "雷",
            "水",
            "无"
        };

        internal void GetScales(out float x, out float y)
        {
            x = X;
            y = Y;

            if (Shape == 2 || Shape == 3 || Shape == 5 || Shape == 10 || Shape == 13)
            {
                if (x == 0)
                    x = y;
            }
            else if (Shape == 8 || Shape == 15 || Shape == 16 || Shape == 17)
            {
                if (y == 0)
                    y = 40;
            }
            else if (Shape == 0)
            {
                if (y == 0)
                    y = 40;

                if (x == 0)
                    x = y;
            }
        }

        internal string OmenCommand()
        {
            var omen = Omen;

            if (omen == "general02f")
                omen = "Rect";
            else if (omen == "general_x02f")
                omen = "Rect2";
            else if (omen == "general_1bf")
                omen = "Circle";
            else
            {
                var fan = Regex.Match(omen, @"^gl_fan(030|045|060|090|120|150|180|210)_1bf$");

                if (fan.Success)
                    omen = "Fan" + int.Parse(fan.Groups[1].Value, CultureInfo.InvariantCulture);
            }

            var command = "_=" + DisplayName;

            if (omen.Length == 0)
            {
                if ((X == 0 && Y == 0 && Shape == 0) || ((Shape == 2 || Shape == 5) && Y >= 40))
                    return command + " | tts=";

                switch (Shape)
                {
                    case 2:
                    case 5:
                        omen = "Circle";
                        break;
                    case 3:
                    case 13:
                        omen = "Fan60";
                        break;
                    case 4:
                    case 8:
                    case 12:
                    case 15:
                    case 16:
                    case 17:
                        omen = "Rect";
                        break;
                    case 10:
                        omen = "gl_sircle_2010bf";
                        break;
                    case 11:
                        omen = "Rect2";
                        break;
                    default:
                        return command + " | tts=";
                }
            }

            GetScales(out var x, out var y);
            return command + string.Format(CultureInfo.InvariantCulture, " | omen={0} | t={1:0.0} | Scale={2},{3},1",
                omen, Math.Max(0, CastTime - 0.3), x, y) + (Shape == 11 ? " | cross=1" : "");
        }

        internal static string ExportActions(IEnumerable<ActionResource> rows)
            => new XElement("ActionBundle", new XElement("Actions", rows.Select((row, i) => new XElement("Action",
                new XAttribute("OrderNumber", i + 1), new XAttribute("ActionType", "DictVariable"),
                new XAttribute("DictVariableOp", "Set"), new XAttribute("DictVariableName", "简易播报"),
                new XAttribute("DictVariableKey", row.HexId), new XAttribute("DictVariableValue", row.OmenCommand()))))).ToString();
    }

    /// <summary>一次搜索的界面快照；后台筛选不能读取控件。</summary>
    internal sealed class ActionResourceFilter
    {
        public bool NewOnly;
        public bool PreviewOnly;
        public bool PlayerOnly;
        public bool[] Shapes;
        public string HexId, Name, Expression, Omen, AnimationEnd;
        public int SortColumn;
        public bool SortAscending = true;

        public ActionResource[] Apply(ActionResource[] rows, CancellationToken cancellation)
        {
            var patterns = new[]
            {
                HexId,
                Name,
                Omen,
                AnimationEnd
            }.Select(pattern => string.IsNullOrWhiteSpace(pattern)
                ? null : new Regex(pattern.Trim(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))).ToArray();
            var shapeFilter = Shapes != null && Shapes.Any(value => value) && !Shapes.All(value => value);
            var tokens = string.IsNullOrWhiteSpace(Expression) ? null : MathParser.Lexer(Expression);
            var filtered = new List<ActionResource>();

            foreach (var row in rows)
            {
                cancellation.ThrowIfCancellationRequested();

                if (NewOnly && !row.IsNew || PreviewOnly && !row.CanPreviewAnimation || PlayerOnly && !row.IsPlayer ||
                    shapeFilter && (row.Shape >= Shapes.Length || !Shapes[row.Shape]))
                    continue;

                if (patterns[0] != null && !patterns[0].IsMatch(row.HexId) ||
                    patterns[1] != null && !patterns[1].IsMatch(row.Name) ||
                    patterns[2] != null && !patterns[2].IsMatch(row.Omen) ||
                    patterns[3] != null && !patterns[3].IsMatch(row.AnimationEnd))
                    continue;

                if (tokens != null && MathParser.IsZero(MathParser.MathParserLogic(tokens.Select(token => Substitute(row, token)).ToList())))
                    continue;

                filtered.Add(row);
            }

            cancellation.ThrowIfCancellationRequested();

            filtered.Sort((a, b) =>
            {
                var result = a.SortKey(SortColumn).CompareTo(b.SortKey(SortColumn));
                return result == 0 ? a.Id.CompareTo(b.Id) : SortAscending ? Math.Sign(result) : -Math.Sign(result);
            });

            return filtered.ToArray();
        }

        private static string Substitute(ActionResource row, string token)
        {
            double value;

            switch (token.ToLowerInvariant())
            {
                case "id":
                    value = row.Id;
                    break;
                case "t":
                    value = row.CastTime;
                    break;
                case "omen":
                    value = row.OmenId;
                    break;
                case "shape":
                    value = row.Shape;
                    break;
                case "vfx":
                    value = row.CastVfxId;
                    break;
                case "x":
                    value = row.X;
                    break;
                case "y":
                    value = row.Y;
                    break;
                case "category":
                    value = row.Category;
                    break;
                case "attack":
                    value = row.Attack;
                    break;
                case "aspect":
                    value = row.Aspect;
                    break;
                case "animationend":
                    value = row.AnimationEndId;
                    break;
                default:
                    return token;
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
