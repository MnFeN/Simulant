using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
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

namespace Simulant.Content.U5a.Presets
{
    internal sealed class P4神圣大审判 : SimPresetBase
    {
        public override int TerritoryId => 887;
        public override string Name => "P4 神圣大审判";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 14);
        public override PhaseData Phase => U5a.P4;
        public override WaymarkLayout Waymarks => U5a.Waymarks;
        public override int Level => 80;
        public override Type SimLogicType => typeof(P4神圣大审判Logic);
        public override string Description => 
            "记忆三组地火预兆，依次躲避爆炸，株连分摊仅为演出效果。\n\n" +
            "左、中、右指各包含 Boss 南侧三个地火之一的三组预兆。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<JudgmentOrder>(
                nameof(P4神圣大审判Logic.FlareOrder), "地火刷新模式", JudgmentOrder.Random,
                new Map<JudgmentOrder>
                {
                    [JudgmentOrder.Random] = "随机",
                    [JudgmentOrder.LeftMiddleRight] = "左 - 中 - 右",
                    [JudgmentOrder.LeftRightMiddle] = "左 - 右 - 中",
                    [JudgmentOrder.MiddleLeftRight] = "中 - 左 - 右",
                    [JudgmentOrder.MiddleRightLeft] = "中 - 右 - 左",
                    [JudgmentOrder.RightLeftMiddle] = "右 - 左 - 中",
                    [JudgmentOrder.RightMiddleLeft] = "右 - 中 - 左",
                }),
        };
    }

    internal enum JudgmentOrder
    {
        [RandomEnum(LeftMiddleRight, LeftRightMiddle, MiddleLeftRight, MiddleRightLeft, RightLeftMiddle, RightMiddleLeft)]
        Random = -1,
        LeftMiddleRight, LeftRightMiddle, MiddleLeftRight, MiddleRightLeft, RightLeftMiddle, RightMiddleLeft,
    }

    internal sealed class P4神圣大审判Logic : SimLogicBase
    {
        // 北起五行、西起五列，坐标 84～116、间隔 8 米。
        // 每个字表示该点所属的固定组；第 4 行中间三列依次为左、中、右。
        private static readonly string[] Layout = { "左右中右左", "中中右左右", "右左左左中", "中左中右右", "左中右中左" };
        private static readonly string[] Orders = { "左中右", "左右中", "中左右", "中右左", "右左中", "右中左" };
        private bool _hit;

        [SimOption]
        internal JudgmentOrder FlareOrder { get; set; }

        public P4神圣大审判Logic(PluginHost host, SimPresetBase preset) : base(host, preset) { }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            timeline.At(0, () => EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                // 提供的国服回放中，完美亚历山大的本体参数。
                BNpcBaseId = 11349,
                BNpcNameId = 9042,
                Level = 80,
                Pos = new Vector3(100, 100, 0),
                Heading = (float)Math.PI,
            }, PrepareFlareCasters), "生成完美亚历山大");
        }

        private void PrepareFlareCasters(SimActor boss)
        {
            var groups = new Dictionary<char, List<SimActor>>
            {
                ['左'] = new List<SimActor>(), ['中'] = new List<SimActor>(), ['右'] = new List<SimActor>(),
            };
            var remaining = 25;
            for (var row = 0; row < 5; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    var group = Layout[row][column];
                    Dummy(new Vector3(84 + 8 * column, 84 + 8 * row, 0), 0, actor =>
                    {
                        groups[group].Add(actor);
                        // 全部就绪后才开始计时；同组预兆与爆炸复用同一批实体。
                        if (--remaining == 0) BeginJudgment(boss, groups);
                    });
                }
            }
        }

        private void BeginJudgment(SimActor boss, Dictionary<char, List<SimActor>> groups)
        {
            var myself = EntityManager.Attach(_host.EntityProvider.GetMyself());
            Party.Add(myself);
            var order = Orders[(int)FlareOrder];
            var mechanic = new SimTimeline();
            mechanic.At(0, () => boss.Cast(0x488E), "神圣大审判读条");

            // 首组预兆在读条开始后 6.1 秒，之后间隔 2 秒。
            for (var i = 0; i < order.Length; i++)
            {
                var group = groups[order[i]];
                var name = $"第 {i + 1} 组（{order[i]}）地火";
                var showAt = 6.1 + 2 * i;
                mechanic.At(showAt, () => group.ForEach(actor => actor.Cast(0x488F)), name + "预兆");
                mechanic.At(showAt + 8, () => ResolveFlares(name, group, myself), name + "爆炸");
            }

            // 神圣大审判读条 3 秒，结束 3.5 秒后持续朝向玩家，直到株连开始读条。
            mechanic.Repeat(6.5, 0.1, 76, _ => FacePlayer(boss, myself), "完美亚历山大朝向玩家");
            mechanic.At(14.1, () =>
            {
                FacePlayer(boss, myself);
                boss.Cast(0x4894, target: myself);
            }, "株连分摊读条并锁定面向");
            mechanic.At(20, () =>
            {
                if (!_hit) ShowResultMarker(myself, "m0489trg_a0c");
                _host.LogSim(_hit ? "[神圣大审判] 三组地火结束，本次有地火命中。" : "[神圣大审判] 三组地火全部躲避成功。");
            }, "神圣大审判结果");
            // 保留最后的技能动画，手动取消仍由会话统一回收实体、预兆和标点。
            mechanic.At(24, () => EntityManager.Clear(), "回收神圣大审判实体");
            Scheduler.ScheduleAfter(3, () => Play(mechanic), "实体就绪后开始神圣大审判");
        }

        private static void FacePlayer(SimActor boss, SimActor myself)
        {
            if (!boss.IsActive || !myself.IsActive) return;
            var direction = myself.Pos - boss.Pos;
            if (direction.LengthSquared() < 0.01f) return;
            var heading = (float)Math.Atan2(direction.X, direction.Y);
            var difference = Math.IEEERemainder(heading - boss.Heading, 2 * Math.PI);
            if (Math.Abs(difference) > 0.01) boss.FaceNetwork(heading);
        }

        private void ResolveFlares(string name, List<SimActor> casters, SimActor myself)
        {
            var area = ActionArea.FromAction(0x4890);
            // 同一快照判定整组圆形；先播放全部爆炸，再汇总一次提示，避免重叠圆重复报错。
            EntityManager.RunMechanic(new MechanicGroup(casters.Select(actor => new AreaMechanic
            {
                Name = name,
                Area = area,
                Center = actor.Pos,
                MaxPlayers = 0,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Visual = result => actor.Execute(0x4890),
            }).ToArray())
            {
                Name = name,
                Failed = result =>
                {
                    _hit = true;
                    ShowResultMarker(myself, "m0489trg_b0c");
                    var message = $"[神圣大审判] {name}命中自身：自身仍在爆炸范围内。";
                    _host.LogWarning(message);
                    try { TriggernometryInterop.InvokeNamedCallback("command", "/e <se.11>\n" + message); }
                    catch (Exception error) { _host.LogWarning("[神圣大审判] 默语提示发送失败：" + error.Message); }
                },
            });
        }

        private void ShowResultMarker(SimActor myself, string marker)
        {
            try { TriggernometryInterop.InvokeNamedCallback("LockOn", $"{myself.Address}, {marker}"); }
            catch (Exception error) { _host.LogWarning("[神圣大审判] 结果提示特效显示失败：" + error.Message); }
        }
    }
}
