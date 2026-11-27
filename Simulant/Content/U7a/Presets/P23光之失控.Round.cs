using Simulant.Core;
using Simulant.Game;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Simulant.Content.U7a.Presets.P23光之失控Plan;

namespace Simulant.Content.U7a.Presets
{
    internal abstract partial class U7aP2ExerciseLogic
    {
        protected sealed class LightRampantRound
        {
            private const ushort Lightsteeped = 2257;
            private const ushort Chains = 4157;
            private const ushort Curse = 4158;
            private const ushort Weight = 4159;
            private const uint Lightsteep = 0x9D18;
            private const double AssignLightAt = 5.825;
            private const double FirstTowerHit = 16.154;
            private const double PowerfulLightAt = 21.883;
            private const double CenterTowerHit = 30.906;
            private const double CurseAt = 15.797;
            private const double LinksEnd = 24.821;
            // 实战包没有距离字段；两套解法的近身错误按此距离判定。
            private const float ChainBreakDistance = 12;
            private static readonly int[] LightBanishPartners = { 6, 5, 4, 7, 2, 1, 0, 3 };
            private const int HammerRounds = 5;
            private static double HammerAt(int wave) => 12.873 + 1.553 * wave;
            private static double PuddleSpawnAt(int wave) => wave == HammerRounds - 1 ? 19.783 : 13.774 + 1.595 * wave;
            private static double PuddleFadeAt(int wave) => 23.537 + 1.545 * wave;
            private static double PuddleRemovalAt(int wave) => 26.548 + 1.599 * wave;
            private static readonly ActionArea TowerArea = ActionArea.Circle(4);
            private readonly SimActor[] _orbs = new SimActor[6];
            private readonly int[] _stacks = new int[8];
            private readonly Dictionary<uint, int> _towerCounts = new Dictionary<uint, int>();
            private readonly P23光之失控Plan _plan;
            private readonly U7aP2ExerciseLogic _fight;
            private readonly bool _spread;
            private readonly LightRampantAssignment _assignment;
            private bool _chainsVisible;
            private int _chainWorld;
            private float _openingRadius;

            internal LightRampantRound(U7aP2ExerciseLogic fight, P23光之失控Plan plan, bool spread,
                LightRampantAssignment assignment = LightRampantAssignment.Random)
            {
                _fight = fight;
                _plan = plan;
                _spread = spread;
                _assignment = assignment;
            }

            internal SimTimeline Build()
            {
                var timeline = new SimTimeline();
                AddOpening(timeline);
                AddHammersAndOuterTowers(timeline);
                AddStacksAndOrbs(timeline);
                AddCenterTowerAndBanish(timeline);
                AddFinalWaves(timeline);
                return timeline;
            }

            private void AddOpening(SimTimeline timeline)
            {
                _fight.AddAutos(timeline, 7.769, 37.136);
                timeline.At(0, () =>
                {
                    _fight.Oracle.ActorMove(_fight.Oracle.Pos3D, (float)Math.PI, 90);
                    CastFrom(_fight.Oracle, 0x9D14, 4.7f, (float)Math.PI);
                    Voice(_fight.Oracle, 8205524);
                    var radius = _fight.Oracle.Entity.HitboxRadius;
                    _openingRadius = radius >= 1.5f && !float.IsInfinity(radius) ? radius : DefaultOpeningRadius;
                    _fight.MoveAll(slot => _plan.OpeningArea(Center, slot, _openingRadius), 4.7, AssignLightAt, urgent: true);
                }, "光之失控读条及语音");
                timeline.At(4.971, () => _fight.Oracle.ExecuteActionEffect(0x9D14, _fight.Oracle, null, _fight.Party, 8, 3.1f), "光之失控全屏伤害");
                timeline.At(AssignLightAt, AssignLight, "连线、光之锤、光之重负与初始层数");
                timeline.At(8.045, OpenOuterTowers, "外圈六塔出现");
                timeline.At(8.124, () => Play(_fight.Oracle, 4581), "冰巫女隐去");
            }

            private void AddHammersAndOuterTowers(SimTimeline timeline)
            {
                for (var i = 0; i < HammerRounds; i++)
                {
                    var wave = i;
                    timeline.At(HammerAt(wave), () => ResolveHammer(wave), "光锤点名及冰圈落点");
                }
                timeline.At(CurseAt, ActivateCurse, "连线转为无尽光之束缚");
                timeline.At(16.064, () =>
                {
                    for (var tower = 0; tower < 6; tower++)
                        _fight.Map(TowerMap(tower), 8, 4);
                }, "关闭六座外圈塔");
                timeline.At(FirstTowerHit, ResolveOuterTowers, "外圈六塔结算");
            }

            private void AddStacksAndOrbs(SimTimeline timeline)
            {
                timeline.At(16.549, () => SpawnOrbs(_plan.FirstOrbParity), "第一批三颗光球出现");
                timeline.At(19.353, () =>
                {
                    CastOrbs(_plan.FirstOrbParity);
                    SpawnOrbs(1 - _plan.FirstOrbParity);
                }, "第一批光球咏唱，第二批出现");
                timeline.At(PowerfulLightAt, ResolvePowerfulLight, "两组四人光之重负分摊");
                timeline.At(22.398, () => CastOrbs(1 - _plan.FirstOrbParity), "第二批光球咏唱");
                timeline.At(24.327, () => ResolveOrbs(_plan.FirstOrbParity), "第一批光球爆炸");
                timeline.At(24.51, () => _fight.MoveAll(slot => OrbArea(slot, true), 26.8, 27.35, urgent: true), "移动至第二批光球的贴边安全区");
                timeline.At(LinksEnd, CancelChains, "诅咒连线消失");
                timeline.At(PuddleRemovalAt(0), () => DeleteOrbs(_plan.FirstOrbParity), "回收第一批光球");
                timeline.At(26.898, () => Play(_fight.Oracle, 7747), "冰巫女重新出现");
                timeline.At(27.341, () => ResolveOrbs(1 - _plan.FirstOrbParity), "第二批光球爆炸");
                timeline.At(27.38, MoveToCenterTower, "按实际层数进中央塔或在外待命");
                timeline.At(PuddleRemovalAt(2), () => DeleteOrbs(1 - _plan.FirstOrbParity), "回收第二批光球");
            }

            private void AddCenterTowerAndBanish(SimTimeline timeline)
            {
                timeline.At(22.829, () =>
                {
                    _fight.Map(21, 2, 1);
                    _fight.Map(22, 1, 4);
                    WatchTower(21, Center, 30.824);
                }, "中央四人塔出现");
                timeline.At(30.824, () => _fight.Map(21, 8, 4), "关闭中央塔");
                timeline.At(CenterTowerHit, ResolveCenterTower, "中央四人塔结算");
                _fight.AddBanish(timeline, _spread, castAt: 29.116, effectAt: 34.093, circlesAt: 34.154,
                    move: MoveForBanish, moveAt: 30.97, partners: LightBanishPartners);
            }

            private void AddFinalWaves(SimTimeline timeline)
            {
                timeline.At(35.0, () => _fight.MoveAll(slot => Spot(Center + BanishOffset(slot, true), PositionTolerance), 40, 43.3), "八方分散准备光之波动");
                timeline.At(37.13, () => _fight.Oracle.SetTargetable(true), "冰巫女恢复可选中");
                timeline.At(37.223, () => CastFrom(_fight.Oracle, 0x9CFD, 4.7f), "光之波动读条");
                timeline.At(42.208, () => _fight.Oracle.ExecuteActionEffect(0x9CFD, _fight.Oracle, null, null, 1, 3.1f), "光之海啸读条结算");
                timeline.At(43.119, () =>
                    _fight.Run(_fight.HouseOfLight(PartySnapshot.Capture(_fight.Party), Center, 0, P23光之失控Plan.PartySlots, 0x9CFC, GrantStacks)),
                    "最后八道光之波动及光层");
                timeline.At(44.5, () =>
                {
                    for (var slot = 0; slot < 8; slot++)
                        _fight.ClearStatus(slot, Lightsteeped);
                }, "光之失控结束");
            }

            private void AssignLight()
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                var error = _plan.AssignOpening(snapshot, Center, _fight.Random, _fight.Self, _assignment);
                Array.Copy(_plan.InitialStacks, _stacks, _stacks.Length);
                _chainsVisible = true;
                _chainWorld = _fight._host.WorldGeneration;
                for (var i = 0; i < _plan.Chain.Length; i++)
                {
                    var slot = _plan.Chain[i];
                    _fight.Party[slot].TetherTo(_fight.Party[_plan.Chain[(i + 1) % _plan.Chain.Length]], 110);
                    _fight.Status(slot, Chains, 10);
                }
                for (var slot = 0; slot < 8; slot++)
                    if (_stacks[slot] != 0)
                        _fight.Status(slot, Lightsteeped, 36, (ushort)_stacks[slot]);
                foreach (var target in _plan.Weights)
                    _fight.Status(target, Weight, 16);
                foreach (var slot in _plan.Baits)
                    _fight.Icon(slot, 375);

                var self = _fight.Self;
                var deviation = _plan.CheckSelfOpening(Center, snapshot.Get(self).Position, self, _openingRadius);
                if (deviation.HasValue)
                {
                    var (degrees, metres) = deviation.Value;
                    SendOpeningMessage(FormattableString.Invariant(
                        $"自身初始站位不准确：\n角度偏差 {degrees:0}°，距目标圈 {metres:0.0} m"));
                    _fight.MarkOffender(self);
                }
                if (error.HasValue)
                {
                    // 先发完重排后的头标和连线，再进入现有的两秒失败保留。
                    var (slot, other, previous, next) = error.Value;
                    var reason = $"{Roles[slot]}与{Roles[other]}的距离小于{Roles[slot]}与八方邻近者{Roles[previous]}和{Roles[next]}的距离";
                    SendOpeningMessage("初始站位处理错误：\n" + reason);
                    throw new SimMechanicFailedException("初始站位处理错误", reason, alreadyReported: true);
                }
                for (var side = 0; side < 2; side++)
                    _fight.Move(_plan.Baits[side], HammerArea(side, 0), 12.2, HammerAt(0), urgent: true);
                WatchChains();
            }

            private void SendOpeningMessage(string message)
            {
                _fight._host.LogWarning(message);
                try
                {
                    GameChat.SendEcho(message, playSound: true);
                }
                catch (Exception error)
                {
                    _fight._host.LogWarning("初始站位的聊天提示发送失败：" + error.Message);
                }
            }

            private static uint TowerMap(int tower) => (uint)(9 + (tower + 5) % 6);
            private static Vector2 TowerPosition(int tower) => Center + P23光之失控Plan.TowerOffset(tower);

            private void OpenOuterTowers()
            {
                _fight.Oracle.SetTargetable(false);
                for (var tower = 0; tower < 6; tower++)
                {
                    var map = TowerMap(tower);
                    var center = TowerPosition(tower);
                    _fight.Map(map, 2, 1);
                    _fight.Map(map + 6, 1, 4);
                    _fight.Move(_plan.TowerPlayers[tower], _plan.OuterTowerArea(Center, tower), 10.7, FirstTowerHit);
                    WatchTower(map, center, 16.064);
                }
            }

            private void WatchTower(uint map, Vector2 position, double until)
            {
                if (_fight.Now >= until)
                    return;
                var count = Math.Min(4, PartySnapshot.Capture(_fight.Party).InArea(TowerArea, position).Count);
                if (_towerCounts.TryGetValue(map, out var previous) ? count != previous : count > 0)
                {
                    var low = count == 0 ? 1 : 1 << (count * 2 + 2);
                    _fight.Map(map, (ushort)(low * 2), (ushort)low);
                }
                _towerCounts[map] = count;
                _fight.Scheduler.ScheduleAfter(0.12, () => WatchTower(map, position, until), "刷新塔内人数");
            }

            private void ResolveHammer(int wave)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                var name = $"第 {wave + 1} 轮光锤引导";
                var area = ActionArea.Circle(PuddleRadius);
                _fight.Run(_plan.Baits.Select((slot, side) =>
                {
                    var position = snapshot.Get(slot).Position;
                    return new AreaMechanic(name, area, position)
                    {
                        LockedSnapshot = snapshot,
                        FailureSeverity = MechanicFailureSeverity.Warning,
                        // 两个圈半径相同，只需从一侧检查两名放圈者是否互相命中。
                        Check = members => side == 0 && members.Any(member => member.Slot == _plan.Baits[1])
                            ? "两名光锤点名玩家站得过近，互相吃到了对方的圈。" : null,
                        Failed = result => _fight.Report(MechanicError.Rule(name, _plan.Baits, result.Reason), result),
                        Visual = result =>
                        {
                            var helper = _fight.Helper(side + 6, position);
                            helper.ExecuteActionEffect(0x9D1A, _fight.Party[slot], 0, result.HitActors, 8, 1.6f);
                            _fight.Vulnerability(result, helper, 0.956f);
                            _fight.AddIcePuddle(wave * 2 + side, position, PuddleRadius,
                                PuddleSpawnAt(wave), PuddleFadeAt(wave), PuddleRemovalAt(wave));
                        }
                    };
                }));
                for (var side = 0; side < 2; side++)
                {
                    var last = wave == HammerRounds - 1;
                    var position = last ? OrbArea(_plan.Baits[side], false) : HammerArea(side, wave + 1);
                    var next = last ? PowerfulLightAt : HammerAt(wave + 1);
                    _fight.Move(_plan.Baits[side], position, next - 0.1, last ? 24.49 : next, urgent: true);
                }
            }

            private void ActivateCurse()
            {
                if (!_chainsVisible)
                    return;
                for (var i = 0; i < _plan.Chain.Length; i++)
                {
                    var slot = _plan.Chain[i];
                    _fight.ClearStatus(slot, Chains);
                    _fight.Status(slot, Curse, 9);
                    _fight.Party[slot].TetherTo(_fight.Party[_plan.Chain[(i + 1) % _plan.Chain.Length]], 111);
                }
            }

            private void WatchChains()
            {
                if (_fight.Now >= LinksEnd || !_chainsVisible)
                    return;
                var snapshot = PartySnapshot.Capture(_fight.Party);
                for (var i = 0; i < _plan.Chain.Length; i++)
                {
                    var first = _plan.Chain[i];
                    var second = _plan.Chain[(i + 1) % _plan.Chain.Length];
                    var dead = _fight.Party[first].Entity.HP == 0 || _fight.Party[second].Entity.HP == 0;
                    if (!dead && (_fight.Now < CurseAt || Vector2.DistanceSquared(snapshot.Get(first).Position, snapshot.Get(second).Position)
                        >= ChainBreakDistance * ChainBreakDistance))
                        continue;
                    _fight.GroundEffect(6, 0x9D17, snapshot.Get(first).Position, _fight.Party);
                    _fight.ApplyDamageDown(P23光之失控Plan.PartySlots, _fight.Helpers[6]);
                    CancelChains();
                    _fight.Report(MechanicError.Rule("无尽光连线", new[] { first, second },
                        dead ? "连线玩家倒下，触发光之束缚。" : "两名相连玩家距离过近，触发光之束缚。"));
                    return;
                }
                _fight.Scheduler.ScheduleAfter(0.15, WatchChains, "检查永恒光连线距离");
            }

            internal void CancelChains()
            {
                if (!_chainsVisible || _chainWorld != _fight._host.WorldGeneration)
                    return;
                foreach (var slot in _plan.Chain)
                {
                    if (_fight.Party[slot].IsActive)
                        _fight.Party[slot].CancelTether();
                    _fight.ClearStatus(slot, Chains);
                    _fight.ClearStatus(slot, Curse);
                }
                _chainsVisible = false;
            }

            private void ResolveOuterTowers()
            {
                _fight.Run(Enumerable.Range(0, 6).Select(tower =>
                    ResolveTower(tower, TowerPosition(tower), new[] { _plan.TowerPlayers[tower] })));
                var hitAt = _fight.Origin + PowerfulLightAt;
                for (var tower = 0; tower < 6; tower++)
                {
                    var slot = _plan.TowerPlayers[tower];
                    if (slot == _fight.Self)
                        continue;
                    var destination = OrbArea(slot, false);
                    var via = _plan.StackViaTower(tower);
                    if (via.HasValue)
                        _fight.Party[slot].MoveForMechanic(destination, new[] { TowerPosition(via.Value) }, hitAt, hitAt + 0.04);
                    else
                        _fight.Move(slot, destination, PowerfulLightAt, PowerfulLightAt + 0.04, urgent: true);
                }
            }

            private AreaMechanic ResolveTower(int helperIndex, Vector2 center, int[] expected)
                => _fight.HitArea(expected.Length == 1 ? "外圈光塔" : "中央四人塔", TowerArea, center, 0, result =>
                {
                    _fight.GroundEffect(helperIndex, 0x9D15, center, result.HitActors);
                    if (result.Participants.Count < expected.Length)
                    {
                        _fight.GroundEffect(helperIndex, 0x9D16, center, _fight.Party);
                        _fight.ApplyDamageDown(P23光之失控Plan.PartySlots, _fight.Helpers[helperIndex]);
                    }
                    GrantStacks(result, _fight.Helpers[helperIndex]);
                }, expected);

            private void GrantStacks(MechanicResult result, SimActor source)
            {
                foreach (var player in result.Participants)
                {
                    if (_stacks[player.Slot] >= 5)
                        continue;
                    var stacks = ++_stacks[player.Slot];
                    _fight.RefreshStatus(player.Slot, Lightsteeped, 36, source, (ushort)stacks);
                    if (stacks == 5)
                        ResolveLightsteep(player, source);
                }
            }

            private void ResolveLightsteep(PartyMemberSnapshot player, SimActor source)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                var hits = snapshot.InArea(ActionArea.FromAction(Lightsteep), player.Position);
                // 在层数增加的同刻将辅助施法者移到玩家脚下，随后以该玩家为主目标结算。
                _fight.Dummy(new Vector3(player.Position, 0), 0, actor =>
                {
                    actor.ExecuteActionEffect(Lightsteep, player.Actor, 0, hits.Select(hit => hit.Actor), 8, 2.1f);
                    _fight.Scheduler.ScheduleAfter(2.1, () => _fight.EntityManager.Delete(actor), "回收过量光辅助施法者");
                }, _fight.HelperNameId);
                _fight.ApplyDamageDown(hits.Select(hit => hit.Slot), source);
                _fight.Report(MechanicError.Rule("过量光", new[] { player.Slot }, "过量光叠到五层，以该玩家为中心爆炸。"));
            }

            private SimPosition HammerArea(int side, int wave)
                => Spot(Center + _plan.HammerOffset(side, wave), HammerTolerance);

            private SimPosition OrbArea(int slot, bool afterFirst) => _plan.OrbSafeArea(Center, _plan.Side(slot), afterFirst);

            private void ResolvePowerfulLight()
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                _fight.Run(_plan.Weights.Select(slot =>
                {
                    var side = _plan.Side(slot);
                    var position = snapshot.Get(slot).Position;
                    var expected = Enumerable.Range(0, 8).Where(player => _plan.Side(player) == side).ToArray();
                    var mechanic = _fight.HitArea("光之重负四人分摊", ActionArea.Circle(5), position, 0, result =>
                    {
                        var helper = _fight.Helper(side + 6, position);
                        helper.ExecuteActionEffect(0x9D19, _fight.Party[slot], 0, result.HitActors, 8, 2.1f);
                        _fight.EntityManager.MarkUnderfilledStack(result.HitActors.ToArray(), 4, _fight.Oracle);
                        _fight.ClearStatus(slot, Weight);
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
                    _fight.SpawnMechanicActor(17826, 9318, 4355, 0xA55BFA, point, 0, actor => _orbs[index] = actor, visibleInEnemyList: true);
                }
            }

            private void CastOrbs(int parity)
            {
                for (var tower = parity; tower < 6; tower += 2)
                    CastFrom(_orbs[tower], 0x9D1B, 4.7f);
            }

            private void ResolveOrbs(int parity)
            {
                _fight.Run(Enumerable.Range(0, 6).Where(tower => tower % 2 == parity).Select(tower =>
                {
                    var orb = _orbs[tower];
                    return _fight.HitArea("光球爆炸", ActionArea.Circle(11), TowerPosition(tower), 0, result =>
                        orb.ExecuteActionEffect(0x9D1B, orb, 0, result.HitActors, 8, 2.1f), Array.Empty<int>());
                }));
            }

            private void DeleteOrbs(int parity)
            {
                for (var tower = parity; tower < 6; tower += 2)
                    _fight.EntityManager.Delete(_orbs[tower]);
            }

            private void MoveToCenterTower()
            {
                foreach (var slot in PartySlots)
                {
                    var offset = _stacks[slot] == 2
                        ? BanishOffset(slot, true) * 0.17f
                        : new Vector2(_plan.Side(slot) == 0 ? -6.2f : 6.2f, (slot % 4 - 1.5f) * 0.8f);
                    var position = Spot(Center + _plan.Rotate(offset), PositionTolerance);
                    MoveAvoidingPuddles(slot, position, CenterTowerHit + 0.04);
                }
            }

            private void MoveAvoidingPuddles(int slot, SimPosition area, double until)
            {
                var endsAt = _fight.Origin + until;
                if (slot == _fight.Self || !_fight.Party[slot].IsActive || endsAt <= _fight.Scheduler.ElapsedSeconds)
                    return;

                var actor = _fight.Party[slot];
                var destination = area.Sample(_fight.Random) - Center;
                actor.FollowForMechanic(() =>
                {
                    var now = _fight.Now;
                    var circles = _fight._icePuddles.Values.Where(puddle => now >= puddle.ActiveAt)
                        .Select(puddle => (puddle.Center - Center, puddle.Radius + 0.15f)).ToArray();

                    return Center + PuddleWaypoint(actor.Pos - Center, destination, circles);
                }, endsAt);
            }

            private void MoveForBanish(double arrive, double hold)
            {
                _fight.MoveAll(slot =>
                {
                    var target = _spread || slot < 4 ? slot : LightBanishPartners[slot];
                    var offset = BanishOffset(target, _spread);
                    // 八人各有两米浮动，分散的相邻取样区域之间仍须留够五米。
                    return Spot(Center + (_spread ? offset * (12 / 9.5f) : offset), PositionTolerance);
                }, arrive, hold, urgent: true);
            }

            private void ResolveCenterTower() => _fight.Run(ResolveTower(7, Center, _plan.CenterPlayers));
        }
    }
}
