using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.Data;
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
    [PhaseSelectionOrder(3.2)]
    internal sealed class P3启示 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P3 二运：启示";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 16);
        public override PhaseData Phase => U7a.P3;
        public override int Level => 100;
        public override Type SimLogicType => typeof(ApocalypseLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "从黑暗狂水点名开始，处理三轮四人分摊、碎灵一击、启示地火、八人分散及死刑击退。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new ComboBoxOption<ApocalypseSolution>(nameof(ApocalypseLogic.Solution), "分散与分摊的分组及优先级", ApocalypseSolution.SingleReverseD, new Map<ApocalypseSolution>
            {
                [ApocalypseSolution.DoubleCN] = "双分组：H12 > T12 > D1-4（MMW）",
                [ApocalypseSolution.DoubleHTDH] = "双分组：H1 > T12 > D1-4 > H2",
                [ApocalypseSolution.DoubleNA] = "双分组：H21 > T21 > D1-4（美服）",
                [ApocalypseSolution.SingleTHD] = "单分组：T12 > H12 > D1-4",
                [ApocalypseSolution.SingleReverseD] = "单分组：T12 > H12 > D4-1（MMW）",
                [ApocalypseSolution.SingleHTDH] = "单分组：H1 > T12 > D1-4 > H2",
                [ApocalypseSolution.SingleJP] = "单分组：H21 > T21 > D1-4（日服）",
                [ApocalypseSolution.SingleBalanced] = "单分组：自动分配每组双近双远（含头顶标点）"
            }),
            new CheckBoxOption(nameof(ApocalypseLogic.SwapSpreadPositions), "地火分散同组左右互换点位", false,
                "地火分散时，同组四个点位的分配在不同攻略中可能有所出入。\n\n" +
                "如果你认为地火分散时，同组近战两人或远程两人的点位与你的预期不同，可切换此选项令同组的两对站位互换。\n\n" +
                "注意，单分组攻略换位后存在灵性分配站位的弊端，不属于此选项范畴。比如此选项不会使换到另一组的近战挤走 T 的位置。"),
            new ComboBoxOption<ApocalypseReference>(nameof(ApocalypseLogic.HalfReference), "地火分散划分半场的方式", ApocalypseReference.GroupSafe, new Map<ApocalypseReference>
            {
                [ApocalypseReference.FirstFire] = "初始地火基准，然后反向找安全区（美服）",
                [ApocalypseReference.GroupSafe] = "三人安全区基准，即地火逆 45° 的安全区",
                [ApocalypseReference.DriverSafe] = "单人安全区基准，即地火垂直 90° 的安全区"
            }),
            new ComboBoxOption<ApocalypseHalf>(nameof(ApocalypseLogic.Half), "半场分界线", ApocalypseHalf.NorthToSouthwest, new Map<ApocalypseHalf>
            {
                [ApocalypseHalf.NorthToSouthwest] = "正北至西南为 MT 组（国/美服）",
                [ApocalypseHalf.NortheastToWest] = "东北至正西为 MT 组（日服）"
            }),
            new ComboBoxOption<BaitTank>(nameof(ApocalypseLogic.BaitTank), "死刑跳跃引导", BaitTank.MT,
                new Map<BaitTank> { [BaitTank.MT] = "MT", [BaitTank.ST] = "ST" }),
            new ComboBoxOption<ApocalypseDebuff>(nameof(ApocalypseLogic.Initial), "初始点名", ApocalypseDebuff.Random, new Map<ApocalypseDebuff>
            {
                [ApocalypseDebuff.Random] = "随机",
                [ApocalypseDebuff.None] = "无水",
                [ApocalypseDebuff.ShortWater] = "短水",
                [ApocalypseDebuff.MediumWater] = "中水",
                [ApocalypseDebuff.LongWater] = "长水"
            }),
            new CheckBoxOption(nameof(ApocalypseLogic.EqualGroups), "等概率分组代替等概率点名", true,
                "对于优先级靠近两侧的职能，等概率点名的真随机会导致很难练到换位的情况，故此推荐等概率分组的伪随机点名以便练习。\n\n" +
                "开启此选项后，会调整全队点名的生成算法，使玩家当前所选职能被分到两组的概率相等。\n\n" +
                "此选项对于优先级最高、最低的玩家无效。"),
            U7aExerciseOptions.Direction(nameof(ApocalypseLogic.StartDirection), "地火起始方向"),
            U7aExerciseOptions.Turn("地火旋转")
        };
    }

    internal sealed class ApocalypseLogic : U7aExerciseLogic
    {
        [SimOption] public ApocalypseSolution Solution { get; set; }
        [SimOption] public bool SwapSpreadPositions { get; set; }
        [SimOption] public ApocalypseReference HalfReference { get; set; }
        [SimOption] public ApocalypseHalf Half { get; set; }
        [SimOption] public BaitTank BaitTank { get; set; }
        [SimOption] public ApocalypseDebuff Initial { get; set; }
        [SimOption] public bool EqualGroups { get; set; }
        [SimOption] public ExerciseDirection StartDirection { get; set; }
        [SimOption] public ExerciseTurn Rotation { get; set; }
        private ApocalypsePlan _plan;
        private readonly List<EventObject> _lights = new List<EventObject>();
        private Vector2 _dance;
        private float _danceHeading;

        public ApocalypseLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void PreparePlan() => _plan = new ApocalypsePlan(Self, Initial, Solution, HalfReference, Half,
            StartDirection, Rotation, Random, GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(40289).EffectRange,
            BaitTank, SwapSpreadPositions, EqualGroups);

        protected override SimPosition InitialPartyPosition(int slot)
            => _plan.IsSingleGroup ? _plan.Preposition(slot) : base.InitialPartyPosition(slot);

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            timeline.At(0, () =>
            {
                for (var i = 0; i < 8; i++)
                    if (_plan.Orders[i] != 0)
                        Icon(i, 62);
                Oracle.Cast(40270, executeOnFinish: false);
                if (_plan.IsSingleGroup)
                    MoveAll(slot => Party[slot].Pos, 13.9, 15.65);
                else
                    MoveAll(_plan.InitialStack, 13.9, 15.65);
            }, "黑暗狂水读条并显示分摊标记");
            timeline.At(4.88, () => Oracle.ExecuteActionEffect(40270, Oracle), "黑暗狂水读条结束演出");
            timeline.At(4.98, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    if (_plan.Orders[slot] != 0)
                        TargetEffect(slot, 40272, slot);
            }, "延迟水点名演出");
            timeline.At(5.54, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    if (_plan.GroupMarkers[slot] is MarkerType marker)
                        MarkPartyMember(slot, marker);

                for (var i = 0; i < 8; i++)
                    if (_plan.Orders[i] != 0)
                    {
                        Status(i, 2461, _plan.Orders[i] == 1 ? 10 : _plan.Orders[i] == 2 ? 29 : 38);
                        // 普通分摊标记出现前，无名状态持续显示延迟水的头顶提示。
                        Status(i, 2458, _plan.Orders[i] == 1 ? 5 : _plan.Orders[i] == 2 ? 23 : 33);
                    }
                if (_plan.IsSingleGroup)
                    for (var slot = 0; slot < 8; slot++)
                        if (_plan.PositionOwners[slot] != slot)
                            Move(slot, _plan.InitialStack(slot), 13.9, 15.65);
            }, "赋予三组延迟水分摊");
            var waterMarks = new[] { 10.52, 28.52, 38.55 };
            timeline.AtEach(Enumerable.Range(0, waterMarks.Length), round => waterMarks[round], round =>
            {
                for (var group = 0; group < 2; group++)
                {
                    var target = _plan.Target(round, group);
                    ClearStatus(target, 2458);
                    Icon(target, 184);
                }
            }, _ => "延迟水分摊标记");
            timeline.At(8.05, () => Oracle.Cast(40296, target: Oracle), "启示读条");
            timeline.At(8.42, SpawnLights, "生成启示地火光点");
            timeline.At(12.77, () => Map(0x29, 0x20), "启示场地魔法阵");
            timeline.At(37.96, () => Map(0x29, 8), "启示地火场地效果结束");
            timeline.At(14.17, () => Oracle.Cast(40288, executeOnFinish: false), "碎灵一击读条");
            timeline.At(15.56, () => ResolveWater(0), "第一轮四人水分摊");
            timeline.At(15.61, () =>
            {
                var positions = _plan.SampleFirstJump(Random);
                MoveAll(slot => positions[slot], 17.32, 18.7, true);
            }, "水分摊后散开躲碎灵一击");
            timeline.At(17.24, () => Oracle.ExecuteActionEffect(40288, Oracle), "碎灵一击读条结束演出");
            timeline.At(17.52, () => SpiritTaker(Oracle), "碎灵一击锁定与跳跃");
            timeline.At(20.34, () =>
            {
                Oracle.ExecuteActionEffectAt(40117, new Vector3(Center, 0), (float)Math.PI);
                Oracle.ActorMove(new Vector3(Center, 0), (float)Math.PI, 0);
                MoveAll(_plan.Spread, 25.1, 28.95, true);
            }, "回场并进入启示分散安全区");
            timeline.At(23.7, () =>
            {
                Oracle.Cast(40273, executeOnFinish: false);
                for (var i = 0; i < 8; i++)
                    Icon(i, 139);
            }, "暗炎喷发八人分散读条");
            timeline.AtEach(Enumerable.Range(0, ApocalypsePlan.ExplosionTimes.Length), wave => ApocalypsePlan.ExplosionTimes[wave],
                wave => Run(_plan.Explosions(wave).Select(point => Avoid("启示地火", 40297, point,
                    severity: MechanicFailureSeverity.Warning))), wave => $"第 {wave + 1} 轮启示地火");
            timeline.At(27.64, () => Oracle.ExecuteActionEffect(40273, Oracle), "暗炎喷发读条结束演出");
            timeline.At(28.69, () => Run(Enumerable.Range(0, 8).Select(i => Spread(Roles[i] + "的暗炎喷发", i))), "八人暗炎喷发判定");
            // 外侧两人先经过前方近战的分散点，再回分摊组，不能斜穿相邻地火的圆边。
            timeline.At(28.74, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    Move(slot, _plan.ReturnGate(slot), _plan.SpreadIndices[slot] >= 2 ? 30.2 : 29.65, 30.45, true);
            }, "分散后离开地火夹角");
            timeline.At(30.45, () =>
            {
                for (var slot = 0; slot < 8; slot++)
                    Move(slot, _plan.SecondStack(slot), 33.4, 34.65, true);
            }, "沿安全方向按分摊组回中");
            timeline.At(31.41, () => Oracle.Cast(40181, executeOnFinish: false), "至黑之舞读条");
            timeline.At(34.53, () => ResolveWater(1), "第二轮四人水分摊");
            timeline.At(34.55, () => Move(_plan.TankSlot, _plan.TankPosition, 36.35, 37.84, true), "分摊后坦克边出人群边转至 45° 贴边引导");
            timeline.At(36.35, () => Oracle.ExecuteActionEffect(40181, Oracle), "至黑之舞读条结束演出");
            timeline.At(36.81, BeginDance, "至黑之舞远点跳跃");
            timeline.At(39.599, Knockback, "至黑之舞击退演出与锁定");
            timeline.At(40.5, () => MoveAll(i => _plan.FinalStack(i, _dance), 42.6, 44.2, true, gapClose: true), "击退后近战突进并准备第三轮分摊");
            timeline.At(43.56, () => ResolveWater(2), "第三轮四人水分摊");
            timeline.At(43.65, () => MoveAll(_ => _plan.AfterFinalStack(_dance), 45.2, 49.1), "分摊结束后回到 Boss 身后集合");
            timeline.At(43.96, () => Oracle.Cast(40282, target: Oracle), "脉冲星震波读条");
            timeline.At(49.1, Complete, "完成启示练习");
            return timeline;
        }

        private void ResolveWater(int round)
        {
            var mechanics = new List<SimMechanic>();
            for (var group = 0; group < 2; group++)
            {
                var target = _plan.Target(round, group);
                ClearStatus(target, 2461);
                mechanics.Add(Stack($"第{round + 1}轮{Roles[target]}的黑暗狂水", target, 4, _plan.Group(group)));
            }
            var water = new MechanicGroup(mechanics);
            if (round == 2)
                water.Resolved = _ => ClearPartyMarks();
            Run(water);
        }

        private void BeginDance()
        {
            var snapshot = PartySnapshot.Capture(Party);
            var target = snapshot.Members.OrderByDescending(p => Vector2.DistanceSquared(p.Position, Oracle.Pos)).First().Slot;
            var targetPosition = snapshot.Get(target).Position;
            _dance = JumpLanding(Oracle, targetPosition);
            _danceHeading = Heading(targetPosition - Oracle.Pos);
            var buster = Spread("至黑之舞死刑", target, 40182, false);
            buster.LockedSnapshot = snapshot;
            buster.Check = _ => target != _plan.TankSlot ? $"应由 {Roles[_plan.TankSlot]} 引导，实际为 {Roles[target]}。" : null;
            buster.Failed = result => Report(result.DescribeBaitFailure("至黑之舞死刑", new[] { _plan.TankSlot }, _plan.TankSlot, target)
                .WithAttribution(target == Self || _plan.TankSlot == Self || result.Participants.Any(p => p.Slot == Self)
                    ? new[] { Self } : Array.Empty<int>()), result);
            buster.Visual = result =>
            {
                Oracle.ActorMove(Oracle.Entity.Pos3D, _danceHeading, 0);
                Oracle.ExecuteActionEffect(40182, Party[target], _danceHeading, result.HitActors);
                SimKnockback.Resolve(Scheduler, result.HitActors, KnockbackEffect.RadialFromRow(targetPosition, SpiritTakerKnockbackRow),
                    movementAt: Scheduler.ElapsedSeconds + 1.02, exclude: Party[target],
                    beforeMovement: () => Oracle.ActorMove(new Vector3(_dance, 0), _danceHeading, 0),
                    afterMovement: () => ReturnForKnockback(target), name: "至黑之舞落点与死刑击退");
            };
            Run(buster);
            for (var i = 0; i < 8; i++)
                if (i != target)
                    ReturnForKnockback(i);
        }

        private void ReturnForKnockback(int slot)
        {
            var angle = Heading(Center - _dance) + Deg(_plan.StackGroups[slot] == 0 ? -15 : 15);
            // 此公式大致可以使 30 度击退时，位于 r_max 的玩家被击退到贴场边。
            var maxRadius = 0.8f * (Vector2.Distance(_dance, Center) - 2);
            Move(slot, SimPosition.AnnularSector(_dance, maxRadius * 0.75f, angle, maxRadius * 0.25f, Deg(5)), 39.15, 40.4, true);
        }

        private void Knockback()
        {
            // 回放：技能包 39.599 秒；录制者 40.350～40.870 秒击退，40.953 秒恢复普通移动。
            SimKnockback.Resolve(Scheduler, Party, KnockbackEffect.RadialFromRow(_dance, 201),
                movementAt: Origin + 40.35, name: "至黑之舞击退位移开始");
            Oracle.ActorMove(new Vector3(_dance, 0), _danceHeading, 0);
            Oracle.ExecuteActionEffectAt(40183, new Vector3(_dance, 0), _danceHeading);
        }

        private void SpawnLights()
        {
            for (var i = 0; i < 10; i++)
            {
                var direction = i < 8 ? i : _plan.StartDirection + (i == 8 ? 0 : 4);
                var point = i < 8 ? Center + 14 * Direction(ApocalypsePlan.Angle(direction)) : Center;
                _lights.Add(EntityManager.SpawnEObj(new EventObjectSpawnSpec
                {
                    Index = (byte)(20 + i),
                    BaseId = 0x1EB0FF,
                    EventId = 0x800375BF,
                    Pos = new Vector3(point, 0),
                    // 场中光点直接朝起始方向；外围 EObj 按切线朝向播放圆弧动画。
                    Heading = (float)(ApocalypsePlan.Angle(direction) - (i < 8 ? Deg(_plan.Turn * 90) : 0)),
                    SharedTimelineState = 4,
                    SharedGroupState = (uint)((i < 8 ? 0x42 + ApocalypsePlan.Mod(-i, 8) : i == 8 ? 0x40 : 0x41) << 16) | 3,
                    TargetableFlags = 5
                }));
            }
            for (var step = 0; step < ApocalypsePlan.LightAnimationTimes.Length; step++)
            {
                var beat = step;
                Scheduler.Schedule(Origin + ApocalypsePlan.LightAnimationTimes[step], () =>
                {
                    if (beat <= 1)
                    {
                        EntityManager.PlayEObjAnimation(_lights[8], 4, 1);
                        EntityManager.PlayEObjAnimation(_lights[9], 4, 1);
                    }
                    // 顺逆时针分别使用 0x10/0x20、0x40/0x80 播放及收尾；每拍最多续播三组圆弧。
                    var animation = (ushort)(_plan.Turn > 0 ? beat == 4 ? 0x20 : 0x10 : beat == 4 ? 0x80 : 0x40);
                    for (var trail = Math.Max(0, beat - 2); trail <= beat; trail++)
                    {
                        var direction = ApocalypsePlan.Mod(_plan.StartDirection - _plan.Turn * trail, 8);
                        EntityManager.PlayEObjAnimation(_lights[direction], 4, animation);
                        EntityManager.PlayEObjAnimation(_lights[(direction + 4) % 8], 4, animation);
                    }
                }, "启示光点步进演出");
            }
        }
    }
}
