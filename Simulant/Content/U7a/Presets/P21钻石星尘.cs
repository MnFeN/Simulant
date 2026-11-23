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
    [PhaseSelectionOrder(2.1)]
    internal sealed class P21钻石星尘 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P2-1 钻石星尘";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 22);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(DiamondDustLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "暂未完成。\n\n钢铁月环 + 扇形/冰花，击退后分摊放圈，背对、滑冰、双剑技。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase> { U7aExerciseOptions.Player() };
    }

    internal sealed class DiamondDustLogic : U7aP2ExerciseLogic
    {
        private const ushort HeavenlyStrikeKnockback = 156;
        private const ushort ThinIce = 911;
        private const ushort WeaponState = 2273;
        private const ushort GazeStun = 4163;
        private const int FlowerHelper = 8;
        private const int ConeHelper = 20;
        private const double GazeAt = 38.040;
        private const double FirstSword = 44.529;
        private const double SecondSword = 46.654;
        private static readonly int[][] Groups = { new[] { 0, 2, 4, 6 }, new[] { 1, 3, 5, 7 } };
        private static readonly double[] StackHits = { 29.653, 31.210, 32.774, 34.332 };
        private static readonly double[] PuddleSpawns = { 30.538, 32.140, 33.749, 35.362 };
        private static readonly double[] PuddleFades = { 41.269, 42.800, 44.412, 45.953 };
        private static readonly double[] PuddleRemovals = { 44.579, 46.181, 47.783, 48.982 };
        private static readonly ActionArea FlowerCross = ActionArea.Cross(40, 5);
        private static readonly ActionArea FlowerCircle = ActionArea.Circle(5);
        private readonly Vector2[] _flowers = new Vector2[4];
        private P21钻石星尘Plan _plan;
        private SimActor _reflection;
        private Vector2 ReflectionPoint => Center + P21钻石星尘Plan.At(_plan.Reflection, 10);
        private Vector2 GazePoint => Center + P21钻石星尘Plan.At(_plan.Gaze, 20);
        private float ReflectionHeading => Heading(Center - ReflectionPoint);
        protected override double OpeningLeadIn => 0.5;

        public DiamondDustLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 24)
        {
        }

        protected override void PreparePlan()
        {
            _plan = new P21钻石星尘Plan(Random.Next(2) == 0, Random.Next(2) == 0, Random.Next(2) == 0,
                Random.Next(4), Random.Next(8), Random.Next(8));
        }

        protected override SimPosition InitialPartyPosition(int slot) => Spot(Center + P21钻石星尘Plan.At(slot, 4), 0.25f);

        protected override void PrepareAdditionalActors(Action ready)
            => SpawnMechanicActor(17824, 13554, 4375, 0xA55BF9, Center + new Vector2(0, 30), (float)Math.PI, actor =>
            {
                _reflection = actor;
                ready();
            });

        protected override void SchedulePrelude()
        {
            BeforeOpening(-0.5, () =>
            {
                BeginIceBoss();
                Oracle.ActorSetPos(new Vector3(Center + new Vector2(0, 5), 0), (float)Math.PI);
            }, "冰巫女仇恨及初始形态");
            BeforeOpening(-0.455, AutoAttack, "冰巫女普攻");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            AddAutos(timeline, 52.290, 60.400, 63.388);
            timeline.At(0, () => CastFrom(Oracle, 0x9CF4, 2.7f), "镜中显影读条");
            timeline.At(2.923, () => Oracle.ExecuteActionEffect(0x9CF4, Oracle, null, null, 1, 2.1f), "镜中显影结算");
            timeline.At(5.020, () => Map(0, 2, 1), "镜中显影场地变化");
            timeline.At(5.091, () =>
            {
                CastFrom(Oracle, 0x9D05, 4.7f);
                Voice(Oracle, 8205523);
            }, "钻石星尘读条及语音");
            timeline.At(6.061, () =>
            {
                _reflection.ActorSetPos(new Vector3(Center, 0), (float)Math.PI);
                Play(_reflection, 4566);
                EntityManager.SetEnemyListVisibility(_reflection, true);
            }, "巫女的镜像显现");
            timeline.At(9.111, () => Map(0, 8, 4), "镜中显影场效结束");
            timeline.At(10.079, () => Oracle.ExecuteActionEffect(0x9D05, Oracle, null, Party, 8, 2.6f), "钻石星尘全屏伤害");
            AddIcicles(timeline, 0, 2, 12.717, 21.708);
            AddIcicles(timeline, 2, 4, 16.713, 25.705);
            AddIcicles(timeline, 6, 2, 20.786, 29.699);
            var kick = _plan.Axe ? 0x9D0Au : 0x9D0Bu;
            timeline.At(12.979, () =>
            {
                CastFrom(_reflection, kick, 5.7f);
                Yell(_reflection, _plan.Axe ? 8205519u : 8205520u, 13554, _plan.Axe ? (ushort)18187 : (ushort)18188);
            }, "分身斧踢或镰踢读条与喊话");
            timeline.At(13.165, () =>
            {
                Oracle.SetTargetable(false);
                foreach (var slot in _plan.Marked)
                    Icon(slot, 345);
                MoveAll(slot => Spot(Center + _plan.InitialBait(slot, false), 0.12f), 18.35, 19.78);
            }, "本体不可选中，分配冰石头标和扇形站位");
            timeline.At(13.247, () => Play(Oracle, 4581), "本体隐去");
            timeline.At(14.018, () => _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 249), "回旋踢模型属性");
            timeline.At(18.947, () =>
            {
                Kick(_reflection, kick, _plan.Axe ? ActionArea.Circle(16) : KickDonut);
                foreach (var slot in _plan.Marked)
                    Move(slot, Spot(Center + _plan.InitialBait(slot, true), 0.12f), 21.02, 21.30, urgent: true);
            }, "斧踢或镰踢结算与冰石外放");
            timeline.At(19.751, () =>
            {
                Run(HouseOfLight(PartySnapshot.Capture(Party), Center, ConeHelper, _plan.Unmarked));
                foreach (var slot in _plan.Unmarked)
                    Move(slot, Spot(Center + Vector2.Normalize(_plan.InitialBait(slot, false)) * (_plan.Axe ? 4.75f : 1.7f), 0.1f),
                        21.52, 21.72, urgent: true);
            }, "无标四人引导扇形后躲开首批冰柱");
            timeline.At(20.355, () => _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 252), "恢复镜像模型属性");
            timeline.At(21.288, ResolveIceMarkers, "四枚冰石结算并锁定冰花中心");
            timeline.At(21.74, () => MoveAll(slot => Spot(Center + _plan.KnockbackStart(slot % 2), 0.1f), 25.24, 25.59, urgent: true),
                "两队就位等待天降一击");
            timeline.At(22.432, () => _reflection.ExecuteActionEffectAt(0x9CEF, new Vector3(ReflectionPoint, -0.0153f),
                Heading(ReflectionPoint - Center), 1.6f), "镜像跳向场边");
            timeline.At(23.138, () => _reflection.ActorSetPos(new Vector3(ReflectionPoint, 0), ReflectionHeading), "镜像跳跃落地");
            timeline.At(23.361, () =>
            {
                Oracle.ActorSetPos(new Vector3(Center, 0), 0);
                CastFlowers();
            }, "本体回到场中，四组冰针开始读条");
            timeline.At(23.447, () => Play(Oracle, 7747), "本体在中央显现");
            timeline.At(23.768, () => CastFrom(_reflection, 0x9D10, 4.7f, ReflectionHeading), "罪神圣读条");
            timeline.At(25.582, HeavenlyStrike, "天降一击结算");
            timeline.At(28.387, ResolveFlowers, "四个冰花的圆形和双十字结算");
            timeline.At(28.43, () => MoveToStack(0), "冰花结算后开始第一轮分摊走位");
            timeline.At(28.704, () => Play(Oracle, 4581), "本体再次隐去");
            timeline.At(28.758, () => _reflection.ExecuteActionEffect(0x9D10, _reflection, ReflectionHeading, null, 1, 3.1f),
                "罪神圣读条结算");
            timeline.At(30.761, () => Oracle.ActorSetPos(new Vector3(GazePoint, 0), Heading(Center - GazePoint)), "本体移至场边");
            timeline.At(30.847, () => Play(Oracle, 7747), "本体在场边重新出现");
            for (var i = 0; i < StackHits.Length; i++)
            {
                var wave = i;
                timeline.At(StackHits[wave], () =>
                {
                    ResolveStack(wave);
                    if (wave < StackHits.Length - 1)
                        MoveToStack(wave + 1);
                    else
                        MoveAll(slot => Spot(Center + _plan.IceOffset(slot, _plan.StartsBehind(slot % 2)), 0.13f),
                            PuddleSpawns[wave], 37.76, urgent: true);
                }, "双四人罪神圣及冰圈");
            }
            timeline.At(32.916, SwapArmor, "冰光形态交换及副本台词");
            timeline.At(37.73, FaceAway, "假队友背对闪光护甲");
            timeline.At(GazeAt, ArmorEffects, "冰霜护甲和闪光护甲结算");
            timeline.At(38.162, () =>
            {
                _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 240);
                Map(23, 32, 16);
            }, "场地结冰");
            timeline.At(38.282, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    Status(slot, ThinIce, 12, 0x140);
            }, "施加冰面状态");
            AddSwords(timeline);
            AddRay(timeline);
            return timeline;
        }

        private void AddIcicles(SimTimeline timeline, int first, int count, double castAt, double hitAt)
        {
            timeline.At(castAt, () =>
            {
                for (var i = first; i < first + count; i++)
                {
                    var center = Center + P21钻石星尘Plan.At(_plan.IcicleDirections[i], 16);
                    CastFrom(Helper(i, center, Heading(Center - center)), 0x9D06, 8.7f, ground: center);
                }
            }, "冰柱冲击预兆");
            timeline.At(hitAt, () => Run(Enumerable.Range(first, count).Select(index =>
            {
                var position = Center + P21钻石星尘Plan.At(_plan.IcicleDirections[index], 16);
                return HitArea("冰柱冲击", ActionArea.Circle(10), position, 0, result =>
                    GroundEffect(index, 0x9D06, position, result.HitActors), Array.Empty<int>());
            })), "冰柱冲击结算");
        }

        private void ResolveIceMarkers()
        {
            var snapshot = PartySnapshot.Capture(Party);
            Run(_plan.Marked.Select((slot, i) =>
            {
                var position = _flowers[i] = snapshot.Get(slot).Position;
                var mechanic = HitArea("冰石分散", FlowerCircle, position, 0, result =>
                {
                    GroundEffect(ConeHelper + i, 0x9D07, position, result.HitActors);
                    Vulnerability(result, Helpers[ConeHelper + i], 1.956f);
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
                    CastFrom(Helper(index + shape, _flowers[i], shape == 2 ? -(float)Math.PI / 4 : 0),
                        shape == 0 ? 0x9D08u : 0x9D09u, 4.7f);
            }
        }

        private void ResolveFlowers()
        {
            Run(Enumerable.Range(0, 12).Select(i =>
            {
                var helper = Helpers[FlowerHelper + i];
                var heading = i % 3 == 2 ? -(float)Math.PI / 4 : 0;
                var position = _flowers[i / 3];
                var circle = i % 3 == 0;
                return HitArea("冰针", circle ? FlowerCircle : FlowerCross, position, heading, result =>
                    helper.ExecuteActionEffect(circle ? 0x9D08u : 0x9D09u, helper, heading, result.HitActors, 8, 2.1f),
                    Array.Empty<int>());
            }));
        }

        private void HeavenlyStrike()
        {
            Oracle.ExecuteActionEffect(0x9D0F, Oracle, 0, Party, 8, 3.1f);
            // 效果项 0x1F 指向 Knockback 156；本机在结算包后约 0.79 秒开始位移。
            SimKnockback.Resolve(Scheduler, Party, KnockbackEffect.RadialFromRow(Center, HeavenlyStrikeKnockback),
                movementAt: Origin + 26.372, name: "天降一击径向击退");
        }

        private void MoveToStack(int wave)
            => MoveAll(slot => Spot(Center + _plan.StackOffset(slot % 2, wave), 0.12f),
                wave == 0 ? StackHits[0] - 0.12 : PuddleSpawns[wave - 1], StackHits[wave] + 0.02, urgent: true);

        private void ResolveStack(int wave)
        {
            var snapshot = PartySnapshot.Capture(Party);
            Run(Groups.Select((group, i) =>
            {
                var position = snapshot.Get(2 + i).Position;
                var mechanic = HitArea("罪神圣四人分摊", ActionArea.Circle(6), position, 0, result =>
                {
                    GroundEffect(ConeHelper + i, 0x9D11, position, result.HitActors, 1.6f);
                    Vulnerability(result, Helpers[ConeHelper + i], 0.956f);
                    AddIcePuddle(2 * wave + i, position, 6, PuddleSpawns[wave], PuddleFades[wave], PuddleRemovals[wave]);
                }, group);
                mechanic.LockedSnapshot = snapshot;
                return (SimMechanic)mechanic;
            }));
        }

        private void SwapArmor()
        {
            _reflection.ActorControl(ActorControlCategory.StanceChange, 17);
            Oracle.ActorControl(ActorControlCategory.StanceChange, 44);
            Oracle.ActorControl(ActorControlCategory.SetModelAttributes, 252);
            EntityManager.SetStatus(_reflection, WeaponState, 17.4f, 0x21C, _reflection);
            EntityManager.SetStatus(Oracle, WeaponState, 17.4f, 0x222, Oracle);
            if (_host.ZoneService.ContentDirectorId == 0x800375BF)
                Party[Self].ActorControlSelf(ActorControlCategory.DirectorUpdate, 0x800375BF, 0x80000027,
                    4, 2, 12809, Oracle.Id);
        }

        private void FaceAway()
        {
            for (var slot = 0; slot < 8; slot++)
            {
                if (slot == Self)
                    continue;
                var actor = Party[slot];
                actor.SuspendBehavior(0.65);
                actor.ActorMove(actor.Pos3D, Heading(actor.Pos - GazePoint), 0);
            }
        }

        private void ArmorEffects()
        {
            var frost = Helper(22, ReflectionPoint, ReflectionHeading);
            frost.ExecuteActionEffect(0x9CF8, frost, ReflectionHeading, null, 1, 2.1f);
            var look = Helper(23, GazePoint, Heading(Center - GazePoint));
            var hit = PartySnapshot.Capture(Party).Members.Where(player =>
                RelativityLogic.FacingGaze(player.Actor, player.Position, GazePoint)).ToArray();
            look.ExecuteActionEffect(0x9CF9, look, look.Heading, hit.Select(player => player.Actor), 8, 2.1f);
            if (hit.Length == 0)
                return;
            Scheduler.ScheduleRelativeToNode(0.237, () =>
            {
                foreach (var player in hit)
                {
                    RefreshStatus(player.Slot, GazeStun, 10, look);
                    player.Actor.SuspendBehavior(10);
                }
                ApplyDamageDown(hit.Select(player => player.Slot), look);
            }, "闪光护甲施加眩晕和伤害降低", finishOnMechanicFailure: true);
            Report(MechanicError.Rule("闪光护甲", hit.Select(player => player.Slot), "结算时面朝了闪光护甲。"));
        }

        private void AddSwords(SimTimeline timeline)
        {
            var front = _plan.FrontFirst;
            var firstAction = front ? 0x9D01u : 0x9D02u;
            var secondAction = front ? 0x9D04u : 0x9D03u;
            timeline.At(41.020, () =>
            {
                _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 243);
                LogNpcYell(_reflection.Id, 13554, front ? (ushort)18189 : (ushort)18190);
            }, "双剑技喊话与模型属性");
            timeline.At(41.083, () =>
            {
                CastFrom(_reflection, firstAction, 3.2f, SwordHeading(front));
                Voice(_reflection, front ? 8205521u : 8205522u);
                Play(Oracle, 4581);
            }, "双剑技第一击读条，本体隐去");
            timeline.At(41.49, () => SlideParty(front, first: true), "滑到双剑第一击的安全侧");
            timeline.At(FirstSword, () => ResolveSword(firstAction, front, 2.1f), "双剑技第一击");
            timeline.At(44.59, () => SlideParty(!front, first: false), "滑到双剑第二击的安全侧");
            timeline.At(45.291, () => _reflection.ActorControl(ActorControlCategory.SetModelState, 5), "双剑技第二击蓄力姿态");
            timeline.At(46.595, () => _reflection.ActorControl(ActorControlCategory.SetModelAttributes, 240), "恢复双剑模型属性");
            timeline.At(SecondSword, () => ResolveSword(secondAction, !front, 3.1f), "双剑技第二击");
            timeline.At(47.404, () => _reflection.ActorControl(ActorControlCategory.SetModelState, 0), "双剑技姿态恢复");
            timeline.At(49.712, () =>
            {
                Play(_reflection, 4580);
                EntityManager.SetEnemyListVisibility(_reflection, false);
            }, "镜像退场");
            timeline.At(50.139, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    ClearStatus(slot, ThinIce);
                EntityManager.RemoveStatus(_reflection, WeaponState);
                EntityManager.RemoveStatus(Oracle, WeaponState);
                Map(23, 0x40, 1);
                Oracle.ActorSetPos(new Vector3(Center, 0), (float)Math.PI);
                Oracle.SetTargetable(true);
            }, "恢复普通地面、本体归位并可选中");
            timeline.At(50.247, () => Play(Oracle, 7747), "本体显现");
            timeline.At(51.0, () => EntityManager.Delete(_reflection), "回收巫女的镜像");
        }

        private float SwordHeading(bool front) => front ? ReflectionHeading : Heading(ReflectionPoint - Center);

        private void ResolveSword(uint action, bool front, float animationLock)
            => Run(HitArea("双剑技", ActionArea.Cone(front ? 30 : 40, front ? 270 : 90), ReflectionPoint, SwordHeading(front),
                result => _reflection.ExecuteActionEffect(action, _reflection, ReflectionHeading, result.HitActors, 8, animationLock),
                Array.Empty<int>()));

        private void SlideParty(bool behind, bool first)
        {
            for (var slot = 0; slot < 8; slot++)
            {
                if (slot == Self || first && _plan.StartsBehind(slot % 2) == behind)
                    continue;
                var actor = Party[slot];
                if (actor.MovementLocked)
                    continue;
                var destination = new Vector3(Center + _plan.IceOffset(slot, behind), actor.Pos3D.Z);
                var heading = Heading(new Vector2(destination.X, destination.Y) - actor.Pos);
                var xy = (uint)PacketCodec.EncodeUShortCoord(destination.X) << 16 | PacketCodec.EncodeUShortCoord(destination.Z);
                var zh = (uint)PacketCodec.EncodeUShortCoord(destination.Y) << 16 | PacketCodec.EncodeUShortHeading(heading);
                // 网络队友沿回放的 0xDF / 0xEF 分支播放滑冰；本机由冰面状态保留玩家控制。
                actor.SuspendBehavior(1.8);
                actor.ActorControl(ActorControlCategory.PlayRemoteTimelineMove, xy, zh, 1, 3788);
                Scheduler.ScheduleAfter(1.73, () =>
                {
                    actor.ActorControl(ActorControlCategory.FinishTimelineMoveAtServerPosition);
                    actor.ActorSetPos(destination, heading);
                }, "滑冰落点同步");
            }
        }

        private void AddRay(SimTimeline timeline)
        {
            timeline.At(50.30, () => MoveAll(slot => Spot(Center + new Vector2(0, -7.5f), 0.34f), 55.2, 58.2, urgent: true), "神圣射线集合");
            timeline.At(52.409, () => CastFrom(Oracle, 0x9D12, 4.7f), "神圣射线读条");
            timeline.At(57.368, () => Oracle.ExecuteActionEffect(0x9D12, Oracle, null, null, 1, 0.4f), "神圣射线读条结算");
            timeline.At(58.068, () =>
            {
                var heading = Heading(Party[0].JudgementPosition - Center);
                Run(HitArea("神圣射线直线分摊", ActionArea.Rectangle(65, 6), Center, heading,
                    result => Oracle.ExecuteActionEffect(0x9D13, Oracle, heading, result.HitActors, 8, 3.1f),
                    Enumerable.Range(0, 8).ToArray()));
            }, "神圣射线直线分摊结算");
            timeline.At(63.55, Complete, "钻石星尘及神圣射线结束");
        }
    }
}
