using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Simulant.Game.Data;

namespace Simulant.Content.Local
{
    internal sealed class LocalEvent
    {
        internal string Op, Actor, Message;
        internal string Path, CheckId;
        internal string Key, Target, By;
        internal int Rank = 1;
        internal float InterceptRadius, Interval;
        internal uint Damage;
        internal ushort AddStacks;
        internal LocalCondition Condition;
        internal LocalSignal On;
        internal double Timeout;
        internal LocalEvent[] Then, Else;
        internal double At;
        internal uint ActionId, TetherId, ObjectId, IconId;
        internal ushort MapSlot, MapFlags;
        internal float Percent;
        internal uint KnockbackId;
        internal ushort StatusId, Stacks;
        internal float Duration, Radius, Heading;
        internal bool Visible, Ground, Targetable = true;
        internal int Role = -1;
        internal int[] Slots;
        internal Vector2 Position;
        internal double CastSeconds(GameDataService data)
            => data.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(ActionId).Cast100ms / 10d;
    }

    internal sealed partial class LocalEncounter
    {
        internal static readonly string[] Roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

        private static Vector2 Point(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
                throw Invalid(path, "需要两个水平坐标 [X, Y]");
            return new Vector2((float)Number(value[0], path + "[0]", float.MaxValue),
                (float)Number(value[1], path + "[1]", float.MaxValue));
        }

        private static bool Boolean(JsonElement value, string path)
            => value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False
                ? value.GetBoolean() : throw Invalid(path, "需要布尔值");

        private static bool ChatText(string value) => !value.Any(char.IsControl) && value.IndexOfAny(new[] { '<', '>' }) < 0;

        private LocalEvent ReadEvent(JsonElement node, string path, HashSet<string> actors, uint version)
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("op", out _))
                throw Invalid(path + ".op", "缺少事件类型");
            var e = new LocalEvent { Op = Text(node, "op", path + "."), Path = path };
            if (version == 1 && e.Op != "cast")
                throw Invalid(path + ".op", "版本 1 仅支持 cast");
            void Read(string required = "", string optional = "") => Fields(node, path, ("at op " + required).TrimEnd(), optional);
            uint Id(string key, uint max) => Integer(node.GetProperty(key), path + "." + key, max, 1);
            float Positive(string key)
            {
                var n = (float)Number(node.GetProperty(key), path + "." + key, float.MaxValue);
                return n > 0 ? n : throw Invalid(path + "." + key, "需要正数");
            }
            int Slot(JsonElement value, string key)
            {
                return PartySlot(value, path + "." + key);
            }
            switch (e.Op)
            {
                case "cast":
                    Read("actor actionId", version >= 3 ? "targetRole groundAt target" : version == 2 ? "targetRole groundAt" : "");
                    e.ActionId = Id("actionId", uint.MaxValue);
                    if (node.TryGetProperty("groundAt", out var ground))
                    {
                        if (node.TryGetProperty("targetRole", out _) || node.TryGetProperty("target", out _))
                            throw Invalid(path + ".groundAt", "地面与单体目标只能选一种");
                        e.Ground = true;
                        if (ground.ValueKind != JsonValueKind.String || ground.GetString() != "self")
                            e.Role = Slot(ground, "groundAt");
                    }
                    break;
                case "visible":
                    Read("actor value", "targetable");
                    e.Visible = Boolean(node.GetProperty("value"), path + ".value");
                    if (node.TryGetProperty("targetable", out var targetable))
                        e.Targetable = Boolean(targetable, path + ".targetable");
                    break;
                case "place":
                    Read("actor position heading");
                    e.Heading = (float)Number(node.GetProperty("heading"), path + ".heading", float.MaxValue);
                    break;
                case "move":
                case "check":
                    Read(e.Op == "move" ? "roles position" : "roles position radius text", e.Op == "check" && version >= 3 ? "id" : "");
                    if (e.Op == "check")
                        e.Radius = Positive("radius");
                    break;
                case "branch" when version >= 3:
                case "trigger" when version >= 3:
                case "judge" when version >= 3:
                    ReadControl(e, node, actors, version);
                    break;
                case "health" when version >= 3:
                    Read("actor percent");
                    e.Percent = (float)Number(node.GetProperty("percent"), path + ".percent", 100);
                    if (e.Percent < 0) throw Invalid(path + ".percent", "百分比不能为负数");
                    break;
                case "mapEffect" when version >= 3:
                    Read("slot flags");
                    e.MapSlot = (ushort)Integer(node.GetProperty("slot"), path + ".slot", ushort.MaxValue);
                    e.MapFlags = (ushort)Integer(node.GetProperty("flags"), path + ".flags", ushort.MaxValue, 1);
                    if (e.MapSlot >= Phase.MapEffectFlags.Count || !Phase.MapEffectFlags[e.MapSlot].HasValue)
                        throw Invalid(path + ".slot", "请在 environment.mapEffectFlags 指定该槽的初始状态，用于停止后还原");
                    break;
                case "knockback" when version >= 3:
                    Read("roles knockbackId", "position heading");
                    if (node.TryGetProperty("position", out _) == node.TryGetProperty("heading", out _))
                        throw Invalid(path, "击退需要中心坐标 position 或方向 heading");
                    e.KnockbackId = Id("knockbackId", ushort.MaxValue);
                    e.Ground = node.TryGetProperty("position", out _);
                    if (!e.Ground) e.Heading = (float)Number(node.GetProperty("heading"), path + ".heading", float.MaxValue);
                    break;
                case "hint":
                    Read("text");
                    break;
                case "finish":
                    Read();
                    break;
                case "icon":
                    Read("targetRole iconId");
                    e.IconId = Id("iconId", uint.MaxValue);
                    break;
                case "object":
                    Read("actor objectId duration");
                    e.ObjectId = Id("objectId", uint.MaxValue);
                    e.Duration = Positive("duration");
                    break;
                case "tether":
                    Read("actor targetRole tetherId duration");
                    e.TetherId = Id("tetherId", ushort.MaxValue);
                    e.Duration = Positive("duration");
                    break;
                case "status":
                    Read("actor statusId duration", version >= 3 ? "stacks targetRole target addStacks" : "stacks targetRole");
                    e.StatusId = (ushort)Id("statusId", ushort.MaxValue);
                    e.Duration = (float)Number(node.GetProperty("duration"), path + ".duration", float.MaxValue);
                    if (e.Duration < 0)
                        throw Invalid(path + ".duration", "需要非负持续时间，0 表示移除");
                    if (node.TryGetProperty("stacks", out var stacks))
                        e.Stacks = (ushort)Integer(stacks, path + ".stacks", ushort.MaxValue);
                    if (node.TryGetProperty("addStacks", out var add))
                    {
                        if (e.Duration == 0 || node.TryGetProperty("stacks", out _)) throw Invalid(path + ".addStacks", "叠层需要正持续时间，不能同时指定 stacks");
                        e.AddStacks = (ushort)Integer(add, path + ".addStacks", ushort.MaxValue, 1);
                    }
                    break;
                case "selectTarget" when version >= 3:
                case "trackTether" when version >= 3:
                    Read("actor key by" + (e.Op == "trackTether" ? " tetherId" : ""),
                        e.Op == "trackTether" ? "rank roles interceptRadius initialRole" : "rank roles");
                    e.Key = Text(node, "key", path + ".");
                    if (!ChatText(e.Key)) throw Invalid(path + ".key", "目标名需要单行纯文本");
                    e.By = Text(node, "by", path + ".");
                    if (PartyPositions == null || !new[] { "nearest", "farthest", "enmity", "firstEnmity" }.Contains(e.By))
                        throw Invalid(path + ".by", "需要 party 和有效选人规则");
                    if (node.TryGetProperty("rank", out var rank))
                    {
                        if (e.By == "firstEnmity") throw Invalid(path + ".rank", "首次仇恨只有一个目标");
                        e.Rank = (int)Integer(rank, path + ".rank", 8, 1);
                    }
                    if (e.Op == "trackTether") e.TetherId = Id("tetherId", uint.MaxValue);
                    if (node.TryGetProperty("interceptRadius", out _)) e.InterceptRadius = Positive("interceptRadius");
                    if (node.TryGetProperty("initialRole", out var initial))
                    {
                        if (e.InterceptRadius == 0) throw Invalid(path + ".initialRole", "仅可为截线指定起始目标");
                        e.Role = Slot(initial, "initialRole");
                    }
                    break;
                case "resolveTether" when version >= 3:
                    Read("key");
                    e.Key = Text(node, "key", path + ".");
                    break;
                case "partyHit" when version >= 3:
                    Read("actor targetRole damage");
                    e.Damage = Id("damage", uint.MaxValue);
                    break;
                case "forParty" when version >= 3:
                    Read("then", "interval duration roles");
                    if (PartyPositions == null) throw Invalid(path, "forParty 需要 party");
                    if (node.TryGetProperty("interval", out _) != node.TryGetProperty("duration", out _))
                        throw Invalid(path, "重复检查需要 interval 和 duration");
                    if (node.TryGetProperty("interval", out _))
                    {
                        e.Interval = Positive("interval");
                        if (e.Interval < 0.1f) throw Invalid(path + ".interval", "最短间隔为 0.1 秒");
                        e.Duration = Positive("duration");
                    }
                    e.Then = ReadSteps(node.GetProperty("then"), path + ".then", actors, version);
                    break;
                default:
                    throw Invalid(path + ".op", "不支持的事件 " + e.Op);
            }
            e.At = Number(node.GetProperty("at"), path + ".at", double.MaxValue);
            if (e.At < 0)
                throw Invalid(path + ".at", "时间须为非负秒数");
            if (node.TryGetProperty("targetRole", out var target))
                e.Role = Slot(target, "targetRole");
            if (node.TryGetProperty("target", out _))
            {
                if (node.TryGetProperty("targetRole", out _)) throw Invalid(path + ".target", "请选择一个目标");
                e.Target = Text(node, "target", path + ".");
            }
            if (node.TryGetProperty("id", out _))
                e.CheckId = Text(node, "id", path + ".");
            if (node.TryGetProperty("actor", out _))
            {
                e.Actor = Text(node, "actor", path + ".");
                if (!actors.Contains(e.Actor))
                    throw Invalid(path + ".actor", "未声明角色 " + e.Actor);
            }
            if (node.TryGetProperty("position", out var point))
                e.Position = Point(point, path + ".position");
            if (node.TryGetProperty("roles", out var slots))
                e.Slots = RoleSlots(slots, path + ".roles");
            if (node.TryGetProperty("text", out _))
            {
                e.Message = Text(node, "text", path + ".");
                if (!ChatText(e.Message))
                    throw Invalid(path + ".text", "游戏内提示需要单行纯文本");
            }
            return e;
        }
    }
}
