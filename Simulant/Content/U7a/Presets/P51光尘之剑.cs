using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Core.Entity;
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
using static System.Math;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(5.1)]
    internal class P51光尘之剑 : SimPresetBase
    {
        public override int TerritoryId { get; } = 1238;
        public override string Name { get; } = "P5-1 光尘之剑";
        public override string Author { get; } = "阿洛";
        public override DateTime LastUpdated { get; } = new DateTime(2026, 11, 23);
        public override PhaseData Phase { get; } = U7a.P5;
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override int Level => 100;
        public override Type SimLogicType { get; } = typeof(FulgentBladeLogic);
        public override string Description { get; } = "躲避光尘之剑地火，随后处理死亡轮回。面向 Boss 一组左二组右。开启模拟队友后可练习两组四人分摊。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new CheckBoxOption(nameof(FulgentBladeLogic.GenerateSimulatedParty), "生成模拟队友", false),
            U7aExerciseOptions.Player(),
            new ComboBoxOption<CenterDir4>(
                nameof(FulgentBladeLogic.CenterDir4),
                "地火安全点相对于中心的方向",
                CenterDir4.Random,
                new Map<CenterDir4>
            {
                [CenterDir4.Random] = "随机",
                    [CenterDir4.N] = "北 (0)",
                    [CenterDir4.W] = "西 (1)",
                    [CenterDir4.S] = "南 (2)",
                    [CenterDir4.E] = "东 (3)",
            }),

            new ComboBoxOption<FirstLineDir4>(
                nameof(FulgentBladeLogic.FirstLineDir4),
                "地火第一根线相对于安全点的方向",
                FirstLineDir4.Random,
                new Map<FirstLineDir4>
            {
                [FirstLineDir4.Random] = "随机",
                    [FirstLineDir4.N] = "北 (0)",
                    [FirstLineDir4.W] = "西 (1)",
                    [FirstLineDir4.S] = "南 (2)",
                    [FirstLineDir4.E] = "东 (3)",
            }),

            new ComboBoxOption<RotateDirection>(
                nameof(FulgentBladeLogic.RotateDirection),
                "地火旋转方向",
                RotateDirection.Random,
                new Map<RotateDirection>
            {
                [RotateDirection.Random] = "随机",
                    [RotateDirection.ClockWise] = "顺时针",
                    [RotateDirection.CounterClockwise] = "逆时针",
            }),
        };

    }

    internal enum CenterDir4
    {
        [RandomEnum(N, W, S, E)]
        Random = -1,
        N = 0,
        W = 1,
        S = 2,
        E = 3
    }

    internal enum FirstLineDir4
    {
        [RandomEnum(N, W, S, E)]
        Random = -1,
        N = 0,
        W = 1,
        S = 2,
        E = 3
    }

    internal enum RotateDirection
    {
        [RandomEnum(ClockWise, CounterClockwise)]
        Random,
        ClockWise,
        CounterClockwise
    }

    internal sealed class FulgentBladeLogic : U7aP5ExerciseLogic
    {
        [SimOption] internal bool GenerateSimulatedParty { get; set; }
        [SimOption] internal CenterDir4 CenterDir4 { get; set; }
        [SimOption] internal FirstLineDir4 FirstLineDir4 { get; set; }
        [SimOption] internal RotateDirection RotateDirection { get; set; }

        private FulgentBladeRound _round;
        protected override bool Solo => !GenerateSimulatedParty;

        public FulgentBladeLogic(PluginHost host, SimPresetBase preset) : base(host, preset, FulgentBladeHelper + FulgentBladeHelperCount)
        {
        }

        protected override void PreparePlan()
            => _round = new FulgentBladeRound(this, 0, 1, CenterDir4, FirstLineDir4, RotateDirection);

        protected override SimTimeline BuildExercise()
        {
            var timeline = _round.Build();
            timeline.At(42, () =>
            {
                Complete();
                EntityManager.Delete(Pandora);
            }, "光尘之剑练习结束");
            return timeline;
        }
    }

    internal abstract partial class U7aP5ExerciseLogic
    {
        protected sealed class FulgentBladeRound
        {
            private const float StepDistance = 5;
            private const float LastLineOriginRadius = 22.001f;
            private const float PocketTolerance = 0.9f;
            private const float SectorCenterOffset = 0.35f;
            private const float StackTolerance = 1.25f;
            private const float StackSideMargin = StackTolerance + 0.1f;
            private const float StackLateralSpacing = 8.5f;
            private const float StackPreferredRadius = 8.5f;
            private const float StackInwardDistance = 2;
            private const float StackApproachTolerance = 0.65f;
            private const double FineLinesAt = 7.96;
            private const double GlowLead = 7.07;
            private const double SplitFormationAt = 29.50;
            private const double SplitAfterFlareAt = 31.505;
            private const double SplitHit = 32.975;
            private const int LineGroups = 3;
            private static double FirstHitAt(int group) => 17.035 + 4.020 * group;
            private static readonly uint[] FirstActions = { 0x9D73, 0x9CB6 };
            private static readonly uint[] StepActions = { 0x9D74, 0x9D75 };
            private static readonly uint[] StackActions = { 0x9D78, 0x9D77 };
            private static readonly Vector2[] StackOffsets = (
                from x in Enumerable.Range(-32, 65)
                from y in Enumerable.Range(-32, 65)
                let offset = new Vector2(x * 0.5f, y * 0.5f)
                where offset.LengthSquared() <= 16 * 16
                orderby offset.LengthSquared()
                select offset).ToArray();
            private readonly U7aP5ExerciseLogic _fight;
            private readonly double _offset;
            private readonly int _occurrence;
            private readonly Vector2 _octagonCenter;
            private readonly SimPosition _approachArea;
            private readonly double[] _simpleHeadings;
            private readonly (Vector2 Position, Vector2 Normal, float Heading)[] _lines;
            private readonly EventObject[] _lineObjects = new EventObject[2 * LineGroups];
            private readonly Vector2[] _stackPositions = new Vector2[2];
            private readonly SimPosition[] _stackAreas = new SimPosition[2];
            private readonly SimPosition?[] _stackApproach = new SimPosition?[2];
            private readonly List<(double At, Vector2 Position, float Heading)> _remainingFlares = new List<(double, Vector2, float)>();
            private readonly ActionArea _lineArea = ActionArea.FromAction(0x9D74);
            private readonly ActionArea _stackArea = ActionArea.FromAction(0x9D77);
            private Vector2 _stackCastOrigin;
            private float _stackCastHeading;
            private Vector2 _leftOfBoss;
            private int _previousMainTank = -1;

            internal FulgentBladeRound(U7aP5ExerciseLogic fight, double offset, int occurrence,
                CenterDir4 center, FirstLineDir4 firstLine, RotateDirection rotation)
            {
                _fight = fight;
                _offset = offset;
                _occurrence = occurrence;
                var toward = Direction(Deg((int)center * 90 - 180));
                var innerOffset = (float)Sqrt(50) * toward;
                var turn = rotation == RotateDirection.ClockWise ? -1 : 1;
                _octagonCenter = Center + innerOffset;
                _approachArea = Spot(Center + 6 * toward, 4);
                _lines = CalculateLines(innerOffset, firstLine, turn);

                // 对应触发器的初始交点 dir-8，窄扇先取逆着地火顺序的一边。
                var corner = Deg((int)firstLine * 90 - 180 + turn * 22.5);
                _simpleHeadings = new[] { corner + PI, corner - turn * PI / 4, corner + turn * PI / 4, corner + PI };
            }

            internal SimTimeline Build()
            {
                var timeline = new SimTimeline();
                timeline.At(0, () =>
                {
                    _fight.LockBossFacing();
                    _fight.CastBoss(0x9D72, 5.7f);
                }, "光尘之剑读条");
                timeline.At(0.10, SpawnLines, "生成六条地火线");
                timeline.At(5.98, () => _fight.Raidwide(0x9D72, 3.1f), "光尘之剑全屏分伤结算");
                timeline.At(FineLinesAt, () =>
                {
                    foreach (var line in _lineObjects)
                        _fight.EntityManager.PlayEObjAnimation(line, 1, 2);
                }, "六条地火细线出现");
                timeline.At(9.10, () => _fight.Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly, "地火后跟随一仇转向");

                _remainingFlares.Clear();
                for (var index = 0; index < _lines.Length; index++)
                    AddLine(timeline, index);
                if (!_fight.Solo)
                    AddPartyMovement(timeline);

                timeline.At(24.90, () =>
                {
                    _fight.LockBossFacing();
                    _stackCastOrigin = _fight.Pandora.Pos;
                    _stackCastHeading = _fight.BossHeading;
                    _fight.CastBoss(0x9D76, 7.7f);
                }, "死亡轮回读条");
                timeline.At(32.88, () => _fight.BossEffect(0x9D76, 3.1f), "死亡轮回本体结算");
                timeline.At(SplitHit, ResolveStacks, "两组四人分摊结算");
                timeline.At(35.95, () => _fight.Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly, "双分摊后恢复一仇转向");
                AddDialogueAndAutos(timeline);
                return timeline;
            }

            private void AddDialogueAndAutos(SimTimeline timeline)
            {
                if (_occurrence == 2)
                {
                    _fight.AddDialogue(timeline, 15.300, 43, RyneNameId, "琳的第二轮地火台词");
                    _fight.AddDialogue(timeline, 20.307, 44, GaiaNameId, "盖娅的第二轮地火台词");
                    _fight.AddDialogue(timeline, 27.392, 37, _fight.BossNameId, "潘多拉的第二轮地火台词");
                }
                else if (_occurrence == 3)
                {
                    _fight.AddDialogue(timeline, 24.853, 45, GaiaNameId, "盖娅的第三轮地火台词");
                    _fight.AddDialogue(timeline, 29.858, 46, RyneNameId, "琳的第三轮地火台词");
                    _fight.AddDialogue(timeline, 34.836, 47, _fight.BossNameId, "潘多拉的第三轮地火台词");
                }

                _fight.AddDoubleAuto(timeline, 9.152, 9.830);
                _fight.AddDoubleAuto(timeline, 12.274, 13.020);
                _fight.AddDoubleAuto(timeline, 15.413, 16.117);
                _fight.AddDoubleAuto(timeline, 18.604, 19.292);
                _fight.AddDoubleAuto(timeline, 21.711, 22.429);
                _fight.AddDoubleAuto(timeline, 35.992, 36.741);
                if (_occurrence > 1)
                    _fight.AddDoubleAuto(timeline, 39.182, 39.901);
            }

            private void SpawnLines()
            {
                // 从最后一条开始生成，让先亮的线拥有较大的实体 ID。
                for (var index = _lines.Length - 1; index >= 0; index--)
                {
                    var line = _lines[index];
                    _lineObjects[index] = _fight.EntityManager.SpawnEObj((ushort)index, 2014199,
                        new Vector3(line.Position, 0), line.Heading);
                }
            }

            private void AddLine(SimTimeline timeline, int index)
            {
                var line = _lines[index];
                var firstHit = FirstHitAt(index / 2);
                timeline.At(firstHit - GlowLead, () => _fight.EntityManager.PlayEObjAnimation(_lineObjects[index], 0x20, 0x10), "地火线发光");
                timeline.At(firstHit - 6.94, () =>
                {
                    for (var side = 0; side < FirstActions.Length; side++)
                    {
                        var heading = line.Heading + side * (float)PI;
                        var helper = _fight.Helper(LineHelper(index, side), line.Position, heading);
                        helper.Cast(FirstActions[side], target: helper, castTime: 6.7f, omenHeading: heading,
                            omenDelay: 0, executeOnFinish: false);
                    }
                }, "地火光暗预兆");
                timeline.At(firstHit - 0.06, () => _fight.EntityManager.PlayEObjAnimation(_lineObjects[index], 4, 8), "地火双向箭头");
                timeline.At(firstHit + 2.12, () => _fight.EntityManager.Delete(_lineObjects[index]), "地火线消失");

                for (var side = 0; side < FirstActions.Length; side++)
                {
                    var helperIndex = LineHelper(index, side);
                    var heading = line.Heading + side * (float)PI;
                    var advance = StepDistance * Direction(heading);
                    var firstAction = FirstActions[side];
                    var stepAction = StepActions[side];
                    for (var step = 0; ; step++)
                    {
                        var point = line.Position + step * advance;
                        if (Vector2.DistanceSquared(point, Center) > LastLineOriginRadius * LastLineOriginRadius)
                            break;
                        var action = step == 0 ? firstAction : stepAction;
                        var name = step == 0 ? "地火首段" : "地火推进";
                        var hit = firstHit + step * 2.055;
                        timeline.At(hit, () => ResolveLine(helperIndex, action, point, heading, name), name);
                        if (hit >= SplitFormationAt)
                            _remainingFlares.Add((hit, point, heading));
                    }
                }
            }

            private static int LineHelper(int index, int side) => FulgentBladeHelper + index * 2 + side;

            private void ResolveLine(int index, uint action, Vector2 position, float heading, string name)
            {
                var helper = _fight.Helper(index, position, heading);
                _fight.Run(new AreaMechanic(name, _lineArea, position)
                {
                    Heading = heading,
                    MaxPlayers = 0,
                    FailureSeverity = MechanicFailureSeverity.Warning,
                    Visual = result => helper.ExecuteActionEffect(action, helper, heading, result.HitActors, 8, 2.1f),
                    Failed = result => _fight.Report(result.DescribeFailure(name, Array.Empty<int>())
                        .WithAttribution(result.Participants.Select(member => member.Slot)), result)
                });
            }

            private void AddPartyMovement(SimTimeline timeline)
            {
                var firstHit = FirstHitAt(0);
                var glow = firstHit - GlowLead;
                timeline.At(FineLinesAt, () => _fight.MoveAll(_ => _approachArea, _offset + glow, _offset + glow), "细线出现后靠近地火一侧");
                AddMove(glow, 0, SafePocket(0, 11), firstHit, true, "首组发光后确定地火安全区");

                for (var group = 0; group < LineGroups; group++)
                {
                    var nextLeg = group + 1;
                    var hit = FirstHitAt(group);
                    AddMove(hit + 2.14, nextLeg, SafePocket(group, 9), hit + 3.82, true, $"第 {nextLeg} 次穿地火");
                    if (nextLeg < LineGroups)
                    {
                        var nextHit = FirstHitAt(nextLeg);
                        AddMove(nextHit + 0.24, nextLeg, SafePocket(nextLeg, 11), nextHit + 1.78,
                            false, "多次穿线的队员转入下一对地火安全区");
                    }
                }

                timeline.At(SplitFormationAt, () =>
                {
                    SetStackPositions();
                    for (var slot = 0; slot < _fight.Party.Count; slot++)
                    {
                        var side = slot & 1;
                        var approach = _stackApproach[side];
                        var arrive = approach.HasValue ? SplitAfterFlareAt - 0.27 : SplitHit - 0.35;
                        _fight.Move(slot, approach ?? _stackAreas[side], _offset + arrive, _offset + SplitHit);
                    }
                }, "地火末段分成两组");
                timeline.At(SplitAfterFlareAt, () =>
                {
                    for (var slot = 0; slot < _fight.Party.Count; slot++)
                        if (_stackApproach[slot & 1].HasValue)
                            _fight.Move(slot, _stackAreas[slot & 1], _offset + SplitHit - 0.12, _offset + SplitHit, urgent: true);
                }, "最后一组地火通过后进入分摊区");

                void AddMove(double time, int leg, Vector2 pocket, double arrive, bool moveBoth, string name)
                    => timeline.At(time, () => MoveToSafeAreas(leg, pocket, arrive, moveBoth), name);
            }

            private void MoveToSafeAreas(int leg, Vector2 pocket, double arrive, bool moveBoth)
            {
                var mainTank = _fight.MainTank;
                for (var slot = 0; slot < _fight.Party.Count; slot++)
                {
                    if (slot == _fight.Self)
                        continue;

                    var usesComplexRoute = UsesComplexRoute(slot, mainTank);
                    if (!moveBoth && !usesComplexRoute && (slot != mainTank || mainTank == _previousMainTank))
                        continue;

                    var destination = usesComplexRoute
                        ? Spot(pocket, PocketTolerance)
                        : SimplePosition(leg, slot == mainTank);
                    var member = slot;
                    var deadline = _offset + arrive;
                    if (leg == 0 || !_fight.EntityManager.PlayerBehavior.DelayArrival)
                    {
                        _fight.Move(member, destination, deadline, _offset + SplitFormationAt);
                        continue;
                    }

                    // 按队员还要走的距离分配反应余量，实际取点和走位继续交给实体。
                    var distance = Vector2.Distance(_fight.Party[member].Pos, destination.Midpoint()) + destination.DeviationRadius;
                    var spare = deadline - _fight.Now - distance / 6 - 0.18;
                    var delay = Max(0, Min(0.58, spare)) * (0.15 + 0.85 * _fight.Random.NextDouble());
                    _fight.Scheduler.ScheduleAfter(delay, () => _fight.Move(member, destination, deadline, _offset + SplitFormationAt), "队员穿地火");
                }

                _previousMainTank = mainTank;
            }

            private static bool UsesComplexRoute(int slot, int mainTank)
                => slot != mainTank && slot + 1 >= (int)ExerciseRole.D1 && slot + 1 <= (int)ExerciseRole.D3;

            private SimPosition SimplePosition(int leg, bool mainTank)
            {
                var heading = _simpleHeadings[leg];
                var direction = Direction(heading);
                // 沿中轴避开回放线位偏差，扇形顶点也能完整留在安全侧。
                var origin = _octagonCenter + SectorCenterOffset * direction;
                if (mainTank)
                    return origin + 3 * direction;

                return SimPosition.AnnularSector(origin, 2, heading, 2, Deg(leg == 0 || leg == 3 ? 65 : 20));
            }

            private void SetStackPositions()
            {
                var pocket = SafePocket(2, 9);
                // 偏向的一组用读条面向选点，另一组用最近的正方向。归属始终按本次读条中轴判断。
                _leftOfBoss = Direction(_stackCastHeading - PI / 2);
                var cardinal = Round(_stackCastHeading / (PI / 2)) * (PI / 2);
                var leftReference = Min(_stackCastHeading, cardinal);
                var rightReference = Max(_stackCastHeading, cardinal);
                var points = StackOffsets.Select(offset => pocket + offset)
                    .Where(point => Vector2.DistanceSquared(point, Center) <= 18 * 18);
                var early = points.Where(point => Vector2.DistanceSquared(point, pocket) <= 8.5f * 8.5f &&
                    CanStackAt(point, StackTolerance, SplitFormationAt));
                if (!FindStackPair(early, pocket, leftReference, rightReference) &&
                    !FindStackPair(points.Where(point => CanStackAt(point, StackTolerance, SplitAfterFlareAt)), pocket, leftReference, rightReference))
                    throw new InvalidOperationException("无法为死亡轮回找到两侧的分摊区域。");

                var forward = Direction(_stackCastHeading);
                for (var side = 0; side < _stackPositions.Length; side++)
                {
                    var point = _stackPositions[side];
                    var depth = Vector2.Dot(point - _stackCastOrigin, forward);
                    var inward = -Sign(depth) * forward;
                    var away = Direction(side == 0 ? leftReference - PI / 2 : rightReference + PI / 2);
                    // 沿读条前后轴靠近 Boss，保留横向距离。遇到末段地火时缩短靠近的距离。
                    for (var move = Min(StackInwardDistance, Abs(depth)); move > 0; move -= 0.5f)
                    {
                        var closer = point + move * inward;
                        if (Vector2.Dot(closer - _stackCastOrigin, away) < StackSideMargin ||
                            !CanStackAt(closer, StackTolerance, SplitAfterFlareAt))
                            continue;

                        point = closer;
                        _stackPositions[side] = point;
                        break;
                    }

                    var direction = point - _stackCastOrigin;
                    var radius = direction.Length();
                    const float radial = 0.85f;
                    var angle = 2 * Asin(Sqrt((StackTolerance * StackTolerance - radial * radial) / (4 * radius * (radius + radial))));
                    _stackAreas[side] = SimPosition.AnnularSector(_stackCastOrigin, radius, Heading(direction), radial, Min(Deg(20), angle));
                    _stackApproach[side] = null;
                    if (!CanStackAt(point, StackTolerance, SplitFormationAt))
                    {
                        var approach = points.Where(candidate => CanStackAt(candidate, StackApproachTolerance, SplitFormationAt))
                            .OrderBy(candidate => Vector2.DistanceSquared(candidate, point)).First();
                        _stackApproach[side] = Spot(approach, StackApproachTolerance);
                    }
                }
            }

            private bool CanStackAt(Vector2 point, float tolerance, double from)
                => _remainingFlares.All(line => line.At < from ||
                    !_lineArea.IntersectsCircle(point, tolerance + 0.12f, line.Position, line.Heading));

            private bool FindStackPair(IEnumerable<Vector2> points, Vector2 pocket, double leftReference, double rightReference)
            {
                var candidates = points.Select(point => (Point: point, Lateral: SideOffset(point))).ToArray();
                var right = SideChoices(rightReference, -1).ToArray();
                if (right.Length == 0)
                    return false;

                var best = float.PositiveInfinity;
                foreach (var left in SideChoices(leftReference, 1))
                {
                    if (left.Cost + right[0].Cost >= best)
                        break;

                    foreach (var partner in right)
                    {
                        var cost = left.Cost + partner.Cost;
                        if (cost >= best)
                            break;
                        if (left.Lateral - partner.Lateral < StackLateralSpacing)
                            continue;

                        best = cost;
                        _stackPositions[0] = left.Point;
                        _stackPositions[1] = partner.Point;
                        break;
                    }
                }
                return !float.IsPositiveInfinity(best);

                IEnumerable<(Vector2 Point, float Lateral, float Cost)> SideChoices(double reference, int sign)
                {
                    var away = Direction(reference - sign * PI / 2);
                    var preferred = _stackCastOrigin + StackPreferredRadius * Direction(reference - sign * PI / 6);
                    return candidates.Where(candidate => sign * candidate.Lateral >= StackSideMargin &&
                            Vector2.Dot(candidate.Point - _stackCastOrigin, away) >= StackSideMargin)
                        .Select(candidate => (candidate.Point, candidate.Lateral, Cost: Vector2.DistanceSquared(candidate.Point, preferred) +
                            0.25f * Vector2.DistanceSquared(candidate.Point, pocket)))
                        .OrderBy(candidate => candidate.Cost);
                }
            }

            private float SideOffset(Vector2 point) => Vector2.Dot(point - _stackCastOrigin, _leftOfBoss);

            private PartyMemberSnapshot?[] SelectStackTargets(PartySnapshot snapshot, int selfSlot)
            {
                var self = snapshot.Members.Where(member => member.Slot == selfSlot)
                    .Select(member => (PartyMemberSnapshot?)member).FirstOrDefault();
                var ownSide = selfSlot & 1;
                // 在对组四米判定内时强制由本人带己方分摊，令串组按实际位置结算。
                var forceSelf = self.HasValue && snapshot.Members.Any(member => (member.Slot & 1) != ownSide &&
                    _stackArea.Contains(member.Position, self.Value.Position, 0));
                var targets = new PartyMemberSnapshot?[2];
                for (var side = 0; side < targets.Length; side++)
                    targets[side] = forceSelf && side == ownSide ? self : snapshot.Members
                        .Where(member => (!forceSelf || member.Slot != selfSlot) && (SideOffset(member.Position) >= 0 ? 0 : 1) == side)
                        .OrderBy(member => Vector2.DistanceSquared(member.Position, _stackPositions[side]))
                        .Select(member => (PartyMemberSnapshot?)member).FirstOrDefault();
                return targets;
            }

            private void ResolveStacks()
            {
                if (_fight.Solo)
                    SetStackPositions();
                var snapshot = PartySnapshot.Capture(_fight.Party);
                var stackSize = _fight.Solo ? 1 : 4;
                var stacks = new List<SimMechanic>(2);
                var targets = SelectStackTargets(snapshot, _fight.Self);
                for (var side = 0; side < targets.Length; side++)
                    AddStack(side, targets[side]);
                _fight.Run(stacks);

                void AddStack(int side, PartyMemberSnapshot? target)
                {
                    var location = target?.Position ?? _stackPositions[side];
                    var heading = Heading(location - _stackCastOrigin);
                    var helper = _fight.Helper(side, Center, heading);
                    if (!target.HasValue)
                    {
                        helper.ExecuteActionEffect(StackActions[side], helper, heading, Array.Empty<SimActor>(), 8, 2.1f);
                        return;
                    }

                    var member = target.Value;
                    var name = $"双分摊第 {side + 1} 组";
                    var expected = _fight.Solo ? new[] { _fight.Self } :
                        Enumerable.Range(0, _fight.Party.Count).Where(slot => (slot & 1) == side).ToArray();
                    stacks.Add(new AreaMechanic(name, _stackArea, location)
                    {
                        LockedSnapshot = snapshot,
                        RequiredPlayers = stackSize,
                        FailureSeverity = MechanicFailureSeverity.Warning,
                        Check = members => AreaMechanic.CheckPartyAssignment(members, expected),
                        Visual = result =>
                        {
                            helper.ExecuteActionEffect(StackActions[side], member.Actor, heading, result.HitActors, 8, 2.1f);
                            _fight.ApplyStatus(result, MagicVulnerability, 1, helper);
                        },
                        Failed = result => _fight.Report(result.DescribeFailure(name, expected, member.Slot), result)
                    });
                }
            }

            private static (Vector2 Position, Vector2 Normal, float Heading)[] CalculateLines(
                Vector2 innerOffset, FirstLineDir4 firstLine, int turn)
            {
                var result = new (Vector2 Position, Vector2 Normal, float Heading)[6];
                for (var index = 0; index < result.Length; index++)
                {
                    var dir8 = (int)firstLine * 2 + turn * index;
                    var angle = Deg(dir8 * 45 - 180);
                    var normal = Direction(angle);
                    var cardinal = dir8 % 2 == 0;
                    // 回放的正线垂足是 3、10、17 米，斜线是 5.071、15.071 米。
                    var distance = 10 + Vector2.Dot(innerOffset, normal);
                    distance = cardinal ? (float)Round(distance) : distance + (float)Sqrt(50) - 7;
                    result[index] = (Center + normal * distance, normal, (float)(angle + (cardinal ? PI : 0)));
                }

                return result;
            }

            private Vector2 SafePocket(int group, float inset)
            {
                // 两条平行于地火线并向内偏移的直线相交，交点用作假队员走位。
                var first = _lines[2 * group];
                var second = _lines[2 * group + 1];
                var a = Vector2.Dot(first.Position - Center, first.Normal) - inset;
                var b = Vector2.Dot(second.Position - Center, second.Normal) - inset;
                var determinant = first.Normal.X * second.Normal.Y - first.Normal.Y * second.Normal.X;
                return Center + new Vector2(a * second.Normal.Y - b * first.Normal.Y,
                    b * first.Normal.X - a * second.Normal.X) / determinant;
            }
        }
    }

}
