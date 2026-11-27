using Simulant.Game;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal abstract partial class U7aP2ExerciseLogic
    {
        protected sealed class MirrorMirrorRound
        {
            private const double MirrorsAppear = 5.082;
            private const double FirstPullStart = MirrorsAppear + 2.5;
            private const double FirstCast = 11.139;
            private const double RedArrive = 26.7;
            private const double RedHold = 27.9;
            private const double RedBaitMoveBy = 25.1;
            private readonly SimActor[] _mirrors = new SimActor[3];
            private readonly U7aP2ExerciseLogic _fight;
            private bool _redMeleeBaitsStarted;

            internal P22镜中奇遇Plan Plan { get; }

            internal MirrorMirrorRound(U7aP2ExerciseLogic fight, P22镜中奇遇Plan plan)
            {
                _fight = fight;
                Plan = plan;
            }

            private static uint MapSlot(int direction) => (uint)((8 - direction) % 8 + 1);

            internal void PrepareActors(Action ready)
            {
                var directions = new[] { Plan.White, Plan.NearRed, Plan.FarRed };
                var actorReady = WhenAllActorsReady(directions.Length, ready);
                for (var i = 0; i < directions.Length; i++)
                {
                    var index = i;
                    var position = P22镜中奇遇Plan.MirrorPosition(directions[i]);
                    _fight.SpawnMechanicActor(17825, 9317, 476, 0xA5743C, position, Heading(Center - position), actor =>
                    {
                        _mirrors[index] = actor;
                        actorReady();
                    });
                }
            }

            internal void HoldOpeningTank()
            {
                var area = Plan.OpeningParty(0).WithPosition(_fight.Oracle.Pos);
                var position = _fight.Party[0].Pos;
                _fight.Move(0, area.Contains(position) ? (SimPosition)position : area, 0, MirrorsAppear, urgent: true);
            }

            internal SimTimeline Build()
            {
                var timeline = new SimTimeline();
                _fight.AddAutos(timeline, 5.234, 8.264, 17.306, 20.303, 23.311, 26.348, 34.386);
                timeline.At(0, () => CastFrom(_fight.Oracle, 0x9CF3, 2.7f), "镜中奇遇读条");
                timeline.At(2.991, () => _fight.Oracle.ExecuteActionEffect(0x9CF3, _fight.Oracle, null, null, 1, 2.1f), "镜中奇遇结算");
                timeline.At(MirrorsAppear, ShowMirrors, "一白两红镜面出现");
                timeline.At(FirstPullStart, StartFirstPull, "镜面出现 2.5 秒后 MT 开始拉怪");
                timeline.At(FirstCast, () =>
                {
                    _fight.Oracle.FollowTopEnmity = EnmityFollowMode.Disabled;
                    CastFrom(_fight.Oracle, 0x9D0B, 5.7f);
                    var boss = _fight.Oracle.Pos;
                    foreach (var slot in NearGroup)
                        _fight.Move(slot, Plan.BossBait(slot, boss), 15.8, 18.02, urgent: true);

                    _fight.Yell(_fight.Oracle, 8205520, 12809, 18188);
                }, "镰踢读条与喊话");
                timeline.At(12.124, () => _fight.Oracle.ActorControl(ActorControlCategory.SetModelAttributes, 249), "镰踢模型属性");
                timeline.At(17.107, () =>
                {
                    _fight.Kick(_fight.Oracle, 0x9D0B, KickDonut);
                    _fight.Map(MapSlot(Plan.NearRed), 0x800, 1);
                    _fight.Map(MapSlot(Plan.FarRed), 0x800, 1);
                }, "首领月环及红镜蓄力");
                timeline.At(17.237, () =>
                {
                    _fight.Kick(_mirrors[0], 0x9D0C, KickDonut);
                    CastFrom(_mirrors[1], 0x9D0D, 9.7f);
                    CastFrom(_mirrors[2], 0x9D0D, 9.7f);
                }, "白镜月环与红镜读条");
                timeline.At(17.807, () => _fight.Run(_fight.HouseOfLight(PartySnapshot.Capture(_fight.Party), _mirrors[0].Pos, 4, FarGroup)), "白镜四道扇形");
                timeline.At(17.944, () => _fight.Run(_fight.HouseOfLight(PartySnapshot.Capture(_fight.Party), _fight.Oracle.Pos, 0, NearGroup)), "首领四道扇形");
                timeline.At(18.476, () => _fight.Oracle.ActorControl(ActorControlCategory.SetModelAttributes, 252), "恢复首领模型属性");
                timeline.At(19.180, () =>
                {
                    _fight.Map(MapSlot(Plan.White), 8, 4);
                    _fight.EntityManager.SetEnemyListVisibility(_mirrors[0], false);
                }, "白镜退场");
                timeline.At(19.21, MoveToRedMirrors, "远程转镜，MT 第二次拉怪");
                timeline.At(RedBaitMoveBy, MoveOtherMeleeToRedBaits, "近战组最迟开始红镜分散");
                timeline.At(27.148, ResolveRedDonuts, "两面红镜月环同时结算");
                timeline.At(27.825, () =>
                {
                    var snapshot = PartySnapshot.Capture(_fight.Party);
                    _fight.Run(_fight.HouseOfLight(snapshot, _mirrors[1].Pos, 4, NearGroup).Concat(_fight.HouseOfLight(snapshot, _mirrors[2].Pos, 0, FarGroup)));
                }, "两面红镜的八道扇形");
                timeline.At(RedHold, () => _fight.Oracle.FollowTopEnmity = EnmityFollowMode.Disabled, "红镜站位结束，首领停止追仇");
                timeline.At(29.224, () =>
                {
                    _fight.Map(MapSlot(Plan.NearRed), 8, 4);
                    _fight.Map(MapSlot(Plan.FarRed), 8, 4);
                    _fight.EntityManager.SetEnemyListVisibility(_mirrors[1], false);
                    _fight.EntityManager.SetEnemyListVisibility(_mirrors[2], false);
                }, "红镜退场");
                _fight.AddBanish(timeline, Plan.Spread, castAt: 28.259, effectAt: 33.265, circlesAt: 33.330, move: MoveForBanish);
                return timeline;
            }

            private void ShowMirrors()
            {
                _fight.Map(MapSlot(Plan.NearRed), 0x200, 0x100);
                _fight.Map(MapSlot(Plan.FarRed), 0x200, 0x100);
                _fight.Map(MapSlot(Plan.White), 2, 1);
                foreach (var mirror in _mirrors)
                    _fight.EntityManager.SetEnemyListVisibility(mirror, true);

                _fight.Oracle.FollowTopEnmity = EnmityFollowMode.MoveAndFace;
                FollowOtherMelee(FirstCast);
                _fight.Move(0, _fight.Party[0].Pos, FirstPullStart, FirstPullStart, urgent: true);
                foreach (var slot in FarGroup)
                    _fight.Move(slot, Plan.WhiteBait(slot), 15.8, 18.02);
            }

            private void StartFirstPull()
            {
                PullTank(Plan.FirstPull, () => _fight.Oracle.Pos, FirstCast, FirstCast,
                    () => _fight.Oracle.FollowTopEnmity = EnmityFollowMode.Disabled);
            }

            private void MoveToRedMirrors()
            {
                _fight.Oracle.FollowTopEnmity = EnmityFollowMode.MoveAndFace;
                foreach (var slot in FarGroup)
                    _fight.Move(slot, Plan.RedBait(slot), RedArrive, RedHold, urgent: true);

                FollowOtherMelee(RedBaitMoveBy);
                var tankBait = Plan.RedBait(0);
                var turnPoint = Plan.SecondPullTurnPoint(_fight.Oracle.Pos, tankBait);
                PullTank(turnPoint, () => tankBait, RedArrive, RedHold, MoveOtherMeleeToRedBaits);
            }

            private void FollowOtherMelee(double until)
            {
                foreach (var slot in NearGroup.Skip(1))
                    if (slot != _fight.Self)
                        _fight.Party[slot].FollowForMechanic(() => Spot(_fight.Oracle.Pos, 2.5f), _fight.Origin + until);
            }

            private void MoveOtherMeleeToRedBaits()
            {
                if (_redMeleeBaitsStarted)
                    return;

                _redMeleeBaitsStarted = true;
                foreach (var slot in NearGroup.Skip(1))
                    _fight.Move(slot, Plan.RedBait(slot), RedArrive, RedHold, urgent: true);
            }

            private void PullTank(SimPosition turnPoint, Func<SimPosition> destination, double arrive, double hold, Action onTurn)
            {
                var tank = _fight.Party[0];
                if (_fight.Self == 0)
                {
                    CheckPlayerArrival();
                    return;
                }

                var sampledTurnPoint = turnPoint.Sample(_fight.Random);
                tank.MoveNetworkTo(sampledTurnPoint, speedParam: 60, onArrived: () =>
                {
                    if (_fight.Now >= hold)
                        return;

                    onTurn?.Invoke();
                    var standingPosition = destination();

                    // 折返点就是最终站位时保持已到达的落点，避免再次抽点。
                    if (standingPosition.Equals(turnPoint))
                        standingPosition = sampledTurnPoint;

                    _fight.Move(0, standingPosition, arrive, hold, urgent: true);
                });

                void CheckPlayerArrival()
                {
                    if (_fight.Now >= hold)
                        return;

                    if (turnPoint.Contains(tank.Pos))
                        onTurn?.Invoke();
                    else
                        _fight.Scheduler.ScheduleAfter(0.1, CheckPlayerArrival, "检查本机 MT 是否到达折返点");
                }
            }

            private void MoveForBanish(double arrive, double hold)
                => _fight.MoveAll(Plan.BanishPositions(Partners, _fight.Oracle.Pos, _fight.Oracle.Entity.HitboxRadius),
                    arrive, hold, urgent: true);

            private void ResolveRedDonuts()
            {
                _fight.Run(_mirrors.Skip(1).Select(actor => _fight.HitArea("红镜月环", KickDonut, actor.Pos, actor.Heading, result =>
                    actor.ExecuteActionEffect(0x9D0D, actor, result.Heading, result.HitActors, 8, 2.1f), Array.Empty<int>())));
            }
        }
    }
}
