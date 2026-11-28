using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using ActionRow = Lumina.Excel.Sheets.Action;
using StatusRow = Lumina.Excel.Sheets.Status;

namespace Simulant.Simulation.Runtime
{
    /// <summary>把模拟事件转成 FF14 解析插件及 OverlayPlugin 共用的网络日志字段。</summary>
    internal sealed class SimCombatLog
    {
        internal const uint NoTarget = 0xE0000000;
        private static int _sequence;
        private readonly Func<bool> _enabled;
        private readonly Action<int, string, DateTimeOffset?> _write;
        private readonly Action<string> _warning;
        private int _writeFailed;

        internal SimCombatLog(Func<bool> enabled, Action<int, string, DateTimeOffset?> write, Action<string> warning)
        {
            _enabled = enabled;
            _write = write;
            _warning = warning;
        }

        internal bool Enabled => _enabled();

        internal void Party(IReadOnlyList<SimActor> members)
            => Record(time =>
            {
                if (members.Count == 0)
                    Write(11, time, $"0");
                else
                    Write(11, time, $"{members.Count}|{string.Join("|", members.Select(member => member.Id.ToString("X8", CultureInfo.InvariantCulture)))}");
            });

        /// <summary>14 行使用施法者的位置与面向，107 行使用此时锁定的技能中心与方向。</summary>
        internal void Cast(Character source, ActionRow action, Character target, float duration,
            Vector3? castOrigin, float? castHeading)
        {
            Record(time =>
            {
                var sourcePos = source.Pos3D;
                var sourceHeading = source.Heading;
                var origin = castOrigin ?? target?.Pos3D ?? sourcePos;
                var heading = castHeading ?? (target == null || target == source
                    ? sourceHeading : Direction(sourcePos, target.Pos3D, sourceHeading));
                var packetPosition = PacketPosition(origin, heading, 3);
                Write(20, time, $"{Actor(source.Id, source)}|{Skill(action.RowId, action)}|{Actor(target?.Id ?? NoTarget, target)}|{duration:F3}|{Position(sourcePos, sourceHeading)}");
                Write(263, time, $"{source.Id:X8}|{action.RowId:X4}|{packetPosition}");
            });
        }

        /// <summary>一次结算共用一个技能序号。15/16 依网络包的目标容量决定，实际命中人数单独记录。</summary>
        internal void Ability(Character source, uint actionId, Character animationTarget, Vector3? effectOrigin,
            float? effectHeading, float animationLock, IEnumerable<SimActor> hitTargets, int? packetTargetCapacity,
            ActionEffect[] effects = null)
        {
            Record(time =>
            {
                var actualHits = hitTargets == null
                    ? (animationTarget == null ? Array.Empty<Character>() : new[] { animationTarget })
                    : hitTargets.Where(target => target?.IsActive == true).Select(target => target.Entity).Distinct().ToArray();
                WriteAbility(time, source, actionId, animationTarget, effectOrigin, effectHeading, animationLock,
                    actualHits, packetTargetCapacity, hitTargets != null, _ => effects);
            });
        }

        internal void AbilityResults(Character source, uint actionId, Character animationTarget, Vector3? effectOrigin,
            float effectHeading, float animationLock, IReadOnlyList<(Character Target, List<ActionEffect> Effects)> hits)
            => Record(time => WriteAbility(time, source, actionId, animationTarget, effectOrigin, effectHeading,
                animationLock, hits.Select(hit => hit.Target).ToArray(), null, true, index => hits[index].Effects));

        private void WriteAbility(DateTimeOffset time, Character source, uint actionId, Character animationTarget,
            Vector3? effectOrigin, float? effectHeading, float animationLock, IReadOnlyList<Character> actualHits,
            int? packetTargetCapacity, bool hasHitList, Func<int, IReadOnlyList<ActionEffect>> effects)
        {
            var action = GameDataService.Instance.GetSheet<ActionRow>().GetRowOrDefault(actionId);
            var timeRotation = effectHeading ?? source.Heading;
            var rawRotation = PacketCodec.EncodeUShortHeading(timeRotation);
            var rotation = PacketCodec.DecodeUShortHeading(rawRotation);
            var sequence = unchecked((uint)Interlocked.Increment(ref _sequence));
            var count = actualHits.Count;
            // 解析插件对无目标结算始终写 15 行。手写结算没有包容量时沿用原来的行号规则。
            var lineType = count != 0 && (packetTargetCapacity ?? count) > 1 ? 22 : 21;
            var sourceFields = Vitals(source);
            var sourceAndSkill = $"{Actor(source.Id, source)}|{Skill(actionId, action)}";
            var area = effectOrigin.HasValue || count > 1 || (count == 0 && (hasHitList || action?.EffectRange > 0));
            var origin = effectOrigin ?? animationTarget?.Pos3D ?? source.Pos3D;

            var rows = new FormattableString[Math.Max(1, count)];
            for (var index = 0; index < rows.Length; index++)
            {
                var target = count == 0 ? null : actualHits[index];
                var effectFields = FormatAbilityEffects(count == 0 ? null : effects(index));
                rows[index] = $"{sourceAndSkill}|{Actor(target?.Id ?? NoTarget, target)}|{effectFields}|{Vitals(target)}|{sourceFields}|{sequence:X8}|{index}|{count}|00||01|{actionId:X2}|{actionId:X2}|{animationLock:F3}|{rawRotation:X4}";
            }

            var targetId = animationTarget?.Id ?? NoTarget;
            var extra = area
                ? $"1|{PacketPosition(origin, timeRotation, 3)}"
                : FormattableString.Invariant($"0||||{rotation:F3}");
            foreach (var row in rows)
                Write(lineType, time, row);

            Write(264, time, $"{source.Id:X8}|{actionId:X4}|{sequence:X8}|{extra}|{targetId:X8}");
        }

        private static string FormatAbilityEffects(IReadOnlyList<ActionEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return "0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0";

            // ACT 将每个原生效果槽拆成两个小端 uint，并以十六进制写入。
            var fields = new string[16];
            for (var slot = 0; slot < fields.Length / 2; slot++)
            {
                var effect = slot < effects.Count ? effects[slot] : default;
                var flags = effect.Type | (uint)effect.Param0 << 8 | (uint)effect.Param1 << 16 | (uint)effect.Param2 << 24;
                var value = effect.Param3 | (uint)effect.Param4 << 8 | (uint)effect.Value << 16;
                fields[slot * 2] = flags.ToString("X", CultureInfo.InvariantCulture);
                fields[slot * 2 + 1] = value.ToString("X", CultureInfo.InvariantCulture);
            }

            return string.Join("|", fields);
        }

        internal void Cancel(Character actor, uint actionId)
        {
            if (actor.ObjectKind != Game.FFCS.Client.Game.Object.ObjectKind.BattleNpc)
                return;
            Record(time =>
            {
                var action = GameDataService.Instance.GetSheet<ActionRow>().GetRowOrDefault(actionId);
                Write(23, time, $"{Actor(actor.Id, actor)}|{Skill(actionId, action)}|Interrupted");
            });
        }

        internal void PeriodicEffect(Character source, Character target, ushort status, uint amount, bool healing)
            => Record(time =>
            {
                var kind = healing ? "HoT" : "DoT";
                Write(24, time, $"{Actor(target.Id, target)}|{kind}|{status:X}|{amount:X}|{Vitals(target)}|{Actor(source?.Id ?? 0, source)}|0|{Vitals(source)}");
            });

        internal void HealthAndMana(Character actor)
            => Record(time => Write(39, time, $"{Actor(actor.Id, actor)}|{Vitals(actor)}"));

        internal void Status(bool adding, ushort id, float duration, ushort param, Character source, Character target)
        {
            Record(time =>
            {
                var name = GameDataService.Instance.GetSheet<StatusRow>().TryGetRow(id, out var status)
                    ? TextField(status.Name.ExtractText()) : FormattableString.Invariant($"Unknown_{id:X}");
                Write(adding ? 26 : 30, time, $"{id:X2}|{name}|{duration:F2}|{Actor(source.Id, source)}|{Actor(target.Id, target)}|{param:X2}|{target.MaxHP}|{(source.IsAlive ? source.MaxHP : 0)}");
            });
        }

        internal void Icon(uint targetId, Character target, uint icon, uint a2, uint a3, uint a4)
            => Record(time => Write(27, time, $"{Actor(targetId, target)}|0000|0000|{icon:X4}|{a2:X4}|{a3:X4}|{a4:X4}"));

        internal void Mark(bool add, int marker, Character source, uint targetId, Character target)
            => Record(time => Write(29, time, $"{(add ? "Add" : "Delete")}|{marker}|{Actor(source?.Id ?? NoTarget, source)}|{Actor(targetId, target)}"));

        internal void Targetable(uint id, Character actor, uint state)
            => Record(time =>
            {
                var entity = Actor(id, actor);
                Write(34, time, $"{entity}|{entity}|{(byte)state:X2}");
            });

        internal void Tether(uint sourceId, Character source, uint targetId, Character target, uint slot, uint tetherId, uint width)
            => Record(time => Write(35, time, $"{Actor(sourceId, source)}|{Actor(targetId, target)}|0000|{slot:X4}|{tetherId:X4}|{targetId:X8}|{width:X4}|0000"));

        internal void Director(uint instance, uint command, uint a, uint b, uint c, uint d)
        {
            // 解析插件的 ActorControlSelf 分支只记录同时带事件编号和命令高位的 DirectorUpdate。
            if ((command & 0xFFFF) != 0 && (command & 0xFF000000) != 0)
                Record(time => Write(33, time, $"{instance:X8}|{command:X8}|{a:X2}|{b:X2}|{c:X2}|{d:X2}"));
        }

        internal void LimitBreak(uint bars, uint units)
            => Record(time => Write(36, time, $"{(ushort)units:X4}|{(byte)bars}"));

        internal void MapEffect(uint instance, uint slot, ushort previous, ushort next)
            => Record(time => Write(257, time, $"{instance:X8}|{next:X4}{previous:X4}|{slot:X2}||"));

        internal void NpcYell(uint actor, ushort name, ushort yell)
            => Record(time => Write(266, time, $"{actor:X8}|{name:X4}|{yell:X4}"));

        internal void BattleTalk(uint actor, uint instance, ushort name, ushort text, uint milliseconds,
            uint a1, uint a2, uint a3, uint a4)
            => Record(time => Write(267, time, $"{actor:X8}|{instance:X8}|{name:X4}|{text:X4}|{milliseconds}|{a1:X}|{a2:X}|{a3:X}|{a4:X}"));

        internal void Move(uint actor, Vector3 position, float heading, byte speed, ActorMoveFlags flags)
        {
            // OverlayPlugin 的 10E 只记录 NPC；10F 没有此过滤。
            if (Enabled && actor >= 0x40000000 && actor != NoTarget)
                Record(time => Write(270, time, $"{actor:X8}|{PacketPositionFields(position, heading, (ushort)flags, speed, true)}"));
        }

        internal void SetPosition(uint actor, Vector3 position, float heading)
            => Record(time => Write(271, time, $"{actor:X8}|{PacketPositionFields(position, heading, 0, 0, false)}"));

        internal void SpawnNpc(uint actor, uint parent, ushort tether, byte animation)
            => Record(time => Write(272, time, $"{actor:X8}|{parent:X8}|{tether:X4}|{animation:X2}"));

        internal void ActorControl(uint actor, ActorControlCategory category, uint a1, uint a2, uint a3, uint a4)
        {
            // 与 OverlayPlugin 的 LineActorControlExtra 白名单同步。0x31/0xB8 是 Machina 的 VfxUnknown49/184。
            switch (category)
            {
                case ActorControlCategory.SetAnimationState:
                case ActorControlCategory.ShowContentBattleTalk:
                case ActorControlCategory.SetModelAttributes:
                case ActorControlCategory.SetModelState:
                case ActorControlCategory.TargetVFX:
                case ActorControlCategory.PlayActionTimeline:
                case ActorControlCategory.EObjAnimation:
                    Record(time => Write(273, time, $"{actor:X8}|{(ushort)category:X4}|{a1:X}|{a2:X}|{a3:X}|{a4:X}"));
                    break;
            }
        }

        internal void ActorControlSelf(uint actor, ActorControlCategory category, uint a1, uint a2, uint a3,
            uint a4, uint a5, uint a6)
        {
            switch (category)
            {
                case ActorControlCategory.DirectorUpdate:
                    Director(a1, a2, a3, a4, a5, a6);
                    break;
                case ActorControlCategory.LimitBreakGauge:
                    LimitBreak(a1, a2);
                    break;
                // DisplayLogMessage / DisplayLogMessageParams in Machina.
                case ActorControlCategory.ShowSourceTargetMessage:
                case ActorControlCategory.ShowLocalSourceTargetMessage:
                    Record(time => Write(274, time, $"{actor:X8}|{(ushort)category:X4}|{a1:X}|{a2:X}|{a3:X}|{a4:X}|{a5:X}|{a6:X}"));
                    break;
            }
        }

        private void Record(Action<DateTimeOffset> record)
        {
            if (!Enabled)
                return;

            try
            {
                record(DateTimeOffset.Now);
                Interlocked.Exchange(ref _writeFailed, 0);
            }
            catch (Exception ex)
            {
                // 日志是可选副作用。插件重启或实体失效时照常运行模拟，重复失败只提醒一次。
                if (Interlocked.Exchange(ref _writeFailed, 1) == 0)
                {
                    try { _warning?.Invoke("生成 ACT 日志失败：" + ex.Message); }
                    catch { /* 记录失败不能中断模拟。 */ }
                }
            }
        }

        private void Write(int id, DateTimeOffset time, FormattableString payload)
            => _write(id, FormattableString.Invariant(payload), time);

        private static string Actor(uint id, Character character)
        {
            var name = string.Empty;
            if (character?.IsAlive == true)
            {
                var bytes = character.ReadNameBytes();
                name = TextField(Encoding.UTF8.GetString(bytes, 0, Array.IndexOf(bytes, (byte)0)));
            }
            return FormattableString.Invariant($"{id:X8}|{name}");
        }

        private static string Skill(uint id, ActionRow? action)
        {
            var name = action.HasValue ? TextField(action.Value.Name.ExtractText()) : FormattableString.Invariant($"Unknown_{id:X}");
            return FormattableString.Invariant($"{id:X2}|{name}");
        }

        private static string TextField(string text)
            => (text ?? string.Empty).Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ');

        private static string Vitals(Character actor)
            => actor == null ? "|||||||||" :
                FormattableString.Invariant($"{actor.HP}|{actor.MaxHP}|{actor.MP}|{actor.MaxMp}|||{Position(actor.Pos3D, actor.Heading)}");

        private static string Position(Vector3 point, float heading)
            => FormattableString.Invariant($"{point.X:0.00}|{point.Y:0.00}|{point.Z:0.00}|{heading:0.00}");

        private static float Direction(Vector3 from, Vector3 to, float fallback)
            => from.X == to.X && from.Y == to.Y ? fallback : (float)Math.Atan2(to.X - from.X, to.Y - from.Y);

        private static string PacketPosition(Vector3 position, float heading, int decimals)
        {
            var x = PacketCodec.DecodeUShortCoord(PacketCodec.EncodeUShortCoord(position.X));
            var y = PacketCodec.DecodeUShortCoord(PacketCodec.EncodeUShortCoord(position.Y));
            var z = PacketCodec.DecodeUShortCoord(PacketCodec.EncodeUShortCoord(position.Z));
            var h = PacketCodec.DecodeUShortHeading(PacketCodec.EncodeUShortHeading(heading));
            return decimals == 3 ? FormattableString.Invariant($"{x:F3}|{y:F3}|{z:F3}|{h:F3}") :
                FormattableString.Invariant($"{x:F4}|{y:F4}|{z:F4}");
        }

        private static string PacketPositionFields(Vector3 position, float heading, ushort flags, byte speed, bool move)
        {
            var h = PacketCodec.DecodeUShortHeading(PacketCodec.EncodeUShortHeading(heading));
            return move ? FormattableString.Invariant($"{h:F4}|{flags:X4}|{speed:X4}|{PacketPosition(position, heading, 4)}") :
                FormattableString.Invariant($"{h:F4}|00|00|{position.X:F4}|{position.Y:F4}|{position.Z:F4}");
        }
    }
}
