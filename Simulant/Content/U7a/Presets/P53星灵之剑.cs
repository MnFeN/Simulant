using Simulant.Core;
using Simulant.Core.Environment;
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
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(5.3)]
    internal sealed class P53星灵之剑 : SimPresetBase
    {
        private const PolarizingOrder DefaultOrder = PolarizingOrder.TMRH;

        public override int TerritoryId => 1238;
        public override string Name => "P5-3 星灵之剑";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 23);
        public override PhaseData Phase => U7a.P5;
        public override int Level => 100;
        public override Type SimLogicType => typeof(PolarizingStrikesLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "星灵之剑的四轮左右分摊。按所选顺序挡枪，回中躲开光暗余波，挡枪者受到耐性降低需要换边。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            U7aExerciseOptions.Direction(nameof(PolarizingStrikesLogic.Facing), "潘多拉朝向"),
            OrderOption()
        };

        private static SimOptionBase OrderOption()
            => new ComboBoxOption<PolarizingOrder>(nameof(PolarizingStrikesLogic.Order), "挡枪职能顺序", DefaultOrder, new Map<PolarizingOrder>
            {
                [PolarizingOrder.TMRH] = "T → 近 → 远 → H",
                [PolarizingOrder.THMR] = "T → H → 近 → 远",
                [PolarizingOrder.TMRT] = "T → 近 → 远 → T"
            });

        internal static PolarizingOrder ConfiguredOrder(Configuration configuration)
        {
            var saved = configuration?.Presets.Find(preset => preset.Id == typeof(P53星灵之剑).FullName);
            var value = saved?.Options.Find(option => option.Name == nameof(PolarizingStrikesLogic.Order))?.Value;
            return Enum.TryParse(value, out PolarizingOrder order) && Enum.IsDefined(typeof(PolarizingOrder), order) ? order : DefaultOrder;
        }
    }

    internal sealed class PolarizingStrikesLogic : U7aP5ExerciseLogic
    {
        [SimOption] public ExerciseDirection Facing { get; set; }
        [SimOption] public PolarizingOrder Order { get; set; }

        private PolarizingStrikesRound _round;

        public PolarizingStrikesLogic(PluginHost host, SimPresetBase preset) : base(host, preset, FulgentBladeHelper)
        {
        }

        protected override void PreparePlan()
            => _round = new PolarizingStrikesRound(this, 0, 1, Order, (float)(Math.PI + Deg((int)Facing * 45)));

        protected override SimPosition InitialPartyPosition(int slot) => _round.InitialPartyPosition(slot);

        protected override SimTimeline BuildExercise()
        {
            var timeline = _round.Build();
            timeline.At(27.1, Complete, "星灵之剑结束");
            return timeline;
        }
    }

    internal abstract partial class U7aP5ExerciseLogic
    {
        protected sealed class PolarizingStrikesRound
        {
            private static readonly double[] BossCasts = { 0, 8.665, 13.340, 17.954 };
            private static readonly double[] BossHits = { 6.506, 11.160, 15.829, 20.430 };
            private static readonly double[] BeamCasts = { 6.568, 11.281, 15.878, 20.535 };
            private static readonly double[] BeamHits = { 7.078, 11.745, 16.374, 21.057 };
            private static readonly double[] Aftershocks = { 9.234, 13.824, 18.526, 23.102 };
            private static readonly uint[] BeamActions = { 0x9D7D, 0x9D7E };
            private static readonly uint[] AftershockActions = { 0x9CB7, 0x9CB8 };
            private static readonly ushort[] ResistanceDown = { 4164, 3323 };
            private static readonly string[] Colors = { "光", "暗" };

            private readonly U7aP5ExerciseLogic _fight;
            private readonly double _offset;
            private readonly int _occurrence;
            private readonly float? _startFacing;
            private readonly PolarizingOrder _order;
            private P53星灵之剑Plan _plan;
            private float _facing;
            private readonly ActionArea _beamArea;
            private readonly int?[] _bait = new int?[2];
            private readonly float[] _beamHeading = new float[2];
            private readonly double[,] _resistanceUntil = new double[2, 8];

            internal PolarizingStrikesRound(U7aP5ExerciseLogic fight, double offset, int occurrence, PolarizingOrder order, float? facing = null)
            {
                _fight = fight;
                _offset = offset;
                _occurrence = occurrence;
                _order = order;
                _startFacing = facing;
                _beamArea = ActionArea.FromAction(BeamActions[0]);
                if (facing.HasValue)
                    _plan = new P53星灵之剑Plan(Center, facing.Value, order);
            }

            internal SimPosition InitialPartyPosition(int slot) => _plan.Stack(0, slot);

            internal SimTimeline Build()
            {
                var timeline = new SimTimeline();
                timeline.At(0, () =>
                {
                    _facing = _startFacing ?? _fight.BossHeading;
                    _fight.LockBossFacing();
                    if (_startFacing.HasValue)
                    {
                        _fight.FaceBoss(_facing);
                        GameMarkers.PlaceWaymarks(WaymarkLayout.Center(100, 100).Rotate(_facing)
                            .At("A", -5, 5).At("1", -7, 7).At("3", -9, 9)
                            .At("B", 5, 5).At("2", 7, 7).At("4", 9, 9)
                            .At("C", 0, 8).At("D", 0, 10.5));
                    }
                    else
                        _plan = new P53星灵之剑Plan(Center, _facing, _order);
                    MoveAll(slot => _plan.Stack(0, slot), BeamCasts[0] - 0.35, BeamHits[0]);
                }, "就位并放置挡枪标点");

                for (var round = 0; round < BeamHits.Length; round++)
                    AddRound(timeline, round);

                if (_occurrence == 1)
                {
                    _fight.AddDialogue(timeline, 19.279, 41, GaiaNameId, "盖娅的星灵之剑台词");
                    _fight.AddDialogue(timeline, 24.286, 42, RyneNameId, "琳的星灵之剑台词");
                    _fight.AddDoubleAuto(timeline, 25.784, 26.463);
                }
                _fight.AddDoubleAuto(timeline, 22.599, 23.292);
                timeline.At(22.78, () => _fight.Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly, "星灵之剑结束后跟随一仇转向");
                return timeline;
            }

            private void AddRound(SimTimeline timeline, int round)
            {
                var action = round == 0 ? 0x9D7Cu : 0x9D2Au;
                timeline.At(BossCasts[round], () => _fight.CastBoss(action, round == 0 ? 6.2f : 2.2f), $"第 {round + 1} 轮星灵之剑读条");
                timeline.At(BossHits[round], () =>
                {
                    _fight.BossEffect(action, 2.1f);
                    var snapshot = PartySnapshot.Capture(_fight.Party);
                    for (var side = 0; side < BeamActions.Length; side++)
                        _fight.Helper(HelperIndex(round, side), Center, SelectBeam(snapshot, side));
                }, $"第 {round + 1} 轮星灵之剑结算");
                timeline.At(BeamCasts[round], () => CastBeams(round), $"第 {round + 1} 轮光暗分摊锁定及读条");
                timeline.At(BeamHits[round], () =>
                {
                    ResolveBeams(round);
                    MoveAll(_plan.Dodge, Aftershocks[round] - 0.25, Aftershocks[round], true);
                }, $"第 {round + 1} 轮光暗分摊结算");
                timeline.At(Aftershocks[round], () =>
                {
                    ResolveAftershocks(round);
                    if (round + 1 < BeamHits.Length)
                        MoveAll(slot => _plan.Stack(round + 1, slot), BeamCasts[round + 1] - 0.28, BeamHits[round + 1], true);
                }, $"第 {round + 1} 轮光暗无目标余波");
            }

            private void CastBeams(int round)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                for (var side = 0; side < BeamActions.Length; side++)
                {
                    _beamHeading[side] = SelectBeam(snapshot, side);
                    var helper = _fight.Helper(HelperIndex(round, side), Center, _beamHeading[side]);
                    helper.Cast(BeamActions[side], target: helper, castTime: 0.2f, omenHeading: _beamHeading[side], executeOnFinish: false);
                }
            }

            private float SelectBeam(PartySnapshot snapshot, int side)
            {
                var closest = snapshot.Members.Where(member => _plan.Side(member.Position) == side)
                    .OrderBy(member => Vector2.DistanceSquared(member.Position, Center)).Select(member => (PartyMemberSnapshot?)member)
                    .FirstOrDefault();
                _bait[side] = closest?.Slot;
                return closest.HasValue && closest.Value.Position != Center
                    ? Heading(closest.Value.Position - Center) : (float)_plan.LineHeading(side);
            }

            private void ResolveBeams(int round)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                _fight.Run(new[] { Stack(round, 0, snapshot), Stack(round, 1, snapshot) });
            }

            private AreaMechanic Stack(int round, int side, PartySnapshot snapshot)
            {
                var name = $"第 {round + 1} 轮{Colors[side]}之波涛";
                var front = _plan.Front(round, side);
                var target = _bait[side];
                var heading = _beamHeading[side];
                var opposite = _beamHeading[1 - side];
                var expected = Enumerable.Range(0, 8).Where(slot => _plan.Side(round, slot) == side).ToArray();
                var blocked = Enumerable.Range(0, 8).Where(slot => _fight.Now < _resistanceUntil[side, slot]).ToArray();
                bool DoubleHit(PartyMemberSnapshot member) => _beamArea.Contains(member.Position, Center, opposite);

                return new AreaMechanic(name, _beamArea, Center)
                {
                    Heading = heading,
                    LockedSnapshot = snapshot,
                    RequiredPlayers = 4,
                    FailureSeverity = MechanicFailureSeverity.Warning,
                    Check = members =>
                    {
                        if (members.Any(DoubleHit))
                            return "有队员同时站在光暗两道直线内。";
                        if (members.Any(member => blocked.Contains(member.Slot)))
                            return "有队员带着同属性耐性降低参加分摊。";
                        return target == front ? null : "本轮最前方应由指定职能挡枪。";
                    },
                    Visual = result =>
                    {
                        var helper = _fight.Helpers[HelperIndex(round, side)];
                        helper.ExecuteActionEffect(BeamActions[side], helper, heading, result.HitActors, 8, 2.1f);
                        _fight.ApplyStatus(result, MagicVulnerability, 1, helper);
                        if (target.HasValue && result.Participants.Any(member => member.Slot == target))
                        {
                            var slot = target.Value;
                            _resistanceUntil[side, slot] = _fight.Now + 16.6;
                            _fight.Scheduler.ScheduleRelativeToNode(0.60, () => _fight.ApplyStatus(slot, ResistanceDown[side], 16, helper), "挡枪者获得耐性降低");
                        }
                    },
                    Failed = result =>
                    {
                        var inLine = result.Participants.Any(member => member.Slot == _fight.Self);
                        var ownBait = target != front && (target == _fight.Self || front == _fight.Self);
                        var ownPosition = inLine
                            ? blocked.Contains(_fight.Self) || DoubleHit(snapshot.Get(_fight.Self)) || !expected.Contains(_fight.Self) && result.Participants.Count > 4
                            : expected.Contains(_fight.Self) && result.Participants.Count < 4;
                        _fight.Report(result.DescribeBaitFailure(name, expected, front, target)
                            .WithAttribution(ownBait || ownPosition ? new[] { _fight.Self } : Array.Empty<int>()), result);
                    }
                };
            }

            private void ResolveAftershocks(int round)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                _fight.Run(Enumerable.Range(0, AftershockActions.Length).Select(side =>
                {
                    var name = $"第 {round + 1} 轮{Colors[side]}之波涛余波";
                    var heading = _beamHeading[side];
                    var helper = _fight.Helper(HelperIndex(round, side), Center, heading);
                    return new AreaMechanic(name, _beamArea, Center)
                    {
                        Heading = heading,
                        LockedSnapshot = snapshot,
                        MaxPlayers = 0,
                        FailureSeverity = MechanicFailureSeverity.Warning,
                        Visual = result => helper.ExecuteActionEffect(AftershockActions[side], helper, heading, result.HitActors, 8, 2.1f),
                        Failed = result => _fight.Report(result.DescribeFailure(name, Array.Empty<int>())
                            .WithAttribution(result.Participants.Select(member => member.Slot)), result)
                    };
                }));
            }

            private void MoveAll(Func<int, SimPosition> positions, double arrive, double hold, bool urgent = false)
                => _fight.MoveAll(positions, _offset + arrive, _offset + hold, urgent);

            private static int HelperIndex(int round, int side) => (round % 2) * 2 + side;
        }
    }
}
