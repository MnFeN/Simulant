using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal abstract class U7aEarlyExerciseLogic : U7aExerciseLogic
    {
        protected const ushort MagicVulnerability = 2941;
        protected const ushort DamageDown = 2911;

        protected override uint HelperNameId => 9707;

        protected U7aEarlyExerciseLogic(PluginHost host, SimPresetBase preset, int helperCount)
            : base(host, preset, helperCount)
        {
        }

        protected void SpawnMechanicActor(uint id, uint name, ushort model, uint layout, Vector2 position, float heading,
            Action<SimActor> ready, bool visibleInEnemyList = false)
            => EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                BNpcBaseId = id,
                BNpcNameId = name,
                ModelCharaId = model,
                LayoutId = layout,
                Pos = new Vector3(position, 0),
                Heading = heading,
                Level = 100,
                Targetable = false,
                EnemyListVisibility = visibleInEnemyList
            }, ready);

        protected static void CastFrom(SimActor actor, uint action, float duration, float? heading = null, Vector2? ground = null)
            => actor.Cast(action, target: ground.HasValue ? null : actor, castTime: duration, omenHeading: heading,
                omenPosition: ground.HasValue ? new Vector3(ground.Value, 0) : (Vector3?)null, executeOnFinish: false);

        protected static void Play(SimActor actor, uint timeline) => actor.ActorControl(ActorControlCategory.PlayActionTimeline, timeline);
        protected static void Voice(SimActor actor, uint voice) => actor.ActorControl(ActorControlCategory.PlayVoiceLine, voice);
        protected void BeforeOpening(double relativeTime, Action action, string name)
            => Scheduler.Schedule(Math.Max(Scheduler.ElapsedSeconds, Origin + relativeTime), action, name);

        protected void Yell(SimActor actor, uint voice, ushort name, ushort text)
        {
            Voice(actor, voice);
            LogNpcYell(actor.Id, name, text);
        }

        protected void RefreshStatus(int slot, ushort id, float duration, SimActor source, ushort param = 0)
        {
            ClearStatus(slot, id);
            EntityManager.SetStatus(Party[slot], id, duration, param, source);
        }

        protected void Vulnerability(MechanicResult result, SimActor source, float duration)
        {
            foreach (var player in result.Participants)
                RefreshStatus(player.Slot, MagicVulnerability, duration, source);
        }

        protected void ApplyDamageDown(IEnumerable<int> hit, SimActor source)
        {
            foreach (var slot in hit)
                RefreshStatus(slot, DamageDown, 180, source, 0xA6);
        }

        protected void SetTankTarget(SimActor boss)
        {
            EntityManager.SetPartyEnmity(boss, 100, 85, 20, 20, 25, 25, 25, 25);
            boss.Entity.SetNetworkTarget(Party[0].Entity);
        }

        protected AreaMechanic HitArea(string name, ActionArea area, Vector2 position, float heading, Action<MechanicResult> visual,
            int[] expected, MechanicFailureSeverity severity = MechanicFailureSeverity.Warning)
            => new AreaMechanic(name, area, position)
            {
                Heading = heading,
                RequiredPlayers = expected.Length,
                FailureSeverity = severity,
                Check = players => AreaMechanic.CheckPartyAssignment(players, expected),
                Visual = visual,
                Failed = result => Report(result.DescribeFailure(name, expected)
                    .WithAttribution(expected.Except(result.Participants.Select(p => p.Slot))
                        .Concat(result.Participants.Select(p => p.Slot).Except(expected))), result)
            };

        protected static int[] Nearest(PartySnapshot snapshot, Vector2 point, int count, Func<int, bool> eligible = null)
            => snapshot.Members.Where(p => eligible == null || eligible(p.Slot))
                .OrderBy(p => Vector2.DistanceSquared(p.Position, point)).ThenBy(p => p.Slot)
                .Take(count).Select(p => p.Slot).ToArray();
    }

    internal abstract class U7aP2ExerciseLogic : U7aEarlyExerciseLogic
    {
        protected static readonly int[] NearGroup = { 0, 1, 4, 5 };
        protected static readonly int[] FarGroup = { 2, 3, 6, 7 };
        private static readonly int[] Partners = { 4, 5, 6, 7, 0, 1, 2, 3 };
        private readonly Dictionary<int, (Vector2 Center, float Radius, double ActiveAt, HashSet<int> Hit)> _icePuddles
            = new Dictionary<int, (Vector2, float, double, HashSet<int>)>();
        private bool _watchingIcePuddles;
        protected static readonly ActionArea KickDonut = ActionArea.Donut(4, 20);
        protected override uint BossBaseId => 17823;
        protected override uint BossNameId => 12809;
        protected override uint BossLayoutId => 0xA55BF8;
        protected override ushort? BossModel => 4374;

        protected U7aP2ExerciseLogic(PluginHost host, SimPresetBase preset, int helperCount)
            : base(host, preset, helperCount)
        {
        }

        protected void AddAutos(SimTimeline timeline, params double[] times)
        {
            foreach (var time in times)
                timeline.At(time, AutoAttack, "冰巫女普攻");
        }

        protected void AutoAttack() => Oracle.ExecuteActionEffect(0x9CF0, Party[0], null, null, 1, 0.1f);

        protected void BeginIceBoss()
        {
            Oracle.ActorControl(ActorControlCategory.SetModelState, 4);
            SetTankTarget(Oracle);
        }

        protected void Kick(SimActor actor, uint action, ActionArea area)
        {
            Run(HitArea(action == 0x9D0A ? "斧踢" : "镰踢月环", area, actor.Pos, actor.Heading, result =>
                actor.ExecuteActionEffect(action, actor, result.Heading, result.HitActors, 8, 2.1f), Array.Empty<int>()));
        }

        protected IEnumerable<AreaMechanic> HouseOfLight(PartySnapshot snapshot, Vector2 source, int firstHelper, int[] expected,
            uint action = 0x9D0E, Action<MechanicResult, SimActor> onHit = null)
        {
            var targets = Nearest(snapshot, source, expected.Length);
            var area = ActionArea.Cone(60, 30);
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                var index = firstHelper + i;
                var heading = Heading(snapshot.Get(target).Position - source);
                var cone = HitArea("光之海啸扇形", area, source, heading, result =>
                {
                    var helper = Helper(index, source, heading);
                    helper.ExecuteActionEffect(action, helper, heading, result.HitActors, 1, 2.1f);
                    if (onHit == null)
                        Vulnerability(result, helper, 1.956f);
                    else
                        onHit(result, helper);
                }, expected.Contains(target) ? new[] { target } : Array.Empty<int>());
                cone.LockedSnapshot = snapshot;
                yield return cone;
            }
        }

        protected void GroundEffect(int helper, uint action, Vector2 position, IEnumerable<SimActor> hit = null, float duration = 2.1f)
            => Helper(helper, position).ExecuteActionEffectAt(action, new Vector3(position, -0.0153f), 0, duration, hit, 8);

        protected void AddBanish(SimTimeline timeline, bool spread, double castAt, double effectAt, double circlesAt,
            double? moveAt = null, int[] partners = null)
        {
            partners = partners ?? Partners;
            var action = spread ? 0x9D1Du : 0x9D1Cu;
            timeline.At(castAt, () =>
            {
                CastFrom(Oracle, action, 4.7f);
                // 两个读条同名。Action 各自引用一颗／四颗光球的咏唱特效，模拟读条需要显式播放。
                var vfx = GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(action).VFX.ValueNullable?.VFX.RowId ?? 0;
                if (vfx != 0)
                    Oracle.ActorControl(ActorControlCategory.TargetVFX, vfx, Oracle.Id, Oracle.Id, 4700);
                Voice(Oracle, 8205525);
            }, spread ? "放逐：分散读条" : "放逐：双人分摊读条");
            timeline.At(moveAt ?? castAt, () =>
                MoveAll(slot => Spot(Center + BanishOffset(spread || slot < 4 ? slot : partners[slot], spread), 0.25f),
                    circlesAt - 0.3, circlesAt + 0.2, urgent: true), "移动到放逐站位");
            timeline.At(effectAt, () => Oracle.ExecuteActionEffect(action, Oracle, null, null, 1, 3.1f), "放逐读条结算");
            timeline.At(circlesAt, () =>
            {
                var snapshot = PartySnapshot.Capture(Party);
                var targets = spread ? Enumerable.Range(0, 8) : Enumerable.Range(4, 4);
                var mechanics = targets.Select((target, helper) =>
                {
                    var position = snapshot.Get(target).Position;
                    var assignment = spread ? new[] { target } : new[] { target, partners[target] };
                    var hit = HitArea(spread ? "放逐分散" : "放逐双人分摊", ActionArea.Circle(5), position, 0, result =>
                    {
                        GroundEffect(helper, spread ? 0x9D1Fu : 0x9D1Eu, position, result.HitActors);
                        Vulnerability(result, Helpers[helper], 1.956f);
                    }, assignment);
                    hit.LockedSnapshot = snapshot;
                    return (SimMechanic)hit;
                });
                Run(mechanics);
            }, "放逐范围判定");
        }

        internal static Vector2 BanishOffset(int slot, bool spread)
        {
            // 北 MT，东北 D4，东 ST，东南 D2，南 H2，西南 D1，西 H1，西北 D3。
            var clock = new[] { 0, 2, 6, 4, 5, 3, 7, 1 };
            var direction = spread ? clock[slot] : 2 * (slot % 4);
            return Direction(Math.PI - direction * Math.PI / 4) * (spread ? 9.5f : 8);
        }

        protected void AddIcePuddle(int index, Vector2 position, float radius, double spawnAt, double fadeAt, double removeAt)
        {
            EventObject puddle = null;
            Scheduler.Schedule(Origin + spawnAt, () =>
            {
                puddle = EntityManager.SpawnEObj(new EventObjectSpawnSpec
                {
                    // 原生地景道具使用低位槽，冰圈从后段空闲区生成。
                    Index = (byte)(24 + index),
                    BaseId = 2014287,
                    EventId = 0x800375BF,
                    TargetableFlags = 5,
                    Pos = new Vector3(position, 0),
                    SharedGroupState = (uint)(0x500003 + index * 0x10000)
                });
                _icePuddles.Add(index, (position, radius, Now + 0.16, new HashSet<int>()));
                if (!_watchingIcePuddles)
                {
                    _watchingIcePuddles = true;
                    Scheduler.ScheduleAfter(0.12, WatchIcePuddles, "检查冰圈残留");
                }
            }, "生成冰圈残留");
            Scheduler.Schedule(Origin + fadeAt, () =>
            {
                _icePuddles.Remove(index);
                puddle?.SetEventState(7);
            }, "冰圈残留消退");
            Scheduler.Schedule(Origin + removeAt, () =>
            {
                if (puddle != null)
                    EntityManager.Delete(puddle);
            }, "回收冰圈残留");
        }

        private void WatchIcePuddles()
        {
            if (_icePuddles.Count == 0)
            {
                _watchingIcePuddles = false;
                return;
            }
            var snapshot = PartySnapshot.Capture(Party);
            foreach (var puddle in _icePuddles.Values)
            {
                if (Now < puddle.ActiveAt)
                    continue;
                var hit = snapshot.InArea(ActionArea.Circle(puddle.Radius), puddle.Center)
                    .Where(p => puddle.Hit.Add(p.Slot)).Select(p => p.Slot).ToArray();
                if (hit.Length != 0)
                    Report(MechanicError.Rule("冰圈残留", hit, "进入了仍然生效的残留范围。"));
            }
            Scheduler.ScheduleAfter(0.12, WatchIcePuddles, "检查冰圈残留");
        }
    }
}
