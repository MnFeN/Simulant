using Simulant.Core.Entity;
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
        protected sealed class DiamondDustRound
        {
            private const ushort HeavenlyStrikeKnockback = 156;
            private const ushort ThinIce = 911;
            private const ushort WeaponState = 2273;
            private const ushort GazeStun = 4163;
            private const int FlowerHelper = 8;
            private const int ConeHelper = 20;
            private const double KnockbackStartsAt = 26.372;
            private const double GazeAt = 38.040;
            private const double IceFloorAt = 38.162;
            private const double FirstSlideAt = 41.49;
            private const double FirstSword = 44.529;
            private const double SecondSlideAt = 44.59;
            private const double SecondSword = 46.654;
            private const double BossAppearsAt = 50.247;
            private const double SlideDuration = 1.73;
            private const int StackRounds = 4;
            private static double StackHitAt(int wave) => 29.652 + 1.560 * wave;
            private static double PuddleSpawnAt(int wave) => 30.535 + 1.608 * wave;
            private static double PuddleFadeAt(int wave) => 41.259 + 1.566 * wave;
            private static double PuddleRemovalAt(int wave) => wave == StackRounds - 1 ? 48.982 : 44.579 + 1.602 * wave;
            private static readonly ActionArea FlowerCross = ActionArea.Cross(40, 5);
            private static readonly ActionArea FlowerCircle = ActionArea.Circle(5);
            private readonly Vector2[] _flowers = new Vector2[4];
            private readonly bool[] _sliding = new bool[8];
            private readonly U7aP2ExerciseLogic _fight;
            private SimActor _reflection;
            private bool _bossAppeared;
            private int _lateWalkBackSlot = -1;
            private float _rayHeading;

            internal P21钻石星尘Plan Plan { get; }
            private Vector2 ReflectionPoint => Center + P21钻石星尘Plan.At(Plan.Reflection, 10);
            private Vector2 GazePoint => Center + P21钻石星尘Plan.At(Plan.Gaze, 20);
            private float ReflectionHeading => Heading(Center - ReflectionPoint);
            private static Vector2 SouthernLanding => Center + new Vector2(0, 5);

            internal DiamondDustRound(U7aP2ExerciseLogic fight, P21钻石星尘Plan plan)
            {
                _fight = fight;
                Plan = plan;
            }

            internal void PrepareActors(Action ready)
                => _fight.EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17824,
                    BNpcNameId = 13554,
                    ModelCharaId = 4375,
                    LayoutId = 0xA55BF9,
                    EventId = _fight._host.ZoneService.ContentDirectorId,
                    Pos = new Vector3(Center, 0),
                    Heading = 0,
                    Level = 100,
                    // 国服回放中，镜像提前以此标志生成在场中，显现时不移动。
                    DisplayFlags = 0x6000B,
                    Targetable = false,
                    EnemyListVisibility = false
                }, actor =>
                {
                    _reflection = actor;
                    ready();
                });

            internal void StartSouthernJump()
                => StartIceBossJump(_fight.Oracle, SouthernLanding, 0);

            internal void FinishSouthernJump()
                => FinishIceBossJump(_fight.Oracle, SouthernLanding, 0);

            internal SimTimeline Build()
            {
                var timeline = new SimTimeline();
                AddOpening(timeline);
                AddKickAndIceMarkers(timeline);
                AddKnockbackAndFlowers(timeline);
                AddStackTrail(timeline);
                AddGazeAndIce(timeline);
                AddSwords(timeline);
                AddRay(timeline);
                return timeline;
            }

            private void AddOpening(SimTimeline timeline)
            {
                _fight.AddAutos(timeline, 52.290, 60.400, 63.388);
                timeline.At(0, () =>
                {
                    _fight.Oracle.ActorMove(_fight.Oracle.Pos3D, (float)Math.PI, 90);
                    CastFrom(_fight.Oracle, 0x9CF4, 2.7f, (float)Math.PI);
                }, "本体转向场中，镜中显影读条");
                timeline.At(2.923, () => _fight.Oracle.ExecuteActionEffect(0x9CF4, _fight.Oracle, null, null, 1, 2.1f), "镜中显影结算");
                timeline.At(5.020, () => _fight.Map(0, 2, 1), "镜中显影场地变化");
                timeline.At(5.091, () =>
                {
                    CastFrom(_fight.Oracle, 0x9D05, 4.7f);
                    Voice(_fight.Oracle, 8205523);
                }, "钻石星尘读条及语音");
                timeline.At(6.061, () =>
                {
                    Play(_reflection, 4566);
                    _fight.EntityManager.SetEnemyListVisibility(_reflection, true);
                }, "巫女的镜像显现");
                timeline.At(9.111, () => _fight.Map(0, 8, 4), "镜中显影场效结束");
                timeline.At(10.079, () => _fight.Oracle.ExecuteActionEffect(0x9D05, _fight.Oracle, null, _fight.Party, 8, 2.6f), "钻石星尘全屏伤害");
            }

            private void AddKickAndIceMarkers(SimTimeline timeline)
            {
                for (var wave = 0; wave < 3; wave++)
                {
                    var first = wave == 2 ? 6 : 2 * wave;
                    var count = wave == 1 ? 4 : 2;
                    AddIcicles(timeline, first, count, 12.704 + 4.0345 * wave, 21.709 + 3.9955 * wave);
                }
                var kick = Plan.Axe ? 0x9D0Au : 0x9D0Bu;
                timeline.At(12.979, () =>
                {
                    _reflection.ActorMove(_reflection.Pos3D, (float)Math.PI, 90);
                    CastFrom(_reflection, kick, 5.7f, (float)Math.PI);
                    _fight.Yell(_reflection, Plan.Axe ? 8205519u : 8205520u, 13554, Plan.Axe ? (ushort)18187 : (ushort)18188);
                }, "分身斧踢或镰踢读条与喊话");
                timeline.At(13.165, () =>
                {
                    _fight.Oracle.SetTargetable(false);
                    foreach (var slot in Plan.Marked)
                        _fight.Icon(slot, 345);
                    _fight.MoveAll(slot => Plan.KickArea(Center, slot), 18.35, 19.78);
                }, "本体不可选中，分配冰石头标和扇形站位");
                timeline.At(13.247, () => Play(_fight.Oracle, 4581), "本体隐去");
                timeline.At(14.018, () => _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 249), "回旋踢模型属性");
                timeline.At(18.947, () =>
                {
                    _fight.Kick(_reflection, kick, Plan.Axe ? ActionArea.Circle(16) : KickDonut);
                    foreach (var slot in Plan.Marked)
                        _fight.Move(slot, Plan.IceMarkerArea(Center, slot), 21.02, 21.30, urgent: true);
                }, "斧踢或镰踢结算与冰石外放");
                timeline.At(19.751, () =>
                {
                    _fight.Run(_fight.HouseOfLight(PartySnapshot.Capture(_fight.Party), Center, ConeHelper, Plan.Unmarked));
                    foreach (var slot in Plan.Unmarked)
                        _fight.Move(slot, Plan.IceMarkerArea(Center, slot),
                            21.52, 21.72, urgent: true);
                }, "无标四人引导扇形后躲开首批冰柱");
                timeline.At(20.355, () => _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 252), "恢复镜像模型属性");
                timeline.At(21.288, ResolveIceMarkers, "四枚冰石结算并锁定冰花中心");
            }

            private void AddKnockbackAndFlowers(SimTimeline timeline)
            {
                timeline.At(21.74, () => _fight.MoveAll(slot => Plan.KnockbackArea(Center, slot % 2), 25.24, KnockbackStartsAt, urgent: true),
                    "两队就位等待天降一击");
                timeline.At(22.432, () => _reflection.ExecuteActionEffectAt(0x9CEF, new Vector3(ReflectionPoint, -0.0153f),
                    Heading(ReflectionPoint - Center), 1.6f), "镜像跳向场边");
                timeline.At(23.138, () => _reflection.ActorSetPos(new Vector3(ReflectionPoint, 0), ReflectionHeading), "镜像跳跃落地");
                timeline.At(23.361, () =>
                {
                    _fight.Oracle.ActorSetPos(new Vector3(Center, 0), 0);
                    CastFlowers();
                }, "本体回到场中，四组冰针开始读条");
                timeline.At(23.447, () => Play(_fight.Oracle, 7747), "本体在中央显现");
                timeline.At(23.768, () => CastFrom(_reflection, 0x9D10, 4.7f, ReflectionHeading), "罪神圣读条");
                timeline.At(25.582, HeavenlyStrike, "天降一击结算");
                timeline.At(28.387, ResolveFlowers, "四个冰花的圆形和双十字结算");
            }

            private void AddStackTrail(SimTimeline timeline)
            {
                timeline.At(28.43, () => MoveToStack(0), "冰花结算后开始第一轮分摊走位");
                timeline.At(28.704, () => Play(_fight.Oracle, 4581), "本体再次隐去");
                timeline.At(28.758, () => _reflection.ExecuteActionEffect(0x9D10, _reflection, ReflectionHeading, null, 1, 3.1f),
                    "罪神圣读条结算");
                timeline.At(30.761, () => _fight.Oracle.ActorSetPos(new Vector3(GazePoint, 0), Heading(Center - GazePoint)), "本体移至场边");
                timeline.At(30.847, () => Play(_fight.Oracle, 7747), "本体在场边重新出现");
                for (var i = 0; i < StackRounds; i++)
                {
                    var wave = i;
                    timeline.At(StackHitAt(wave), () =>
                    {
                        ResolveStack(wave);
                        if (wave < StackRounds - 1)
                            MoveToStack(wave + 1);
                        else
                            _fight.MoveAll(slot => Plan.IceStartArea(Center, slot % 2), 37.60, FirstSlideAt, urgent: true);
                    }, "双四人罪神圣及冰圈");
                }
            }

            private void AddGazeAndIce(SimTimeline timeline)
            {
                timeline.At(32.916, SwapArmor, "冰光形态交换及副本台词");
                timeline.At(37.73, FaceAway, "假队友背对闪光护甲");
                timeline.At(GazeAt, ArmorEffects, "冰霜护甲和闪光护甲结算");
                timeline.At(IceFloorAt, () =>
                {
                    _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 240);
                    _fight.Map(23, 32, 16);
                }, "场地结冰");
                timeline.At(38.282, () =>
                {
                    for (var slot = 0; slot < 8; slot++)
                        _fight.Status(slot, ThinIce, 12, 0x140);
                }, "施加冰面状态");
                // 180° 同轴时本体脚下可能有冰圈，跳过读条前的随机穿场。
                if (!Plan.ReflectionOnKnockbackAxis)
                    timeline.At(IceFloorAt + 1, () => TryExtraSlide(FirstSlideAt), "冰面生成一秒后的随机提前滑冰");
            }

            private void AddIcicles(SimTimeline timeline, int first, int count, double castAt, double hitAt)
            {
                timeline.At(castAt, () =>
                {
                    for (var i = first; i < first + count; i++)
                    {
                        var center = Center + P21钻石星尘Plan.At(Plan.IcicleDirections[i], 16);
                        CastFrom(_fight.Helper(i, center, Heading(Center - center)), 0x9D06, 8.7f, ground: center);
                    }
                }, "冰柱冲击预兆");
                timeline.At(hitAt, () => _fight.Run(Enumerable.Range(first, count).Select(index =>
                {
                    var position = Center + P21钻石星尘Plan.At(Plan.IcicleDirections[index], 16);
                    return _fight.HitArea("冰柱冲击", ActionArea.Circle(10), position, 0, result =>
                        _fight.GroundEffect(index, 0x9D06, position, result.HitActors), Array.Empty<int>());
                })), "冰柱冲击结算");
            }

            private void ResolveIceMarkers()
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                _fight.Run(Plan.Marked.Select((slot, i) =>
                {
                    var position = _flowers[i] = snapshot.Get(slot).Position;
                    var mechanic = _fight.HitArea("冰石分散", FlowerCircle, position, 0, result =>
                    {
                        _fight.GroundEffect(ConeHelper + i, 0x9D07, position, result.HitActors);
                        _fight.Vulnerability(result, _fight.Helpers[ConeHelper + i], 1.956f);
                    }, new[] { slot });
                    mechanic.LockedSnapshot = snapshot;
                    return (SimMechanic)mechanic;
                }));
            }

            private void CastFlowers()
            {
                for (var i = 0; i < _flowers.Length; i++)
                {
                    var index = FlowerHelper + 3 * i;
                    for (var shape = 0; shape < 3; shape++)
                        CastFrom(_fight.Helper(index + shape, _flowers[i], shape == 2 ? -(float)Math.PI / 4 : 0),
                            shape == 0 ? 0x9D08u : 0x9D09u, 4.7f);
                }
            }

            private void ResolveFlowers()
            {
                _fight.Run(Enumerable.Range(0, 12).Select(i =>
                {
                    var helper = _fight.Helpers[FlowerHelper + i];
                    var heading = i % 3 == 2 ? -(float)Math.PI / 4 : 0;
                    var position = _flowers[i / 3];
                    var circle = i % 3 == 0;
                    return _fight.HitArea("冰针", circle ? FlowerCircle : FlowerCross, position, heading, result =>
                        helper.ExecuteActionEffect(circle ? 0x9D08u : 0x9D09u, helper, heading, result.HitActors, 8, 2.1f),
                        Array.Empty<int>());
                }));
            }

            private void HeavenlyStrike()
            {
                _fight.Oracle.ExecuteActionEffect(0x9D0F, _fight.Oracle, 0, _fight.Party, 8, 3.1f);
                // 效果项 0x1F 指向 Knockback 156；本机在结算包后约 0.79 秒开始位移。
                SimKnockback.Resolve(_fight.Scheduler, _fight.Party, KnockbackEffect.RadialFromRow(Center, HeavenlyStrikeKnockback),
                    movementAt: _fight.Origin + KnockbackStartsAt,
                    // 强制移动开始后排队，队友实际落地就能向场边躲冰花。
                    afterMovement: () => _fight.MoveAll(slot => Plan.FlowerDodgeArea(Center, slot % 2), 28.24, 28.42, urgent: true),
                    name: "天降一击径向击退");
            }

            private void MoveToStack(int wave)
                => _fight.MoveAll(slot => Plan.StackArea(Center, slot % 2, wave),
                    StackHitAt(wave) - 0.12, StackHitAt(wave) + 0.02, urgent: true);

            private void ResolveStack(int wave)
            {
                var snapshot = PartySnapshot.Capture(_fight.Party);
                _fight.Run(Enumerable.Range(0, 2).Select(i =>
                {
                    var group = Enumerable.Range(0, 4).Select(index => i + 2 * index).ToArray();
                    var position = snapshot.Get(2 + i).Position;
                    var mechanic = _fight.HitArea("罪神圣四人分摊", ActionArea.Circle(6), position, 0, result =>
                    {
                        _fight.GroundEffect(ConeHelper + i, 0x9D11, position, result.HitActors, 1.6f);
                        _fight.Vulnerability(result, _fight.Helpers[ConeHelper + i], 0.956f);
                        _fight.AddIcePuddle(2 * wave + i, position, 6, PuddleSpawnAt(wave), PuddleFadeAt(wave), PuddleRemovalAt(wave));
                    }, group);
                    mechanic.LockedSnapshot = snapshot;
                    return (SimMechanic)mechanic;
                }));
            }

            private void SwapArmor()
            {
                _reflection.ActorControl(ActorControlCategory.StanceChange, 17);
                _fight.Oracle.ActorControl(ActorControlCategory.StanceChange, 44);
                _fight.Oracle.ActorControl(ActorControlCategory.SetModelAttributes, 252);
                _fight.EntityManager.SetStatus(_reflection, WeaponState, 17.4f, 0x21C, _reflection);
                _fight.EntityManager.SetStatus(_fight.Oracle, WeaponState, 17.4f, 0x222, _fight.Oracle);
                if (_fight._host.ZoneService.ContentDirectorId == 0x800375BF)
                    _fight.Party[_fight.Self].ActorControlSelf(ActorControlCategory.DirectorUpdate, 0x800375BF, 0x80000027,
                        4, 2, 12809, _fight.Oracle.Id);
            }

            private void FaceAway()
            {
                for (var slot = 0; slot < 8; slot++)
                {
                    if (slot == _fight.Self)
                        continue;
                    var actor = _fight.Party[slot];
                    actor.SuspendBehavior(0.65);
                    actor.ActorMove(actor.Pos3D, Heading(actor.Pos - GazePoint), 0);
                }
            }

            private void ArmorEffects()
            {
                var frost = _fight.Helper(22, ReflectionPoint, ReflectionHeading);
                frost.ExecuteActionEffect(0x9CF8, frost, ReflectionHeading, null, 1, 2.1f);
                var look = _fight.Helper(23, GazePoint, Heading(Center - GazePoint));
                var hit = PartySnapshot.Capture(_fight.Party).Members.Where(player =>
                    RelativityLogic.FacingGaze(player.Actor, player.Position, GazePoint)).ToArray();
                look.ExecuteActionEffect(0x9CF9, look, look.Heading, hit.Select(player => player.Actor), 8, 2.1f);
                if (hit.Length == 0)
                    return;
                _fight.Scheduler.ScheduleRelativeToNode(0.237, () =>
                {
                    foreach (var player in hit)
                    {
                        _fight.RefreshStatus(player.Slot, GazeStun, 10, look);
                        player.Actor.SuspendBehavior(10);
                    }
                    _fight.ApplyDamageDown(hit.Select(player => player.Slot), look);
                }, "闪光护甲施加眩晕和伤害降低", finishOnMechanicFailure: true);
                _fight.Report(MechanicError.Rule("闪光护甲", hit.Select(player => player.Slot), "结算时面朝了闪光护甲。"));
            }

            private void AddSwords(SimTimeline timeline)
            {
                var front = Plan.FrontFirst;
                var firstAction = front ? 0x9D01u : 0x9D02u;
                var secondAction = front ? 0x9D04u : 0x9D03u;
                timeline.At(41.020, () =>
                {
                    _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 243);
                    _fight.LogNpcYell(_reflection.Id, 13554, front ? (ushort)18189 : (ushort)18190);
                }, "双剑技喊话与模型属性");
                timeline.At(41.083, () =>
                {
                    CastFrom(_reflection, firstAction, 3.2f, SwordHeading(front));
                    Voice(_reflection, front ? 8205521u : 8205522u);
                    Play(_fight.Oracle, 4581);
                }, "双剑技第一击读条，本体隐去");
                timeline.At(FirstSlideAt, () => SlideParty(front, SecondSlideAt), "滑到双剑第一击的安全侧");
                timeline.At(FirstSword, () => ResolveSword(firstAction, front, 2.1f), "双剑技第一击");
                timeline.At(SecondSlideAt, () => SlideParty(Plan.SecondSafeBehind, BossAppearsAt), "滑到双剑第二击的安全侧");
                timeline.At(45.291, () => _reflection.ActorControl(ActorControlCategory.SetModelState, 5), "双剑技第二击蓄力姿态");
                timeline.At(46.595, () => _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 240), "恢复双剑模型属性");
                timeline.At(SecondSword, () =>
                {
                    ResolveSword(secondAction, !front, 3.1f);
                    TryExtraSlide(BossAppearsAt, walkBack: true);
                }, "双剑技第二击及随机额外滑冰");
                timeline.At(47.404, () => _reflection.ActorControl(ActorControlCategory.SetModelState, 0), "双剑技姿态恢复");
                timeline.At(49.712, () =>
                {
                    Play(_reflection, 4580);
                    _fight.EntityManager.SetEnemyListVisibility(_reflection, false);
                }, "镜像退场");
                timeline.At(50.139, () =>
                {
                    for (var slot = 0; slot < 8; slot++)
                        _fight.ClearStatus(slot, ThinIce);
                    _fight.EntityManager.RemoveStatus(_reflection, WeaponState);
                    _fight.EntityManager.RemoveStatus(_fight.Oracle, WeaponState);
                    _fight.Map(23, 0x40, 1);
                    _fight.Oracle.ActorSetPos(new Vector3(Center, 0), (float)Math.PI);
                    _fight.Oracle.SetTargetable(true);
                }, "恢复普通地面、本体归位并可选中");
                timeline.At(BossAppearsAt, () =>
                {
                    Play(_fight.Oracle, 7747);
                    if (!_fight.Oracle.Entity.IsTargetable)
                        _fight.Oracle.SetTargetable(true);
                    _bossAppeared = true;
                    for (var slot = 0; slot < 8; slot++)
                        if (!_sliding[slot])
                            MoveToRay(slot);
                }, "本体显现及同侧神圣射线站位");
                timeline.At(51.0, () => _fight.EntityManager.Delete(_reflection), "回收巫女的镜像");
            }

            private float SwordHeading(bool front) => front ? ReflectionHeading : Heading(ReflectionPoint - Center);

            private void ResolveSword(uint action, bool front, float animationLock)
                => _fight.Run(_fight.HitArea("双剑技", ActionArea.Cone(front ? 30 : 40, front ? 270 : 90), ReflectionPoint, SwordHeading(front),
                    result => _reflection.ExecuteActionEffect(action, _reflection, ReflectionHeading, result.HitActors, 8, animationLock),
                    Array.Empty<int>()));

            private bool CanSlide(int slot)
            {
                if (slot == _fight.Self || _sliding[slot])
                    return false;
                var actor = _fight.Party[slot];
                return actor.IsActive && !actor.MovementLocked && actor.Entity.HP != 0;
            }

            private void TryExtraSlide(double holdUntil, bool walkBack = false)
            {
                if (_fight.Random.Next(3) != 0)
                    return;
                var candidates = Enumerable.Range(0, 8).Where(CanSlide).ToArray();
                if (candidates.Length == 0)
                    return;
                var slot = candidates[_fight.Random.Next(candidates.Length)];
                var behind = Plan.IsBehind(Center, _fight.Party[slot].Pos);
                if (walkBack)
                    _lateWalkBackSlot = slot;
                Slide(slot, !behind, holdUntil);
            }

            private void SlideParty(bool behind, double holdUntil)
            {
                foreach (var slot in Enumerable.Range(0, 8).Where(CanSlide))
                {
                    var actor = _fight.Party[slot];
                    if (Plan.IsBehind(Center, actor.Pos) == behind)
                        actor.SuspendBehavior(Math.Max(0, holdUntil - _fight.Now));
                    else
                        Slide(slot, behind, holdUntil);
                }
            }

            private void Slide(int slot, bool behind, double holdUntil)
            {
                var actor = _fight.Party[slot];
                actor.SuspendBehavior(Math.Max(SlideDuration, holdUntil - _fight.Now));
                var destination = new Vector3(Plan.IceArea(Center, slot, behind).Sample(_fight.Random), actor.Pos3D.Z);
                var heading = Heading(new Vector2(destination.X, destination.Y) - actor.Pos);
                var xy = (uint)PacketCodec.EncodeUShortCoord(destination.X) << 16 | PacketCodec.EncodeUShortCoord(destination.Z);
                var zh = (uint)PacketCodec.EncodeUShortCoord(destination.Y) << 16 | PacketCodec.EncodeUShortHeading(heading);
                // 网络队友沿回放的 0xDF / 0xEF 分支播放滑冰；本机由冰面状态保留玩家控制。
                _sliding[slot] = true;
                actor.ActorControl(ActorControlCategory.PlayRemoteTimelineMove, xy, zh, 1, 3788);
                _fight.Scheduler.ScheduleAfter(SlideDuration, () =>
                {
                    _sliding[slot] = false;
                    if (!actor.IsActive)
                        return;
                    actor.ActorControl(ActorControlCategory.FinishTimelineMoveAtServerPosition);
                    actor.ActorSetPos(destination, heading);
                    if (!_fight.Scheduler.IsDrainingFailure && _bossAppeared)
                        MoveToRay(slot);
                }, "滑冰落点同步", finishOnMechanicFailure: true);
            }

            private void MoveToRay(int slot)
            {
                if (slot == _fight.Self)
                    return;
                var actor = _fight.Party[slot];
                if (!actor.IsActive || actor.Entity.HP == 0)
                    return;
                var melee = SimJobActions.For(actor.Job)?.Melee == true;
                if (melee && slot != _lateWalkBackSlot)
                    actor.GapCloseForMechanic(_fight.Origin + 58.2);
                else
                    _fight.Move(slot, Plan.RayGatherArea(Center, _fight.Oracle.Entity.HitboxRadius), 55.8, 58.2, urgent: true);
            }

            private void AddRay(SimTimeline timeline)
            {
                timeline.At(52.409, () => CastFrom(_fight.Oracle, 0x9D12, 4.7f), "神圣射线读条");
                timeline.At(57.368, () => _fight.Oracle.ExecuteActionEffect(0x9D12, _fight.Oracle, null, null, 1, 0.4f), "神圣射线读条结算");
                timeline.At(57.942, () =>
                {
                    // 射线特效跟随本体模型。按回放在结算前同步转向，再用同一朝向判定。
                    _rayHeading = Heading(_fight.Party[0].JudgementPosition - Center);
                    _fight.Oracle.ActorMove(_fight.Oracle.Pos3D, _rayHeading, 90);
                }, "神圣射线锁定面向");
                timeline.At(58.068, () =>
                    _fight.Run(_fight.HitArea("神圣射线直线分摊", ActionArea.Rectangle(65, 6), Center, _rayHeading,
                        result => _fight.Oracle.ExecuteActionEffect(0x9D13, _fight.Oracle, _rayHeading, result.HitActors, 8, 3.1f),
                        Enumerable.Range(0, 8).ToArray())), "神圣射线直线分摊结算");
            }
        }
    }
}
