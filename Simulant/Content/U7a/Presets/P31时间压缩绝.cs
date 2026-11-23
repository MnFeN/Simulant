using Simulant.Core;
using Simulant.Core.Entity;
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
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(3.1)]
    internal sealed class P31时间压缩绝 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P3-1 时间压缩·绝";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 16);
        public override PhaseData Phase => U7a.P3;
        public override int Level => 100;
        public override Type SimLogicType => typeof(RelativityLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new ComboBoxOption<RelativitySolution>(nameof(RelativityLogic.Solution), "攻略解法", RelativitySolution.CN, new Map<RelativitySolution>
            {
                [RelativitySolution.CN] = "国服",
                [RelativitySolution.CNHTDH] = "国服（但改为 HTDH 优先级）",
                [RelativitySolution.NA] = "美服"
            }),
            new ComboBoxOption<RelativityDebuff>(nameof(RelativityLogic.Initial), "初始点名", RelativityDebuff.Random, new Map<RelativityDebuff>
            {
                [RelativityDebuff.Random] = "随机", 
                [RelativityDebuff.Ice] = "冰", 
                [RelativityDebuff.ShortFire] = "短火",
                [RelativityDebuff.MediumFire] = "中火", 
                [RelativityDebuff.LongFire] = "长火"
            }),
            U7aExerciseOptions.Direction(nameof(RelativityLogic.NorthDirection), "相对北"),
            U7aExerciseOptions.FastOpeningOption()
        };
    }

    internal sealed class RelativityLogic : U7aExerciseLogic
    {
        [SimOption] public RelativitySolution Solution { get; set; }
        [SimOption] public RelativityDebuff Initial { get; set; }
        [SimOption] public ExerciseDirection NorthDirection { get; set; }
        [SimOption] public bool FastOpening { get; set; }
        protected override double OpeningAdvance => FastOpening ? 8.7 : 0;
        private P31时间压缩绝Plan _plan;
        private readonly SimActor[] _hourglasses = new SimActor[8];
        private readonly Vector3[] _returns = new Vector3[8];

        public RelativityLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void PreparePlan() => _plan = new P31时间压缩绝Plan(Self, Initial, Solution, NorthDirection, Random);

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            timeline.At(0, () => Map(0x28, 0x20), "切换时间压缩地板");
            timeline.At(0, () => Oracle.Cast(40266, target: Oracle, castTime: FastOpening ? 1 : 9.7f), "时间压缩·绝读条");
            timeline.At(10.74, Assign, "时间压缩点名");
            timeline.At(10.774, () =>
            {
                // 无名状态 2459 单独控制头顶计时图标，不随回返状态的切换而移除。
                for (var i = 0; i < 8; i++)
                    Status(i, 2459, 42);
            }, "显示回返头顶计时图标");
            timeline.At(10.81, () => HourglassMaps(2), "八方沙漏场地预兆");
            timeline.At(18.78, () => HourglassMaps(0x20), "沙漏场地效果进入等待阶段");
            timeline.At(11.22, SpawnHourglasses, "生成八座沙漏");
            timeline.At(14.06, () => Oracle.Cast(40293, target: Oracle), "限速读条");
            timeline.At(19.53, () =>
            {
                for (var direction = 0; direction < 8; direction++)
                    if (P31时间压缩绝Plan.HourglassWave(direction) != 1)
                        _hourglasses[direction].CancelTether();
            }, "取消沙漏加减速连线");
            timeline.At(19.63, () =>
            {
                for (var i = 0; i < 8; i++)
                    if (P31时间压缩绝Plan.HourglassWave(i) != 1)
                        _hourglasses[i].ExecuteActionEffect(P31时间压缩绝Plan.HourglassWave(i) == 0 ? 40294u : 40295u, _hourglasses[i]);
            }, "沙漏加减速演出");

            double FireAt(int round) => 21.853 + 10.010 * round;
            var casts = new[] { 22.95, 32.94, 43.90 };
            var hits = new[] { 26.94, 36.94, 47.90 };
            for (var round = 0; round < 3; round++)
            {
                var wave = round;
                timeline.At(FireAt(round), () => ResolveFire(wave), $"第 {round + 1} 次火与分摊");
                timeline.At(FireAt(round) + 0.2, () => MoveAll(i => _plan.Between(i, wave), hits[wave] - 0.65, hits[wave] + 0.15), "引导激光或记录回返");
                timeline.At(20.798 + 5.005 * round, () => HourglassMaps(0x80, wave), "沙漏就绪场地效果");
                timeline.At(casts[round] - 0.13, () =>
                {
                    for (var direction = 0; direction < 8; direction++)
                        if (P31时间压缩绝Plan.HourglassWave(direction) == wave)
                        {
                            var actor = _hourglasses[direction];
                            Map(_plan.HourglassMapSlot(direction), 0x200);
                            EntityManager.SetStatus(actor, 2970, 0, (ushort)(_plan.Spins[direction] < 0 ? 0x15C : 0x10D), actor);
                        }
                }, "沙漏旋转方向与读条");
                timeline.At(casts[round], () =>
                {
                    for (var direction = 0; direction < 8; direction++)
                        if (P31时间压缩绝Plan.HourglassWave(direction) == wave)
                            _hourglasses[direction].Cast(40291, target: _hourglasses[direction], castTime: 3.7f);
                }, "沙漏罪熔毁读条");
                timeline.At(new[] { 26.85, 36.81, 47.88 }[round], () =>
                {
                    // 2970 是常驻旋转标记，按回放的移除包主动结束。
                    for (var direction = 0; direction < 8; direction++)
                        if (P31时间压缩绝Plan.HourglassWave(direction) == wave)
                            EntityManager.RemoveStatus(_hourglasses[direction], 2970);
                }, "移除沙漏旋转方向标记");
                timeline.At(hits[round], () => LockLasers(wave), "锁定最近玩家并引导激光");
                // 第一段之后固定等两秒，再让下一轮分散者穿出；不检测激光是否已经离开。
                if (round < 2)
                    timeline.At(hits[round] + 2.05, () => MoveAll(i => _plan.FirePosition(i, wave + 1), FireAt(wave + 1) - 0.45, FireAt(wave + 1) + 0.1, true), "等待激光转过后再出去分散");
            }

            timeline.At(26.77, () => RecordReturn(true), "第一组记录回返位置");
            timeline.At(36.73, () => RecordReturn(false), "第二组记录回返位置");
            timeline.At(50.74, () =>
            {
                for (var i = 0; i < 8; i++)
                {
                    Status(i, 4163, 4);
                    if (i != Self)
                        Party[i].FaceNetwork(Heading(new Vector2(_returns[i].X, _returns[i].Y) - Center));
                }
            }, "回返眩晕并调整假队友朝向");
            timeline.At(51.73, () =>
            {
                for (var i = 0; i < 8; i++)
                {
                    ClearStatus(i, 2452);
                    Party[i].Entity.WarpWithAnimation(_returns[i], Party[i].Heading, 8488);
                }
            }, "带动画返回记录点");
            timeline.At(52.708, () =>
            {
                for (var i = 0; i < 8; i++)
                    ClearStatus(i, 2459);
            }, "移除回返头顶计时图标");
            timeline.At(53.88, ResolveRewinds, "回返后的分散、分摊和石化");
            timeline.At(54.74, () =>
            {
                MoveAll(_ => Spot(Center, 1), 57.0, 58.2, true);
                Oracle.Cast(40286, executeOnFinish: false);
            }, "眩晕结束，破盾一击读条");
            timeline.At(57.78, () => Oracle.ExecuteActionEffect(40286, Oracle), "破盾一击读条结束演出");
            timeline.At(58.15, () => Run(Stack("破盾一击八人分摊", Random.Next(8), 8, Enumerable.Range(0, 8), 40287)), "破盾一击判定");
            timeline.At(59, Complete, "完成时间压缩练习");
            timeline.At(61.23, () =>
            {
                HourglassMaps(8);
                foreach (var hourglass in _hourglasses)
                    EntityManager.SetEnemyListVisibility(hourglass, false);
            }, "沙漏退场");
            return timeline;
        }

        private void Assign()
        {
            for (var i = 0; i < 8; i++)
            {
                var kind = P31时间压缩绝Plan.Kind(_plan.Codes[i]);
                Status(i, kind == 0 ? (ushort)2462 : (ushort)2455, kind == 0 ? 21.1f : kind * 10 + 1.1f);
                Status(i, 2464, _plan.EarlyReturn(i) ? 16 : 26);
                Status(i, _plan.Gaze(i) ? (ushort)2456 : _plan.Water(i) ? (ushort)2461 : (ushort)2460, 43.1f);
            }
            for (var wave = 0; wave < 3; wave++)
                Status(_plan.StackTargets[wave], 2454, 11.1f + wave * 10);
            MoveAll(i => _plan.FirePosition(i, 0), 20.7, 22.05);
        }

        private void SpawnHourglasses()
        {
            var actorReady = WhenAllActorsReady(_hourglasses.Length, () =>
                Scheduler.Schedule(Math.Max(Origin + 13.98, Scheduler.ElapsedSeconds), TetherHourglasses, "沙漏加减速连线"));
            for (var i = 0; i < _hourglasses.Length; i++)
            {
                var slot = i;
                var position = _plan.Hourglass(i);
                EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17832,
                    BNpcNameId = 9825,
                    LayoutId = 0xA55C00,
                    Level = 100,
                    MaxHealthPoints = 188300,
                    Pos = new Vector3(position, 0),
                    Heading = Heading(Center - position),
                    Targetable = false,
                    EnemyListVisibility = true
                }, actor =>
                {
                    _hourglasses[slot] = actor;
                    actorReady();
                });
            }
        }

        private void HourglassMaps(ushort state, int? wave = null)
        {
            for (var direction = 0; direction < 8; direction++)
                if (!wave.HasValue || P31时间压缩绝Plan.HourglassWave(direction) == wave.Value)
                    Map(_plan.HourglassMapSlot(direction), state);
        }

        private void TetherHourglasses()
        {
            for (var i = 0; i < 8; i++)
                if (P31时间压缩绝Plan.HourglassWave(i) != 1)
                    _hourglasses[i].TetherTo(Oracle, P31时间压缩绝Plan.HourglassWave(i) == 0 ? 134u : 133u);
        }

        private void ResolveFire(int wave)
        {
            var mechanics = new List<SimMechanic>();
            for (var i = 0; i < 8; i++)
            {
                if (_plan.HasFire(i, wave))
                {
                    ClearStatus(i, 2455);
                    mechanics.Add(Spread($"{Roles[i]}的黑暗爆炎", i, 40276));
                }
                else if (wave == 1 && P31时间压缩绝Plan.Kind(_plan.Codes[i]) == 0)
                {
                    ClearStatus(i, 2462);
                    var target = i;
                    mechanics.Add(new AreaMechanic("黑暗冰封月环", ActionArea.Donut(4, 12), centerSlot: target)
                    {
                        MaxPlayers = 0,
                        Visual = result => TargetEffect(target, 40279, target, result.HitActors),
                        Failed = result => Report(result.DescribeFailure("黑暗冰封月环", Array.Empty<int>())
                            .WithAttribution(result.Participants.Select(p => p.Slot)), result)
                    });
                }
            }
            var stack = _plan.StackTargets[wave];
            ClearStatus(stack, 2454);
            mechanics.Add(Stack($"第{wave + 1}轮黑暗神圣", stack, 5,
                Enumerable.Range(0, 8).Where(i => !_plan.HasFire(i, wave)), 40277));
            Run(mechanics);
        }

        private void LockLasers(int wave)
        {
            var snapshot = PartySnapshot.Capture(Party);
            var directions = Enumerable.Range(0, 8).Where(i => P31时间压缩绝Plan.HourglassWave(i) == wave).ToArray();
            var headings = new float[8];
            var firstLasers = new List<SimMechanic>();
            foreach (var direction in directions)
            {
                var center = _plan.Hourglass(direction);
                var target = snapshot.Nearest(center).Value;
                var heading = Heading(target.Position - center);
                var outward = _plan.North + Deg(direction * 45);
                var angle = ToDegrees(Math.Abs(Math.Atan2(Math.Sin(heading - outward), Math.Cos(heading - outward))));
                if (target.Slot == Self && angle < 70)
                    Report(MechanicError.Rule("激光引导", new[] { Self },
                        FormattableString.Invariant($"自身激光引导过于偏向场外（{angle:0}°），应控制在 80° 附近。"))
                        .WithAttribution(new[] { Self }));
                var first = Avoid("沙漏初始激光", 40235, center, direction, ActionArea.Rectangle(60, 5), heading);
                first.LockedSnapshot = snapshot;
                first.MaxPlayers = 1;
                first.Failed = result => Report(result.DescribeFailure("沙漏初始激光", new[] { target.Slot }, target.Slot)
                    .WithAttribution(target.Slot == Self || result.Participants.Any(p => p.Slot == Self)
                        ? new[] { Self } : Array.Empty<int>(), "可能是自身站歪了"), result);
                firstLasers.Add(first);
                headings[direction] = heading;
                if (wave == 2)
                    Move(target.Slot, _plan.LastLaserRetreat(direction), 49.0, 54.74, true);
                else
                    Move(target.Slot, Spot(Center, 0.7f), Now + 1.75, Now + 4.7, true);
            }

            // 同一轮先播放全部激光，再报告失败，避免第一道出错就取消其余演出。
            Run(firstLasers);
            for (var step = 1; step <= 9; step++)
            {
                var rotationStep = step;
                Scheduler.ScheduleAfter(2.1 + (step - 1) * 1.067, () =>
                {
                    var lasers = directions.Select(direction => Avoid("沙漏旋转激光", 40292, _plan.Hourglass(direction), direction,
                        ActionArea.Rectangle(50, 5), headings[direction] + (float)Deg(_plan.Spins[direction] * rotationStep * 15)));
                    Run(lasers);
                }, "旋转激光判定");
            }
        }

        private void RecordReturn(bool early)
        {
            for (var i = 0; i < 8; i++)
                if (_plan.EarlyReturn(i) == early)
                {
                    _returns[i] = Party[i].JudgementPosition3D;
                    ClearStatus(i, 2464);
                    Status(i, 2452, early ? 24 : 14);
                    var position = _returns[i];
                    Party[Self].Entity.PlayPositionVfx(1013, position);
                }
        }

        private void ResolveRewinds()
        {
            var mechanics = new List<SimMechanic>();
            for (var i = 0; i < 8; i++)
            {
                var target = i;
                if (_plan.Gaze(i))
                {
                    ClearStatus(i, 2456);
                    var gaze = new AreaMechanic(Roles[i] + "的暗影之眼", ActionArea.Circle(100), centerSlot: target)
                    {
                        Visual = result => TargetEffect(target, 40278, target, result.HitActors),
                        Check = members => members.Any(p => p.Slot != target && FacingGaze(p.Actor, p.Position, Party[target].JudgementPosition))
                            ? "面前 90° 范围内存在石化源。" : null,
                        Failed = result => Report(MechanicError.Rule("暗影之眼", result.Participants.Where(p => p.Slot != target &&
                            FacingGaze(p.Actor, p.Position, result.Center)).Select(p => p.Slot), result.Reason)
                            .WithAttribution(result.Participants.Where(p => p.Slot != target && FacingGaze(p.Actor, p.Position, result.Center)).Select(p => p.Slot)), result)
                    };
                    mechanics.Add(gaze);
                }
                else if (_plan.Water(i))
                {
                    ClearStatus(i, 2461);
                    mechanics.Add(Stack("回返黑暗狂水", i, 4, Enumerable.Range(0, 8).Where(s => _plan.Gaze(s) || _plan.Water(s))));
                }
                else
                {
                    ClearStatus(i, 2460);
                    mechanics.Add(Spread(Roles[i] + "的回返暗炎喷发", i));
                }
            }
            Run(mechanics);
        }

        internal static bool FacingGaze(SimActor actor, Vector2 position, Vector2 source)
            => ActionArea.Cone(100, 90).Contains(source, position, actor.JudgementHeading);
    }
}
