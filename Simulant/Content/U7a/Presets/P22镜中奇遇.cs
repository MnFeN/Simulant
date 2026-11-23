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
    [PhaseSelectionOrder(2.2)]
    internal sealed class P22镜中奇遇 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P2-2 镜中奇遇";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 22);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(MirrorMirrorLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "暂未完成。\n\nT 拉 Boss去白镜子对侧，远程和治疗去黄镜子；然后去红镜子。最后双人分摊或全体分散。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            U7aExerciseOptions.Direction(nameof(MirrorMirrorLogic.BlueDirection), "白镜方位"),
            new CheckBoxOption(nameof(MirrorMirrorLogic.RangedLeft), "对称时远程去左", true, "相对白镜为北。非对称时远程去离白镜近的红镜。")
        };
    }

    internal sealed class MirrorMirrorLogic : U7aP2ExerciseLogic
    {
        private readonly SimActor[] _mirrors = new SimActor[3];
        private int _blue;
        private int _redNear;
        private int _redFar;
        private bool _spread;

        [SimOption] public ExerciseDirection BlueDirection { get; set; }
        [SimOption] public bool RangedLeft { get; set; }
        protected override double OpeningLeadIn => 0.82;

        public MirrorMirrorLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 8)
        {
        }

        protected override void PreparePlan()
        {
            _blue = BlueDirection == ExerciseDirection.Random ? Random.Next(8) : (int)BlueDirection;
            var midpoint = Enumerable.Range(0, 8).Where(dir => ArcDistance(_blue, dir) != 1).OrderBy(_ => Random.Next()).First();
            var left = (midpoint + 1) % 8;
            var right = (midpoint + 7) % 8;
            var leftDistance = ArcDistance(_blue, left);
            var rightDistance = ArcDistance(_blue, right);
            var farGoesLeft = leftDistance == rightDistance ? RangedLeft : leftDistance < rightDistance;
            _redFar = farGoesLeft ? left : right;
            _redNear = farGoesLeft ? right : left;
            _spread = Random.Next(2) == 0;
        }

        private static int ArcDistance(int a, int b) => Math.Min((a - b + 8) % 8, (b - a + 8) % 8);
        private static Vector2 AtDirection(int direction) => Direction(Math.PI + direction * Math.PI / 4);
        private static uint MapSlot(int direction) => (uint)((8 - direction) % 8 + 1);
        private static Vector2 MirrorPosition(int direction) => Center + AtDirection(direction) * 20;

        protected override SimPosition InitialPartyPosition(int slot) => Spot(Center + BanishOffset(slot, true) * 0.45f);

        protected override void PrepareAdditionalActors(Action ready)
        {
            var directions = new[] { _blue, _redNear, _redFar };
            var actorReady = WhenAllActorsReady(directions.Length, ready);
            for (var i = 0; i < directions.Length; i++)
            {
                var index = i;
                var position = MirrorPosition(directions[i]);
                SpawnMechanicActor(17825, 9317, 476, 0xA5743C, position, Heading(Center - position), actor =>
                {
                    _mirrors[index] = actor;
                    actorReady();
                });
            }
        }

        protected override void SchedulePrelude()
        {
            BeforeOpening(-0.82, BeginIceBoss, "冰巫女仇恨及初始形态");
            BeforeOpening(-0.789, AutoAttack, "冰巫女普攻");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            AddAutos(timeline, 5.234, 8.264, 17.306, 20.303, 23.311, 26.348, 34.386);
            timeline.At(0, () => CastFrom(Oracle, 0x9CF3, 2.7f), "镜中奇遇读条");
            timeline.At(2.991, () => Oracle.ExecuteActionEffect(0x9CF3, Oracle, null, null, 1, 2.1f), "镜中奇遇结算");
            timeline.At(5.082, ShowMirrors, "一蓝两红镜面出现");
            timeline.At(11.139, () =>
            {
                Oracle.FollowTopEnmity = EnmityFollowMode.Disabled;
                CastFrom(Oracle, 0x9D0B, 5.7f);
                MoveMirrorGroup(NearGroup, Oracle.Pos, MirrorPosition(_blue), false, 15.8, 18.02);
                Yell(Oracle, 8205520, 12809, 18188);
            }, "镰踢读条与喊话");
            timeline.At(12.124, () => Oracle.ActorControl(ActorControlCategory.SetModelAttributes, 249), "镰踢模型属性");
            timeline.At(17.107, () =>
            {
                Kick(Oracle, 0x9D0B, KickDonut);
                Map(MapSlot(_redNear), 0x800, 1);
                Map(MapSlot(_redFar), 0x800, 1);
            }, "首领月环及红镜蓄力");
            timeline.At(17.237, () =>
            {
                Kick(_mirrors[0], 0x9D0C, KickDonut);
                CastFrom(_mirrors[1], 0x9D0D, 9.7f);
                CastFrom(_mirrors[2], 0x9D0D, 9.7f);
            }, "蓝镜月环与红镜读条");
            timeline.At(17.807, () => Run(HouseOfLight(PartySnapshot.Capture(Party), _mirrors[0].Pos, 4, FarGroup)), "蓝镜四道扇形");
            timeline.At(17.944, () => Run(HouseOfLight(PartySnapshot.Capture(Party), Oracle.Pos, 0, NearGroup)), "首领四道扇形");
            timeline.At(18.476, () => Oracle.ActorControl(ActorControlCategory.SetModelAttributes, 252), "恢复首领模型属性");
            timeline.At(19.180, () =>
            {
                Map(MapSlot(_blue), 8, 4);
                EntityManager.SetEnemyListVisibility(_mirrors[0], false);
            }, "蓝镜退场");
            timeline.At(19.21, MoveToRedMirrors, "两组分别移动到红镜");
            timeline.At(27.148, ResolveRedDonuts, "两面红镜月环同时结算");
            timeline.At(27.825, () =>
            {
                var snapshot = PartySnapshot.Capture(Party);
                Run(HouseOfLight(snapshot, _mirrors[1].Pos, 4, NearGroup).Concat(HouseOfLight(snapshot, _mirrors[2].Pos, 0, FarGroup)));
            }, "两面红镜的八道扇形");
            timeline.At(29.224, () =>
            {
                Map(MapSlot(_redNear), 8, 4);
                Map(MapSlot(_redFar), 8, 4);
                EntityManager.SetEnemyListVisibility(_mirrors[1], false);
                EntityManager.SetEnemyListVisibility(_mirrors[2], false);
            }, "红镜退场");
            AddBanish(timeline, _spread, castAt: 28.259, effectAt: 33.265, circlesAt: 33.330);
            timeline.At(35.2, () =>
            {
                Oracle.FollowTopEnmity = EnmityFollowMode.Disabled;
                Complete();
            }, "镜中奇遇结束");
            return timeline;
        }

        private void ShowMirrors()
        {
            Map(MapSlot(_redNear), 0x200, 0x100);
            Map(MapSlot(_redFar), 0x200, 0x100);
            Map(MapSlot(_blue), 2, 1);
            foreach (var mirror in _mirrors)
                EntityManager.SetEnemyListVisibility(mirror, true);

            Oracle.FollowTopEnmity = EnmityFollowMode.MoveAndFace;
            // 假坦克沿蓝镜对侧外移拉怪。实际首领位置取决于一仇的走位。
            Move(0, Spot(Center - AtDirection(_blue) * 16.3f, 0.12f), 10.15, 11.139, urgent: true);
            var blue = MirrorPosition(_blue);
            MoveMirrorGroup(FarGroup, blue, Center, false, 15.8, 18.02);
        }

        private void MoveToRedMirrors()
        {
            var near = MirrorPosition(_redNear);
            var far = MirrorPosition(_redFar);
            MoveMirrorGroup(NearGroup, near, far, true, 26.7, 27.9);
            MoveMirrorGroup(FarGroup, far, near, true, 26.7, 27.9);
            Oracle.FollowTopEnmity = EnmityFollowMode.MoveAndFace;
        }

        private void MoveMirrorGroup(int[] group, Vector2 source, Vector2 other, bool bothRed, double arrive, double hold)
        {
            for (var i = 0; i < group.Length; i++)
                Move(group[i], Spot(BaitPosition(source, other, i, bothRed), 0.12f), arrive, hold, urgent: bothRed);
        }

        internal static Vector2 BaitPosition(Vector2 source, Vector2 other, int index, bool bothRed)
        {
            var direction = Center - source;
            if (direction.LengthSquared() < 0.01f)
                direction = other - source;

            var inward = Vector2.Normalize(direction);
            var offsets = bothRed ? new[] { -74, 79, -39, -4 } : new[] { -75, 75, -37, 37 };
            var side = bothRed && Vector2.Dot(new Vector2(inward.Y, -inward.X), other - source) < 0 ? -1 : 1;
            return source + Direction(Heading(inward) + side * offsets[index] * Math.PI / 180) * 3.05f;
        }

        private void ResolveRedDonuts()
        {
            Run(_mirrors.Skip(1).Select(actor => HitArea("红镜月环", KickDonut, actor.Pos, actor.Heading, result =>
                actor.ExecuteActionEffect(0x9D0D, actor, result.Heading, result.HitActors, 8, 2.1f), Array.Empty<int>())));
        }
    }
}
