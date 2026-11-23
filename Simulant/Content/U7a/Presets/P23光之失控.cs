using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(2.3)]
    internal sealed class P23光之失控 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P2-3 光之失控";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 22);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(LightRampantLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "暂未完成。\n\n灰九解法。六人按连线顺序踩 C1243A，无连线两人从正东西放光锤。四人分摊后躲两轮光球，2 层光踩塔，再处理放逐和光之波动。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase> { U7aExerciseOptions.Player() };
    }

    internal sealed class LightRampantLogic : U7aP2ExerciseLogic
    {
        private const ushort Lightsteeped = 2257;
        private const ushort Chains = 4157;
        private const ushort Curse = 4158;
        private const ushort Weight = 4159;
        private const double FirstTowerHit = 16.154;
        private const double CenterTowerHit = 30.906;
        private const double CurseAt = 15.797;
        private const double LinksEnd = 24.821;
        // 实战包没有距离字段；沿灰九路径留足间距，近身错误按此距离判定。
        private const float ChainBreakDistance = 12;
        private static readonly int[] Partners = { 6, 5, 4, 7, 2, 1, 0, 3 };
        private static readonly double[] HammerTimes = { 12.859, 14.453, 15.970, 17.529, 19.086 };
        private static readonly double[] PuddleSpawns = { 13.755, 15.402, 16.956, 18.555, 19.783 };
        private static readonly double[] PuddleFades = { 23.527, 25.124, 26.593, 28.154, 29.738 };
        private static readonly double[] PuddleRemovals = { 26.546, 28.154, 29.738, 31.347, 32.943 };
        private static readonly ActionArea TowerArea = ActionArea.Circle(4);
        private readonly SimActor[] _orbs = new SimActor[6];
        private readonly int[] _stacks = new int[8];
        private readonly Dictionary<uint, int> _towerCounts = new Dictionary<uint, int>();
        private P23光之失控Plan _plan;
        private bool _spread;
        private bool _chainsVisible;
        private int _chainWorld;

        protected override double OpeningLeadIn => 1.19;

        public LightRampantLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 8)
        {
        }

        protected override void PreparePlan()
        {
            _plan = new P23光之失控Plan(Random);
            Array.Copy(_plan.InitialStacks, _stacks, 8);
            _spread = Random.Next(2) == 0;
        }

        protected override SimPosition InitialPartyPosition(int slot) => Spot(Center + BanishOffset(slot, true) * 0.8f, 0.15f);

        protected override void SchedulePrelude()
        {
            BeforeOpening(-1.19, BeginIceBoss, "冰巫女仇恨及初始形态");
            BeforeOpening(-1.182, () => Oracle.ExecuteActionEffectAt(0x9CEF, new Vector3(Center, -0.0153f), (float)Math.PI, 1.6f), "冰巫女回到场中");
            BeforeOpening(-0.238, AutoAttack, "冰巫女普攻");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            AddAutos(timeline, 7.769, 37.136);
            timeline.At(0, () =>
            {
                CastFrom(Oracle, 0x9D14, 4.7f);
                Voice(Oracle, 8205524);
            }, "光之失控读条及语音");
            timeline.At(4.971, () => Oracle.ExecuteActionEffect(0x9D14, Oracle, null, Party, 8, 3.1f), "光之失控全屏伤害");
            timeline.At(5.825, AssignLight, "连线、光之锤、光之重负与初始层数");
            timeline.At(8.045, OpenOuterTowers, "外圈六塔出现");
            timeline.At(8.124, () => Play(Oracle, 4581), "冰巫女隐去");
            for (var i = 0; i < HammerTimes.Length; i++)
            {
                var wave = i;
                timeline.At(HammerTimes[wave], () => ResolveHammer(wave), "光锤点名及冰圈落点");
            }
            timeline.At(CurseAt, ActivateCurse, "连线转为无尽光之束缚");
            timeline.At(16.064, () =>
            {
                for (var tower = 0; tower < 6; tower++)
                    Map(TowerMap(tower), 8, 4);
            }, "关闭六座外圈塔");
            timeline.At(FirstTowerHit, ResolveOuterTowers, "外圈六塔结算");
            timeline.At(16.549, () => SpawnOrbs(_plan.FirstOrbParity), "第一批三颗光球出现");
            timeline.At(18.25, () => MoveLinkedToStacks(21.55), "连线组沿场边靠向分摊点");
            timeline.At(19.353, () =>
            {
                CastOrbs(_plan.FirstOrbParity);
                SpawnOrbs(1 - _plan.FirstOrbParity);
            }, "第一批光球咏唱，第二批出现");
            timeline.At(21.883, ResolvePowerfulLight, "两组四人光之重负分摊");
            timeline.At(22.398, () => CastOrbs(1 - _plan.FirstOrbParity), "第二批光球咏唱");
            timeline.At(22.829, () =>
            {
                Map(21, 2, 1);
                Map(22, 1, 4);
                WatchTower(21, Center, 30.824);
            }, "中央四人塔出现");
            timeline.At(24.327, () => ResolveOrbs(_plan.FirstOrbParity), "第一批光球爆炸");
            timeline.At(24.51, () => MoveAll(slot => Spot(AbsoluteOrbPoint(slot, true), 0.24f), 26.8, 27.35, urgent: true), "移动至第一批光球的安全区");
            timeline.At(LinksEnd, CancelChains, "诅咒连线消失");
            timeline.At(26.546, () => DeleteOrbs(_plan.FirstOrbParity), "回收第一批光球");
            timeline.At(26.898, () => Play(Oracle, 7747), "冰巫女重新出现");
            timeline.At(27.341, () => ResolveOrbs(1 - _plan.FirstOrbParity), "第二批光球爆炸");
            timeline.At(27.38, MoveToCenterTower, "按实际层数进中央塔或在外待命");
            timeline.At(29.738, () => DeleteOrbs(1 - _plan.FirstOrbParity), "回收第二批光球");
            timeline.At(30.824, () => Map(21, 8, 4), "关闭中央塔");
            timeline.At(CenterTowerHit, ResolveCenterTower, "中央四人塔结算");
            AddBanish(timeline, _spread, castAt: 29.116, effectAt: 34.093, circlesAt: 34.154,
                moveAt: 30.97, partners: Partners);
            timeline.At(35.0, () => MoveAll(slot => Spot(Center + BanishOffset(slot, true), 0.24f), 40, 43.3), "八方分散准备光之波动");
            timeline.At(37.13, () => Oracle.SetTargetable(true), "冰巫女恢复可选中");
            timeline.At(37.223, () => CastFrom(Oracle, 0x9CFD, 4.7f), "光之波动读条");
            timeline.At(42.208, () => Oracle.ExecuteActionEffect(0x9CFD, Oracle, null, null, 1, 3.1f), "光之海啸读条结算");
            timeline.At(43.119, () =>
                Run(HouseOfLight(PartySnapshot.Capture(Party), Center, 0, P23光之失控Plan.Clock, 0x9CFC, GrantStacks)),
                "最后八道光之波动及光层");
            timeline.At(44.5, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    ClearStatus(slot, Lightsteeped);
                Complete();
            }, "光之失控结束");
            return timeline;
        }

        private void AssignLight()
        {
            _chainsVisible = true;
            _chainWorld = _host.WorldGeneration;
            for (var i = 0; i < _plan.Chain.Length; i++)
            {
                var slot = _plan.Chain[i];
                Party[slot].TetherTo(Party[_plan.Chain[(i + 1) % 6]], 110);
                Status(slot, Chains, 10);
            }
            for (var slot = 0; slot < 8; slot++)
                if (_stacks[slot] != 0)
                    Status(slot, Lightsteeped, 36, (ushort)_stacks[slot]);
            foreach (var target in _plan.Weights)
                Status(target, Weight, 16);
            for (var side = 0; side < 2; side++)
            {
                Icon(_plan.Baits[side], 375);
                Move(_plan.Baits[side], Spot(Center + _plan.HammerOffset(side, 0), 0.13f), 12.2, HammerTimes[0]);
            }
            WatchChains();
        }

        private static uint TowerMap(int tower) => (uint)(9 + (tower + 5) % 6);
        private static Vector2 TowerPosition(int tower) => Center + P23光之失控Plan.TowerOffset(tower);

        private void OpenOuterTowers()
        {
            Oracle.SetTargetable(false);
            for (var tower = 0; tower < 6; tower++)
            {
                var map = TowerMap(tower);
                var center = TowerPosition(tower);
                Map(map, 2, 1);
                Map(map + 6, 1, 4);
                Move(_plan.TowerPlayers[tower], Spot(Center + P23光之失控Plan.TowerOffset(tower, 18.3f), 0.16f), 10.7, FirstTowerHit);
                WatchTower(map, center, 16.064);
            }
        }

        private void WatchTower(uint map, Vector2 position, double until)
        {
            if (Now >= until)
                return;
            var count = Math.Min(4, PartySnapshot.Capture(Party).InArea(TowerArea, position).Count);
            if (_towerCounts.TryGetValue(map, out var previous) ? count != previous : count > 0)
            {
                var low = count == 0 ? 1 : 1 << (count * 2 + 2);
                Map(map, (ushort)(low * 2), (ushort)low);
            }
            _towerCounts[map] = count;
            Scheduler.ScheduleAfter(0.12, () => WatchTower(map, position, until), "刷新塔内人数");
        }

        private void ResolveHammer(int wave)
        {
            var snapshot = PartySnapshot.Capture(Party);
            Run(_plan.Baits.Select((slot, side) =>
            {
                var position = snapshot.Get(slot).Position;
                var mechanic = HitArea("光锤引导", ActionArea.Circle(6), position, 0, result =>
                {
                    var helper = Helper(side + 6, position);
                    helper.ExecuteActionEffect(0x9D1A, Party[slot], 0, result.HitActors, 8, 1.6f);
                    Vulnerability(result, helper, 0.956f);
                    AddIcePuddle(wave * 2 + side, position, 6, PuddleSpawns[wave], PuddleFades[wave], PuddleRemovals[wave]);
                }, new[] { slot });
                mechanic.LockedSnapshot = snapshot;
                return (SimMechanic)mechanic;
            }));
            for (var side = 0; side < 2; side++)
            {
                var position = wave == 4 ? AbsoluteOrbPoint(_plan.Baits[side], false) : Center + _plan.HammerOffset(side, wave + 1);
                var next = wave == 4 ? 21.883 : HammerTimes[wave + 1];
                Move(_plan.Baits[side], Spot(position, 0.14f), PuddleSpawns[wave], next, urgent: true);
            }
        }

        private void ActivateCurse()
        {
            if (!_chainsVisible)
                return;
            for (var i = 0; i < _plan.Chain.Length; i++)
            {
                var slot = _plan.Chain[i];
                ClearStatus(slot, Chains);
                Status(slot, Curse, 9);
                Party[slot].TetherTo(Party[_plan.Chain[(i + 1) % 6]], 111);
            }
        }

        private void WatchChains()
        {
            if (Now >= LinksEnd || !_chainsVisible)
                return;
            var snapshot = PartySnapshot.Capture(Party);
            for (var i = 0; i < _plan.Chain.Length; i++)
            {
                var first = _plan.Chain[i];
                var second = _plan.Chain[(i + 1) % 6];
                var dead = Party[first].Entity.HP == 0 || Party[second].Entity.HP == 0;
                if (!dead && (Now < CurseAt || Vector2.DistanceSquared(snapshot.Get(first).Position, snapshot.Get(second).Position)
                    >= ChainBreakDistance * ChainBreakDistance))
                    continue;
                GroundEffect(6, 0x9D17, snapshot.Get(first).Position, Party);
                ApplyDamageDown(P23光之失控Plan.Clock, Helpers[6]);
                CancelChains();
                Report(MechanicError.Rule("无尽光连线", new[] { first, second },
                    dead ? "连线玩家倒下，触发光之束缚。" : "两名相连玩家距离过近，触发光之束缚。"));
                return;
            }
            Scheduler.ScheduleAfter(0.15, WatchChains, "检查永恒光连线距离");
        }

        private void CancelChains()
        {
            if (!_chainsVisible || _chainWorld != _host.WorldGeneration)
                return;
            foreach (var slot in _plan.Chain)
            {
                if (Party[slot].IsActive)
                    Party[slot].CancelTether();
                ClearStatus(slot, Chains);
                ClearStatus(slot, Curse);
            }
            _chainsVisible = false;
        }

        private void ResolveOuterTowers()
        {
            Run(Enumerable.Range(0, 6).Select(tower =>
                ResolveTower(tower, TowerPosition(tower), new[] { _plan.TowerPlayers[tower] })));
            for (var tower = 0; tower < 6; tower++)
                Move(_plan.TowerPlayers[tower], Spot(Center + _plan.EdgeWaypoint(tower), 0.2f), 18.18, 18.25, urgent: true);
        }

        private AreaMechanic ResolveTower(int helperIndex, Vector2 center, int[] expected)
            => HitArea(expected.Length == 1 ? "外圈光塔" : "中央四人塔", TowerArea, center, 0, result =>
            {
                GroundEffect(helperIndex, 0x9D15, center, result.HitActors);
                if (result.Participants.Count < expected.Length)
                {
                    GroundEffect(helperIndex, 0x9D16, center, Party);
                    ApplyDamageDown(P23光之失控Plan.Clock, Helpers[helperIndex]);
                }
                GrantStacks(result, Helpers[helperIndex]);
            }, expected);

        private void GrantStacks(MechanicResult result, SimActor source)
        {
            var overflow = false;
            foreach (var player in result.Participants)
            {
                if (_stacks[player.Slot] >= 5)
                    continue;
                var stacks = ++_stacks[player.Slot];
                RefreshStatus(player.Slot, Lightsteeped, 36, source, (ushort)stacks);
                if (stacks == 5)
                {
                    overflow = true;
                    Report(MechanicError.Rule("光之过剩", new[] { player.Slot }, "光层叠到了五层。"));
                }
            }
            if (overflow)
            {
                source.ExecuteActionEffect(0x9D18, source, null, Party, 8, 2.1f);
                ApplyDamageDown(P23光之失控Plan.Clock, source);
            }
        }

        private Vector2 AbsoluteOrbPoint(int slot, bool afterFirst)
            => Center + _plan.OrbSafeOffset(_plan.Side(slot), afterFirst);

        private void MoveLinkedToStacks(double arrive)
        {
            foreach (var slot in _plan.Chain)
                Move(slot, Spot(AbsoluteOrbPoint(slot, false), 0.22f), arrive, 24.49, urgent: true);
        }

        private void ResolvePowerfulLight()
        {
            var snapshot = PartySnapshot.Capture(Party);
            Run(_plan.Weights.Select((slot, side) =>
            {
                var position = snapshot.Get(slot).Position;
                var expected = Enumerable.Range(0, 8).Where(player => _plan.Side(player) == side).ToArray();
                var mechanic = HitArea("光之重负四人分摊", ActionArea.Circle(5), position, 0, result =>
                {
                    var helper = Helper(side + 6, position);
                    helper.ExecuteActionEffect(0x9D19, Party[slot], 0, result.HitActors, 8, 2.1f);
                    EntityManager.MarkUnderfilledStack(result.HitActors.ToArray(), 4, Oracle);
                    ClearStatus(slot, Weight);
                    GrantStacks(result, helper);
                }, expected);
                mechanic.LockedSnapshot = snapshot;
                return (SimMechanic)mechanic;
            }));
        }

        private void SpawnOrbs(int parity)
        {
            for (var tower = parity; tower < 6; tower += 2)
            {
                var index = tower;
                var point = TowerPosition(index);
                SpawnMechanicActor(17826, 9318, 4355, 0xA55BFA, point, 0, actor => _orbs[index] = actor, visibleInEnemyList: true);
            }
        }

        private void CastOrbs(int parity)
        {
            for (var tower = parity; tower < 6; tower += 2)
                CastFrom(_orbs[tower], 0x9D1B, 4.7f);
        }

        private void ResolveOrbs(int parity)
        {
            Run(Enumerable.Range(0, 6).Where(tower => tower % 2 == parity).Select(tower =>
            {
                var orb = _orbs[tower];
                return HitArea("光球爆炸", ActionArea.Circle(11), TowerPosition(tower), 0, result =>
                    orb.ExecuteActionEffect(0x9D1B, orb, 0, result.HitActors, 8, 2.1f), Array.Empty<int>());
            }));
        }

        private void DeleteOrbs(int parity)
        {
            for (var tower = parity; tower < 6; tower += 2)
                EntityManager.Delete(_orbs[tower]);
        }

        private void MoveToCenterTower() => MoveAll(slot => _stacks[slot] == 2
            ? Spot(Center + BanishOffset(slot, true) * 0.17f, 0.18f)
            : Spot(Center + new Vector2(_plan.Side(slot) == 0 ? -6 : 6, (slot % 4 - 1.5f) * 0.8f), 0.22f),
            30.55, CenterTowerHit + 0.04, urgent: true);

        private void ResolveCenterTower() => Run(ResolveTower(7, Center, _plan.CenterPlayers));

        protected override void OnStop()
        {
            try
            {
                CancelChains();
            }
            finally
            {
                base.OnStop();
            }
        }
    }
}
