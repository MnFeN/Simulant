using Simulant.Core;
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

namespace Simulant.Content.U7a.Presets
{
    internal sealed class P4光暗龙诗 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P4 一运：光与暗的龙诗";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 16);
        public override PhaseData Phase => U7a.P4;
        public override int Level => 100;
        public override Type SimLogicType => typeof(DarklitLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "处理光之锁、双塔、近点扇形、碎灵一击、半场刀与分摊，再引导先远后近的真夜舞蹈。坦克可用 /e sim ac 无敌，或使用本职业无敌的快捷栏按键。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new ComboBoxOption<DarklitSolution>(nameof(DarklitLogic.Solution), "攻略解法", DarklitSolution.RopeTHMR, new Map<DarklitSolution>
            {
                [DarklitSolution.RopeTHMR] = "国服翻绳（MT ST H1 H2 | D1-4）",
                [DarklitSolution.RopeHTMR] = "日服翻绳（H1 H2 MT ST | D1-4）",
                [DarklitSolution.RopeHTRM] = "美服翻绳（H1 H2 MT ST | D3 D4 D1 D2）"
            }),
            new ComboBoxOption<DiagonalSwap>(nameof(DarklitLogic.HourglassSwap), "沙漏换位", DiagonalSwap.Anti, new Map<DiagonalSwap>
            {
                [DiagonalSwap.Main] = "主斜向（左上↔右下）", [DiagonalSwap.Anti] = "副斜向（右上↔左下）国/日/美服"
            }),
            new ComboBoxOption<SideSwap>(nameof(DarklitLogic.SquareSwap), "方形换位", SideSwap.Left, new Map<SideSwap>
            {
                [SideSwap.Left] = "左侧（左上↔左下）国服", [SideSwap.Right] = "右侧（右上↔右下）美/日"
            }),
            new ComboBoxOption<SomberDanceBait>(nameof(DarklitLogic.TankBait), "死刑跳跃引导", SomberDanceBait.MTInvulnerable, new Map<SomberDanceBait>
            {
                [SomberDanceBait.MTInvulnerable] = "MT 无敌两次", [SomberDanceBait.STInvulnerable] = "ST 无敌两次",
                [SomberDanceBait.MTThenST] = "MT → ST", [SomberDanceBait.STThenMT] = "ST → MT"
            }),
            new ComboBoxOption<DarklitDebuff>(nameof(DarklitLogic.Initial), "初始点名", DarklitDebuff.Random, new Map<DarklitDebuff>
            {
                [DarklitDebuff.Random] = "全随机", [DarklitDebuff.ChainWater] = "连线 + 水分摊", [DarklitDebuff.Chain] = "连线，无水分摊",
                [DarklitDebuff.ProteanWater] = "无连线 + 水分摊", [DarklitDebuff.Protean] = "无连线，无水分摊"
            }),
            new ComboBoxOption<WingSide>(nameof(DarklitLogic.Wing), "神圣之翼", WingSide.Random, new Map<WingSide>
            {
                [WingSide.Random] = "随机", [WingSide.Left] = "左侧刀（右侧安全）", [WingSide.Right] = "右侧刀（左侧安全）"
            }),
            U7aExerciseOptions.FastOpeningOption()
        };
    }

    internal sealed class DarklitLogic : U7aExerciseLogic
    {
        [SimOption] public DarklitSolution Solution { get; set; }
        [SimOption] public DiagonalSwap HourglassSwap { get; set; }
        [SimOption] public SideSwap SquareSwap { get; set; }
        [SimOption] public SomberDanceBait TankBait { get; set; }
        [SimOption] public DarklitDebuff Initial { get; set; }
        [SimOption] public WingSide Wing { get; set; }
        [SimOption] public bool FastOpening { get; set; }
        protected override bool IsP4 => true;
        protected override double OpeningAdvance => FastOpening ? 3.7 : 0;
        private DarklitPlan _plan;
        private int _firstHit = -1;
        private readonly int[] _towerStates = { -1, -1 };

        public DarklitLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void PreparePlan() => _plan = new DarklitPlan(Self, Initial, Solution, HourglassSwap,
            SquareSwap, TankBait, Wing, Random);

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            timeline.At(0, () =>
            {
                Oracle.FaceNetwork(0);
                Shiva.FaceNetwork((float)Math.PI);
                Oracle.Entity.SetNetworkTarget(Oracle.Entity);
                Shiva.Entity.SetNetworkTarget(Shiva.Entity);
                Oracle.Cast(40301, target: Oracle, castTime: FastOpening ? 1 : 4.7f);
                Shiva.Cast(40239, target: Shiva, castTime: FastOpening ? 1 : 4.7f);
                foreach (var slot in _plan.Waters)
                    Icon(slot, 62);
                MoveAll(_plan.Initial, 5.4, 5.8);
            }, "光与暗的龙诗双读条");
            timeline.At(5, () =>
            {
                foreach (var slot in _plan.Waters)
                {
                    Status(slot, 2461, 19.1f);
                    // 无名状态 2458 的循环特效保留头顶水标记，19.015 秒换为最终分摊标记。
                    Status(slot, 2458, 14.015f);
                }
            }, "给予两组延迟水分摊");
            timeline.At(5.79, () =>
            {
                for (var i = 0; i < 8; i++)
                    Status(i, 2257, 600, 3);
                Tethers(110);
                MoveAll(_plan.TowerOrBait, 14.9, 17.15);
            }, "光之锁连线、换位与双塔");
            timeline.At(8.06, () =>
            {
                Map(0x2A, 2);
                Map(0x2B, 2);
                Map(0x2C, 1);
                Map(0x2D, 1);
                RefreshTowerPreview();
            }, "双塔与光之波动场地预兆");
            timeline.At(16.1, () =>
            {
                Map(0x2A, 8);
                Map(0x2B, 8);
            }, "双塔场地预兆结束");
            timeline.At(8.13, () => Shiva.Cast(40187, target: Shiva, castTime: 7.7f), "光之波动读条与双塔预兆");
            timeline.At(12.76, () => Tethers(111), "转为无尽光之束缚");
            timeline.At(16.2, ResolveTowers, "南北双人塔判定");
            timeline.At(16.2, () => Oracle.Cast(40288, executeOnFinish: false), "碎灵一击读条");
            timeline.At(17.04, ResolveProteans, "四近点光之波动判定");
            timeline.At(17.15, () => MoveAll(_plan.Spread, 19.15, 20.55, true), "后退分散，避开北侧水晶");
            timeline.At(19.015, () =>
            {
                foreach (var slot in _plan.Waters)
                {
                    ClearStatus(slot, 2458);
                    Icon(slot, 0xB8);
                }
            }, "黑暗狂水分摊标记");
            timeline.At(19.3, () =>
            {
                Shiva.Cast(_plan.WingAction, omenPosition: new Vector3(_plan.WingOrigin, 0),
                    omenHeading: (float)Math.PI, castTime: 4.7f, executeOnFinish: false);
            }, "希瓦神圣之翼半场刀");
            timeline.At(19.2, () => Oracle.Entity.ExecuteActionEffect(40288, Oracle.Entity), "碎灵一击读条结束演出");
            timeline.At(19.47, () => SpiritTaker(Oracle), "碎灵一击锁定与跳跃");
            timeline.At(20.55, () =>
            {
                var safeDistance = Crystal.Entity.HitboxRadius + GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(40271).EffectRange;
                MoveAll(slot => _plan.Stack(slot, Crystal.Pos, safeDistance), 23.55, 24.3, true);
            }, "穿到安全半场，南北四人分摊");
            timeline.At(22.35, () => Oracle.Cast(40283, castTime: 4.7f, executeOnFinish: false), "真夜舞蹈读条");
            timeline.At(24.12, () =>
            {
                foreach (var target in _plan.Waters)
                {
                    ClearStatus(target, 2461);
                    Run(Stack(Roles[target] + "的黑暗狂水", target, 4, _plan.Group(_plan.Quadrants[target])));
                }
            }, "两组黑暗狂水判定");
            timeline.At(24.2, () =>
            {
                var wing = Avoid("神圣之翼", _plan.WingAction, _plan.WingOrigin,
                    area: ActionArea.FromAction(_plan.WingAction), heading: (float)Math.PI);
                wing.Visual = _ => Shiva.Entity.ExecuteActionEffect(_plan.WingAction, Shiva.Entity, (float)Math.PI);
                Run(wing);
            }, "半场刀判定");
            timeline.At(24.35, () => MoveAll(_plan.FirstBait, 27.25, 27.9, true), "指定坦克引导最远死刑");
            timeline.At(26.3, () =>
            {
                if (_plan.NeedsInvulnerability && _plan.FirstTank != Self)
                    EntityManager.UseInvulnerability(Party[_plan.FirstTank]);
            }, "假坦克开启本职业无敌");
            timeline.At(27.3, () =>
            {
                foreach (var slot in _plan.Cycle)
                {
                    Party[slot].Entity.ActorControl(ActorControlCategory.TetherCancel);
                    ClearStatus(slot, 2255);
                }
                for (var slot = 0; slot < 8; slot++)
                    ClearStatus(slot, 2257);
            }, "光之锁解除");
            timeline.At(27.27, () => Oracle.Entity.ExecuteActionEffect(40283, Oracle.Entity), "真夜舞蹈读条结束演出");
            timeline.At(27.78, () => ResolveDance(false), "真夜舞蹈最远死刑");
            timeline.At(31.02, () => ResolveDance(true), "真夜舞蹈最近死刑");
            timeline.At(33, Complete, "完成光与暗的龙诗练习");
            return timeline;
        }

        private void RefreshTowerPreview()
        {
            if (Now >= 16.1)
                return;
            var snapshot = PartySnapshot.Capture(Party);
            for (var tower = 0; tower < 2; tower++)
            {
                var state = snapshot.InArea(ActionArea.Circle(4), _plan.Tower(tower)).Count == 2 ? 0x80 : 0x20;
                if (_towerStates[tower] != state)
                {
                    Map((uint)(0x2A + tower), (ushort)state);
                    _towerStates[tower] = state;
                }
            }
            Scheduler.ScheduleAfter(0.1, RefreshTowerPreview, "更新双人塔人数提示");
        }

        private void Tethers(uint id)
        {
            for (var index = 0; index < 4; index++)
            {
                var slot = _plan.Cycle[index];
                Party[slot].Entity.ActorControl(ActorControlCategory.TetherCancel);
                if (id == 111)
                    ClearStatus(slot, 2253);
                Status(slot, id == 110 ? (ushort)2253 : (ushort)2255, id == 110 ? 7 : 14.6f);
                Party[slot].Entity.ActorControl(ActorControlCategory.Tether, 0, id, Party[_plan.Cycle[(index + 1) % 4]].Id, 15);
            }
        }

        private void ResolveTowers()
        {
            var towers = new List<SimMechanic>();
            for (var half = 0; half < 2; half++)
            {
                var expected = _plan.Soakers.Skip(half * 2).Take(2).ToArray();
                var point = _plan.Tower(half);
                var helper = half;
                towers.Add(new AreaMechanic
                {
                    Name = half == 0 ? "北侧双人塔" : "南侧双人塔",
                    Area = ActionArea.Circle(4),
                    Center = point,
                    RequiredPlayers = 2,
                    Check = members => members.Any(p => !_plan.Cycle.Contains(p.Slot)) ? "无连线玩家不应踩塔。" : null,
                    Visual = _ => PointEffect(helper, 40213, point),
                    Failed = result => Report(result.DescribeFailure("双人塔", expected)
                        .WithAttribution(expected.Contains(Self) != result.Participants.Any(p => p.Slot == Self)
                            ? new[] { Self } : Array.Empty<int>()), result)
                });
            }
            Run(towers.ToArray());
        }

        private void ResolveProteans()
        {
            var snapshot = PartySnapshot.Capture(Party);
            var mechanics = new List<SimMechanic>();
            foreach (var target in snapshot.Nearest(Center, 4))
            {
                var heading = Heading(target.Position - Center);
                var bait = Avoid(Roles[target.Slot] + "引导的光之波动", 40190, Center, target.Slot,
                    ActionArea.Cone(60, 60), heading);
                bait.LockedSnapshot = snapshot;
                bait.MaxPlayers = 1;
                bait.Check = _ => _plan.Cycle.Contains(target.Slot) ? "连线玩家不应引导光之波动。" : null;
                bait.Failed = result => Report(result.DescribeFailure("光之波动扇形", new[] { target.Slot }, target.Slot)
                    .WithAttribution(target.Slot == Self || result.Participants.Any(p => p.Slot == Self)
                        ? new[] { Self } : Array.Empty<int>(), "可能是自身站歪了"), result);
                mechanics.Add(bait);
            }
            Run(mechanics.ToArray());
        }

        private void ResolveDance(bool second)
        {
            var snapshot = PartySnapshot.Capture(Party);
            var target = second ? snapshot.Nearest(Oracle.Pos).Value.Slot :
                snapshot.Members.OrderByDescending(p => Vector2.DistanceSquared(p.Position, Oracle.Pos)).First().Slot;
            var expected = second ? _plan.SecondTank : _plan.FirstTank;
            var targetPosition = snapshot.Get(target).Position;
            var landing = JumpLanding(Oracle, targetPosition);
            var heading = Heading(targetPosition - Oracle.Pos);
            var invulnerable = Party[target].JudgementInvulnerable;
            var mechanic = Spread(second ? "真夜舞蹈最近死刑" : "真夜舞蹈最远死刑", target,
                second ? 40285u : 40284u, false);
            mechanic.LockedSnapshot = snapshot;
            mechanic.Check = _ => target != expected ? "未由约定坦克引导跳跃。" :
                second && target == _firstHit && !invulnerable ? "连续承受两次死刑，需要本职业无敌。" : null;
            mechanic.Visual = result =>
            {
                Oracle.Entity.ActorMove(Oracle.Entity.Pos3D, heading, 0);
                Oracle.Entity.ExecuteActionEffect(second ? 40285u : 40284u, Party[target].Entity, heading);
                Scheduler.ScheduleAfter(1.05, () => Oracle.Entity.ActorMove(new Vector3(landing, 0), heading, 0), "真夜舞蹈落点同步");
                Status(target, 3323, 10);
                if (!second)
                    _firstHit = target;
            };
            mechanic.Failed = result => Report(result.DescribeFailure(mechanic.Name, new[] { expected }, target)
                .WithAttribution(target == Self || expected == Self || result.Participants.Any(p => p.Slot == Self)
                    ? new[] { Self } : Array.Empty<int>(), "自身引导位置或无敌时机不正确"), result);
            Run(ProtectCrystal(mechanic, target));
            if (!second)
                MoveAll(slot => _plan.SecondBait(slot, landing), 30.3, 31.2, true);
        }
    }
}
