using Lumina.Excel.Sheets;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game.Data;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Simulant.Content.Local
{
    internal sealed partial class LocalEncounter
    {
        internal string Id;
        internal string Name;
        internal string Author;
        internal string Description;
        internal int TerritoryId;
        internal uint Version;
        internal PhaseData Phase;
        internal WaymarkLayout Waymarks;
        internal readonly List<(string Id, NetworkNpcSpawnSpec Spawn)> Actors = new List<(string, NetworkNpcSpawnSpec)>();
        internal readonly HashSet<string> InitiallyHidden = new HashSet<string>();
        internal readonly Dictionary<string, int[]> InitialEnmity = new Dictionary<string, int[]>();
        internal readonly HashSet<string> Damageable = new HashSet<string>();
        internal Vector2[] PartyPositions;
        internal string SwapLabel;
        private int _swapA, _swapB;
        internal readonly List<LocalEvent> Timeline = new List<LocalEvent>();
        internal readonly List<LocalEvent> AllEvents = new List<LocalEvent>();

        internal static LocalEncounter Parse(string json)
        {
            using (var document = JsonDocument.Parse(json))
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var versionNode))
                    throw Invalid("schemaVersion", "需要版本号");
                var version = Integer(versionNode, "schemaVersion", 3, 1);
                Fields(root, "$", "schemaVersion id name territoryId", "author description environment actors timeline" +
                    (version >= 2 ? " party roleSwap waymarks" : "") + (version >= 3 ? " options" : ""));

                var result = new LocalEncounter
                {
                    Id = Text(root, "id"),
                    Version = version,
                    Name = Text(root, "name"),
                    TerritoryId = (int)Integer(root.GetProperty("territoryId"), "territoryId", ushort.MaxValue, 1),
                    Author = root.TryGetProperty("author", out _) ? Text(root, "author") : null,
                    Description = root.TryGetProperty("description", out _) ? Text(root, "description") : null,
                    Phase = PhaseData.TerritoryDefaults
                };
                if (!Regex.IsMatch(result.Id, @"\A[a-z0-9.-]{1,96}\z"))
                    throw Invalid("id", "请使用 1 至 96 个小写英文字母、数字、点或连字符");

                if (root.TryGetProperty("environment", out var env))
                {
                    Fields(env, "environment", "weatherId bgmId", "mapEffectFlags");
                    var flags = new List<ushort?>();
                    if (env.TryGetProperty("mapEffectFlags", out var slots))
                        foreach (var flag in Array(slots, "environment.mapEffectFlags"))
                            flags.Add(flag.ValueKind == JsonValueKind.Null ? (ushort?)null :
                                (ushort)Integer(flag, $"environment.mapEffectFlags[{flags.Count}]", ushort.MaxValue));
                    result.Phase = new PhaseData(result.Name,
                        (byte)Integer(env.GetProperty("weatherId"), "environment.weatherId", byte.MaxValue),
                        (ushort)Integer(env.GetProperty("bgmId"), "environment.bgmId", ushort.MaxValue), flags);
                }

                var hasActors = root.TryGetProperty("actors", out var actors);
                var hasTimeline = root.TryGetProperty("timeline", out var timeline);
                if (hasActors != hasTimeline)
                    throw Invalid("actors / timeline", "模拟练习需要同时提供角色和时间轴");
                if (root.TryGetProperty("waymarks", out var marks))
                {
                    if (!hasActors)
                        throw Invalid("waymarks", "标点随练习开始和停止");
                    Fields(marks, "waymarks", "", "A B C D 1 2 3 4");
                    result.Waymarks = WaymarkLayout.Center(0, 0);
                    foreach (var mark in marks.EnumerateObject())
                    {
                        var pos = Coordinates(mark.Value, "waymarks." + mark.Name);
                        result.Waymarks.At(mark.Name, pos.X, pos.Y, pos.Z);
                    }
                }
                if (root.TryGetProperty("party", out var party))
                {
                    if (!hasActors)
                        throw Invalid("party", "小队需要练习时间轴");
                    Fields(party, "party", string.Join(" ", Roles));
                    result.PartyPositions = Roles.Select(role => Point(party.GetProperty(role), "party." + role)).ToArray();
                }
                if (root.TryGetProperty("options", out var options))
                {
                    if (!hasActors)
                        throw Invalid("options", "模拟参数需要时间轴");
                    result.ReadOptions(options);
                }
                if (hasActors)
                    result.ReadExercise(actors, timeline, version);
                if (root.TryGetProperty("roleSwap", out var swap))
                {
                    Fields(swap, "roleSwap", "label roles");
                    var pair = Array(swap.GetProperty("roles"), "roleSwap.roles")
                        .Select(v => v.ValueKind == JsonValueKind.String ? System.Array.IndexOf(Roles, v.GetString()) : -1).ToArray();
                    if (result.PartyPositions == null || pair.Length != 2 || pair.Any(i => i < 0) || pair[0] == pair[1])
                        throw Invalid("roleSwap.roles", "请选择 party 中的两个不同职能");
                    result.SwapLabel = Text(swap, "label", "roleSwap.");
                    result._swapA = pair[0];
                    result._swapB = pair[1];
                }
                return result;
            }
        }

        internal int AssignedSlot(int slot, bool swap)
        {
            if (swap && SwapLabel != null)
            {
                if (slot == _swapA) return _swapB;
                if (slot == _swapB) return _swapA;
            }
            return slot;
        }

        internal string RoleText(string message, bool swap)
        {
            for (var i = 0; i < Roles.Length; i++)
                message = message.Replace("{" + Roles[i] + "}", Roles[AssignedSlot(i, swap)]);
            return message;
        }

        private void ReadExercise(JsonElement actors, JsonElement timeline, uint version)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var actor in Array(actors, "actors"))
            {
                var path = $"actors[{Actors.Count}]";
                Fields(actor, path, "id baseId nameId level position heading", version >= 3 ? "hidden enmity maxHp" : version == 2 ? "hidden" : "");
                var id = Text(actor, "id", path + ".");
                if (!ids.Add(id))
                    throw Invalid(path + ".id", "重复的角色名 " + id);
                if (actor.TryGetProperty("hidden", out var hidden) && Boolean(hidden, path + ".hidden"))
                    InitiallyHidden.Add(id);
                if (actor.TryGetProperty("enmity", out var scores))
                {
                    if (PartyPositions == null) throw Invalid(path + ".enmity", "仇恨表需要 party");
                    Fields(scores, path + ".enmity", "", string.Join(" ", Roles));
                    InitialEnmity.Add(id, Roles.Select(role => scores.TryGetProperty(role, out var value)
                        ? (int)Integer(value, path + ".enmity." + role, 100) : 0).ToArray());
                }
                uint maxHp = 10000;
                if (actor.TryGetProperty("maxHp", out var hp))
                {
                    if (!InitialEnmity.ContainsKey(id)) throw Invalid(path + ".maxHp", "可受伤角色需要 enmity");
                    maxHp = Integer(hp, path + ".maxHp", uint.MaxValue, 1);
                    Damageable.Add(id);
                }
                Actors.Add((id, new NetworkNpcSpawnSpec
                {
                    MaxHealthPoints = maxHp,
                    BNpcBaseId = Integer(actor.GetProperty("baseId"), path + ".baseId", int.MaxValue, 1),
                    BNpcNameId = Integer(actor.GetProperty("nameId"), path + ".nameId", int.MaxValue, version == 1 ? 1u : 0u),
                    EnemyListVisibility = InitiallyHidden.Contains(id) ? (bool?)false : null,
                    Targetable = !InitiallyHidden.Contains(id),
                    Level = (byte)Integer(actor.GetProperty("level"), path + ".level", byte.MaxValue, 1),
                    Pos = Coordinates(actor.GetProperty("position"), path + ".position"),
                    Heading = (float)Number(actor.GetProperty("heading"), path + ".heading", float.MaxValue)
                }));
            }
            Timeline.AddRange(ReadSteps(timeline, "timeline", ids, version));
            if (Actors.Count == 0 || Timeline.Count == 0)
                throw Invalid("actors / timeline", "模拟练习需要至少一个角色和一个事件，场地预览请省略这两个字段");
            ValidateCheckReferences();
            ValidateTargets();
            var occupiedUntil = Enumerable.Repeat(-1d, 40).ToArray();
            foreach (var node in Timeline.Where(e => e.Op == "object").OrderBy(e => e.At))
            {
                var slot = System.Array.FindIndex(occupiedUntil, end => end < node.At);
                if (slot < 0)
                    throw Invalid("timeline", "同时出现的场地物件超过 40 个");
                occupiedUntil[slot] = node.At + node.Duration;
            }
        }

        internal SimTimeline BuildTimeline(Action<LocalEvent> run)
        {
            var result = new SimTimeline();
            foreach (var node in Timeline)
                result.At(node.At, () => run(node), $"{node.Op} {node.Actor} {node.ActionId}");
            return result;
        }

        internal void ValidateResources(GameDataService data)
        {
            void Require(bool exists, string field, uint id)
            {
                if (!exists)
                    throw Invalid(field, $"本机游戏数据中没有行号 {id}");
            }
            Require(data.GetSheet<TerritoryType>().HasRow((uint)TerritoryId), "territoryId", (uint)TerritoryId);
            if (Phase != PhaseData.TerritoryDefaults)
            {
                Require(data.GetSheet<Weather>().HasRow(Phase.Weather), "environment.weatherId", Phase.Weather);
                Require(data.GetSheet<BGM>().HasRow(Phase.BGM), "environment.bgmId", Phase.BGM);
            }
            for (var i = 0; i < Actors.Count; i++)
            {
                var spawn = Actors[i].Spawn;
                Require(data.GetSheet<BNpcBase>().HasRow(spawn.BNpcBaseId), $"actors[{i}].baseId", spawn.BNpcBaseId);
                Require(data.GetSheet<BNpcName>().HasRow(spawn.BNpcNameId), $"actors[{i}].nameId", spawn.BNpcNameId);
            }
            foreach (var node in AllEvents)
            {
                if (node.Op == "cast")
                    Require(data.GetSheet<Lumina.Excel.Sheets.Action>().HasRow(node.ActionId), node.Path + ".actionId", node.ActionId);
                if (node.Op == "status")
                    Require(data.GetSheet<Status>().HasRow(node.StatusId), node.Path + ".statusId", node.StatusId);
                if (node.Op == "object")
                    Require(data.GetSheet<EObj>().HasRow(node.ObjectId), node.Path + ".objectId", node.ObjectId);
                if (node.Op == "icon")
                    Require(data.GetSheet<Lockon>().HasRow(node.IconId), node.Path + ".iconId", node.IconId);
                if (node.Op == "tether" || node.Op == "trackTether")
                    Require(data.GetSheet<Channeling>().HasRow(node.TetherId), node.Path + ".tetherId", node.TetherId);
                if (node.On?.ActionId > 0)
                    Require(data.GetSheet<Lumina.Excel.Sheets.Action>().HasRow(node.On.ActionId), node.Path + ".event.actionId", node.On.ActionId);
                if (node.Op == "knockback")
                {
                    var rows = data.GetSheet<Knockback>();
                    if (!rows.TryGetRow(node.KnockbackId, out var row) || row.Distance == 0 || row.Speed == 0)
                        throw Invalid(node.Path + ".knockbackId", "需要带有效距离和速度的 Knockback 行");
                }
            }
            foreach (var condition in _conditions.Where(c => c.Type == "hasStatus"))
                Require(data.GetSheet<Status>().HasRow(condition.StatusId), condition.Path + ".statusId", condition.StatusId);
            foreach (var condition in _conditions.Where(c => c.Type == "objectActive"))
                Require(data.GetSheet<EObj>().HasRow(condition.ObjectId), condition.Path + ".objectId", condition.ObjectId);
        }

        private static void Fields(JsonElement value, string path, string required, string optional = "")
        {
            if (value.ValueKind != JsonValueKind.Object)
                throw Invalid(path, "需要 JSON 对象");
            var allowed = (required + " " + optional).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject())
                if (!allowed.Contains(field.Name) || !seen.Add(field.Name))
                    throw Invalid(path + "." + field.Name, "未知或重复的字段");
            foreach (var field in required.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (!seen.Contains(field))
                    throw Invalid(path + "." + field, "缺少必填字段");
        }

        private static JsonElement.ArrayEnumerator Array(JsonElement value, string path)
            => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : throw Invalid(path, "需要数组");

        private static Vector3 Coordinates(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
                throw Invalid(path, "需要三个场地坐标 [X, Y, Z]");
            return new Vector3((float)Number(value[0], path + "[0]", float.MaxValue),
                (float)Number(value[1], path + "[1]", float.MaxValue), (float)Number(value[2], path + "[2]", float.MaxValue));
        }

        private static string Text(JsonElement value, string key, string prefix = "")
        {
            var field = value.GetProperty(key);
            return field.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.GetString())
                ? field.GetString() : throw Invalid(prefix + key, "需要非空文本");
        }

        private static uint Integer(JsonElement value, string path, uint max, uint min = 0)
            => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var n) && n >= min && n <= max
                ? n : throw Invalid(path, $"需要 {min} 至 {max} 的整数");

        private static double Number(JsonElement value, string path, double max)
            => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && Math.Abs(n) <= max
                ? n : throw Invalid(path, "需要可表示的有限数值");

        private static InvalidDataException Invalid(string path, string message)
            => new InvalidDataException(path + "：" + message);
    }
}
