using Simulant.Simulation.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace Simulant.Content.Local
{
    internal sealed class LocalChoice
    {
        internal string Id, Label, Default;
        internal string[] Values;
        internal bool Random;
        internal string Resolve(string selected, Random random)
            => selected == "*" ? Values[random.Next(Values.Length)] : selected;
    }

    internal sealed class LocalCondition
    {
        internal string Type, Path, Actor, Key, Value;
        internal string Target;
        internal ushort MinStacks;
        internal bool Passed;
        internal uint StatusId;
        internal double Percent;
        internal int Role = -1, CenterRole = -1, Min, Max;
        internal uint ObjectId;
        internal int[] Slots;
        internal Vector2 Position;
        internal float? Heading;
        internal ActionArea Area;
        internal LocalCondition[] Children;

        internal bool MatchesArea(IEnumerable<Vector2> positions, Vector2 origin, float heading)
        {
            var count = positions.Count(point => Area.Contains(point, origin, heading));
            return count >= Min && count <= Max;
        }
    }

    internal sealed class LocalSignal
    {
        internal string Type, Actor, Check;
        internal uint ActionId;
        internal bool? Passed;
        internal bool Matches(LocalSignal value) => Type == value.Type && Actor == value.Actor &&
            ActionId == value.ActionId && Check == value.Check && (!Passed.HasValue || Passed == value.Passed);
    }

    internal sealed partial class LocalEncounter
    {
        internal readonly List<LocalChoice> Choices = new List<LocalChoice>();
        private readonly List<LocalCondition> _conditions = new List<LocalCondition>();

        private void ReadOptions(JsonElement options)
        {
            foreach (var option in Array(options, "options"))
            {
                var path = $"options[{Choices.Count}]";
                Fields(option, path, "id label values default", "random");
                var choice = new LocalChoice
                {
                    Id = Text(option, "id", path + "."), Label = Text(option, "label", path + "."),
                    Default = Text(option, "default", path + "."),
                    Random = option.TryGetProperty("random", out var random) && Boolean(random, path + ".random"),
                    Values = Array(option.GetProperty("values"), path + ".values").Select(v =>
                        v.ValueKind == JsonValueKind.String ? v.GetString() : null).ToArray()
                };
                if (Choices.Any(c => c.Id == choice.Id))
                    throw Invalid(path + ".id", "选项名称重复");
                if (choice.Values.Length == 0 || choice.Values.Any(s => string.IsNullOrWhiteSpace(s) || s == "*") ||
                    choice.Values.Distinct().Count() != choice.Values.Length)
                    throw Invalid(path + ".values", "需要非空且不重复的选项，星号保留给随机");
                if (!choice.Values.Contains(choice.Default) && !(choice.Random && choice.Default == "*"))
                    throw Invalid(path + ".default", "默认值不在候选项中");
                Choices.Add(choice);
            }
        }

        private LocalEvent[] ReadSteps(JsonElement steps, string path, HashSet<string> actors, uint version)
        {
            var list = Array(steps, path).Select((s, i) => ReadEvent(s, $"{path}[{i}]", actors, version)).ToArray();
            if (list.Length == 0)
                throw Invalid(path, "时间轴不能为空");
            AllEvents.AddRange(list);
            return list;
        }

        private void ReadControl(LocalEvent e, JsonElement node, HashSet<string> actors, uint version)
        {
            var path = e.Path;
            if (e.Op == "judge")
                Fields(node, path, "at op id if text");
            else if (e.Op == "branch")
                Fields(node, path, "at op if then", "else");
            else
            {
                Fields(node, path, "at op timeout then", "event if else");
                if (node.TryGetProperty("if", out _) == node.TryGetProperty("event", out _))
                    throw Invalid(path, "trigger 需要且只能选择 event 或 if");
                e.Timeout = Number(node.GetProperty("timeout"), path + ".timeout", double.MaxValue);
                if (e.Timeout <= 0) throw Invalid(path + ".timeout", "需要正秒数");
            }
            if (node.TryGetProperty("if", out var condition))
                e.Condition = ReadCondition(condition, path + ".if", actors);
            if (node.TryGetProperty("event", out var signal))
            {
                var p = path + ".event";
                var type = TypeOf(signal, p);
                switch (type)
                {
                    case "castStarted":
                    case "castResolved":
                        Fields(signal, p, "type actor actionId");
                        e.On = new LocalSignal { Type = type, Actor = ActorName(signal, p, actors),
                            ActionId = Integer(signal.GetProperty("actionId"), p + ".actionId", uint.MaxValue, 1) };
                        break;
                    case "checkResolved":
                        Fields(signal, p, "type check", "passed");
                        e.On = new LocalSignal { Type = type, Check = Text(signal, "check", p + "."),
                            Passed = signal.TryGetProperty("passed", out var b) ? Boolean(b, p + ".passed") : (bool?)null };
                        break;
                    default: throw Invalid(p + ".type", "不支持的触发事件 " + type);
                }
            }
            if (node.TryGetProperty("then", out var yes))
                e.Then = ReadSteps(yes, path + ".then", actors, version);
            if (node.TryGetProperty("else", out var no))
                e.Else = ReadSteps(no, path + ".else", actors, version);
        }

        private static string TypeOf(JsonElement node, string path)
            => node.ValueKind == JsonValueKind.Object && node.TryGetProperty("type", out _)
                ? Text(node, "type", path + ".") : throw Invalid(path + ".type", "缺少类型");

        private static string ActorName(JsonElement node, string path, HashSet<string> actors)
        {
            var id = Text(node, "actor", path + ".");
            return actors.Contains(id) ? id : throw Invalid(path + ".actor", "未声明角色 " + id);
        }

        private int PartySlot(JsonElement value, string path)
        {
            var slot = value.ValueKind == JsonValueKind.String ? System.Array.IndexOf(Roles, value.GetString()) : -1;
            return PartyPositions != null && slot >= 0 ? slot : throw Invalid(path, "需要 party 中的职能");
        }

        private LocalCondition ReadCondition(JsonElement node, string path, HashSet<string> actors)
        {
            var c = new LocalCondition { Type = TypeOf(node, path), Path = path };
            switch (c.Type)
            {
                case "all":
                case "any":
                    Fields(node, path, "type conditions");
                    c.Children = Array(node.GetProperty("conditions"), path + ".conditions")
                        .Select((v, i) => ReadCondition(v, $"{path}.conditions[{i}]", actors)).ToArray();
                    if (c.Children.Length == 0) throw Invalid(path + ".conditions", "条件列表不能为空");
                    break;
                case "not":
                    Fields(node, path, "type condition");
                    c.Children = new[] { ReadCondition(node.GetProperty("condition"), path + ".condition", actors) };
                    break;
                case "optionEquals":
                    Fields(node, path, "type option value");
                    c.Key = Text(node, "option", path + ".");
                    c.Value = Text(node, "value", path + ".");
                    if (!Choices.Any(o => o.Id == c.Key && o.Values.Contains(c.Value)))
                        throw Invalid(path + ".option", "选项或候选值未声明");
                    break;
                case "checkResult":
                    Fields(node, path, "type check passed");
                    c.Key = Text(node, "check", path + ".");
                    c.Passed = Boolean(node.GetProperty("passed"), path + ".passed");
                    break;
                case "hpAtMost":
                    Fields(node, path, "type actor percent");
                    c.Percent = Number(node.GetProperty("percent"), path + ".percent", 100);
                    if (c.Percent < 0) throw Invalid(path + ".percent", "百分比不能为负数");
                    break;
                case "hasStatus":
                    Fields(node, path, "type statusId", "actor role target minStacks");
                    if (new[] { "actor", "role", "target" }.Count(k => node.TryGetProperty(k, out _)) != 1)
                        throw Invalid(path, "需要一个 actor、role 或 target");
                    c.StatusId = Integer(node.GetProperty("statusId"), path + ".statusId", ushort.MaxValue, 1);
                    if (node.TryGetProperty("minStacks", out var minimum))
                        c.MinStacks = (ushort)Integer(minimum, path + ".minStacks", ushort.MaxValue, 1);
                    break;
                case "targetIs":
                    Fields(node, path, "type target role");
                    break;
                case "objectActive":
                    Fields(node, path, "type actor objectId");
                    c.ObjectId = Integer(node.GetProperty("objectId"), path + ".objectId", uint.MaxValue, 1);
                    break;
                case "areaCount":
                    Fields(node, path, "type shape min max", "position actor centerRole heading roles target");
                    if (node.TryGetProperty("roles", out _) && node.TryGetProperty("target", out _))
                        throw Invalid(path + ".target", "不能同时筛选 roles 和 target");
                    if (PartyPositions == null || new[] { "actor", "position", "centerRole" }.Count(k => node.TryGetProperty(k, out _)) != 1)
                        throw Invalid(path, "areaCount 需要 party 和唯一的中心 position、actor 或 centerRole");
                    if (node.TryGetProperty("centerRole", out var center)) c.CenterRole = PartySlot(center, path + ".centerRole");
                    c.Area = ReadShape(node.GetProperty("shape"), path + ".shape");
                    c.Min = (int)Integer(node.GetProperty("min"), path + ".min", 8);
                    c.Max = (int)Integer(node.GetProperty("max"), path + ".max", 8, (uint)c.Min);
                    if (node.TryGetProperty("roles", out var roles))
                        c.Slots = RoleSlots(roles, path + ".roles");
                    else c.Slots = Enumerable.Range(0, Roles.Length).ToArray();
                    if (node.TryGetProperty("position", out var point)) c.Position = Point(point, path + ".position");
                    if (node.TryGetProperty("heading", out var heading)) c.Heading = (float)Number(heading, path + ".heading", float.MaxValue);
                    break;
                default: throw Invalid(path + ".type", "不支持的条件 " + c.Type);
            }
            if (node.TryGetProperty("actor", out _)) c.Actor = ActorName(node, path, actors);
            if (node.TryGetProperty("role", out var role)) c.Role = PartySlot(role, path + ".role");
            if (node.TryGetProperty("target", out _)) c.Target = Text(node, "target", path + ".");
            _conditions.Add(c);
            return c;
        }

        private static ActionArea ReadShape(JsonElement node, string path)
        {
            float Size(string key) => (float)Number(node.GetProperty(key), path + "." + key, float.MaxValue);
            try
            {
                switch (TypeOf(node, path))
                {
                    case "circle": Fields(node, path, "type radius"); return ActionArea.Circle(Size("radius"));
                    case "donut": Fields(node, path, "type inner outer"); return ActionArea.Donut(Size("inner"), Size("outer"));
                    case "cone": Fields(node, path, "type radius degrees"); return ActionArea.Cone(Size("radius"), Size("degrees"));
                    case "rectangle": Fields(node, path, "type length width"); return ActionArea.Rectangle(Size("length"), Size("width"));
                    case "cross": Fields(node, path, "type length width"); return ActionArea.Cross(Size("length"), Size("width"));
                    default: throw Invalid(path + ".type", "不支持的范围");
                }
            }
            catch (ArgumentOutOfRangeException ex) { throw Invalid(path, ex.Message); }
        }

        private int[] RoleSlots(JsonElement node, string path)
        {
            var slots = Array(node, path).Select(v => PartySlot(v, path)).ToArray();
            if (slots.Length == 0 || slots.Distinct().Count() != slots.Length)
                throw Invalid(path, "需要非空且不重复的职能");
            return slots;
        }

        private void ValidateCheckReferences()
        {
            var checks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in AllEvents.Where(e => e.CheckId != null))
                if (!checks.Add(e.CheckId)) throw Invalid(e.Path + ".id", "重复的判定名 " + e.CheckId);
            foreach (var e in AllEvents.Where(e => e.On?.Check != null))
                if (!checks.Contains(e.On.Check)) throw Invalid(e.Path + ".event.check", "未声明判定 " + e.On.Check);
            foreach (var c in _conditions.Where(c => c.Type == "checkResult"))
                if (!checks.Contains(c.Key)) throw Invalid(c.Path + ".check", "未声明判定 " + c.Key);
        }

        private void ValidateTargets()
        {
            var keys = new Dictionary<string, LocalEvent>();
            foreach (var e in AllEvents.Where(s => s.Op == "selectTarget" || s.Op == "trackTether"))
            {
                if (e.Key == "$member" || keys.ContainsKey(e.Key)) throw Invalid(e.Path + ".key", "目标名保留或重复");
                if ((e.By == "enmity" || e.By == "firstEnmity") && !InitialEnmity.ContainsKey(e.Actor))
                    throw Invalid(e.Path + ".by", "请给该角色声明 enmity");
                keys.Add(e.Key, e);
            }
            void Reference(string key, string path, bool member)
            {
                if (key != null && !(key == "$member" && member) && !keys.ContainsKey(key))
                    throw Invalid(path, "目标未声明或不在 forParty 内：" + key);
            }
            void Condition(LocalCondition c, bool member)
            {
                if (c == null) return;
                Reference(c.Target, c.Path + ".target", member);
                if (c.Children != null) foreach (var child in c.Children) Condition(child, member);
            }
            void Steps(IEnumerable<LocalEvent> steps, bool member)
            {
                foreach (var e in steps)
                {
                    Reference(e.Target, e.Path + ".target", member);
                    Condition(e.Condition, member);
                    if (e.Op == "resolveTether" && (!keys.TryGetValue(e.Key, out var line) || line.Op != "trackTether"))
                        throw Invalid(e.Path + ".key", "需要已声明的动态连线");
                    if (e.Op == "partyHit" && !InitialEnmity.ContainsKey(e.Actor))
                        throw Invalid(e.Path + ".actor", "目标需要 enmity");
                    if (e.Then != null) Steps(e.Then, member || e.Op == "forParty");
                    if (e.Else != null) Steps(e.Else, member);
                }
            }
            Steps(Timeline, false);
        }
    }
}
