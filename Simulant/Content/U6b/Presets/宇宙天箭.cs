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

namespace Simulant.Content.U6b.Presets
{
    internal class 宇宙天箭 : SimPresetBase
    {
        public override int TerritoryId => 1122;
        public override string Name => "P6 宇宙天箭";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 14);
        public override PhaseData Phase => U6b.P6;
        public override WaymarkLayout Waymarks => U6b.Waymarks;
        public override int Level => 90;
        public override Type SimLogicType => typeof(U6b6_宇宙天箭Logic);
        public override string Description =>
            "第一次：T 按第二次路径，其余人左下起步；治疗按第二次路径走位到正点，最后回左下分摊。\n\n" +
            "第二次：TH 正点、DPS 斜点分散，两组波动炮后左下集合，双 T 站在人群前挡枪。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<int>(nameof(U6b6_宇宙天箭Logic.PlayerSlot), "自己的职能", 1,
                new Map<int> { 
                    [1] = "MT", [2] = "ST", [3] = "H1", [4] = "H2",
                    [5] = "D1", [6] = "D2", [7] = "D3", [8] = "D4" }),
            new ComboBoxOption<ArrowOccurrence>(nameof(U6b6_宇宙天箭Logic.Occurrence), "第几次天箭", ArrowOccurrence.First,
                new Map<ArrowOccurrence> { 
                    [ArrowOccurrence.First] = "第一次：宇宙龙炎", 
                    [ArrowOccurrence.Second] = "第二次：波动炮" }),
            new ComboBoxOption<ArrowMode>(nameof(U6b6_宇宙天箭Logic.ArrowMode), "宇宙天箭模式", ArrowMode.Random,
                new Map<ArrowMode> { 
                    [ArrowMode.Random] = "随机", 
                    [ArrowMode.OutsideFirst] = "先外侧",
                    [ArrowMode.InsideFirst] = "先内侧" }),
        };
    }

    internal partial class U6b6_宇宙天箭Logic : SimLogicBase
    {
        [SimOption] internal int PlayerSlot { get; set; }
        [SimOption] internal ArrowMode ArrowMode { get; set; }
        [SimOption] internal ArrowOccurrence Occurrence { get; set; }
        private SimActor _boss;
        private readonly SimActor[] _helpers = new SimActor[20];
        private readonly Random _random = new Random();
        private readonly HashSet<int> _marked = new HashSet<int>();
        private int[] _beamOrder;
        private int _chargeTarget, _mistakes;
        private double _origin;
        private int SelfSlot => PlayerSlot - 1;

        public U6b6_宇宙天箭Logic(PluginHost host, SimPresetBase preset) : base(host, preset) { }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            if (SelfSlot < 0 || SelfSlot >= 8) throw new InvalidOperationException("宇宙天箭的职能必须在 MT 至 D4 范围内。");
            _mistakes = 0;
            _marked.Clear();
            timeline.At(0, () => GeneratePartyMembers(PlayerSlot, PrepareActors), "生成天箭模拟小队");
        }

        private void PrepareActors()
        {
            for (var slot = 0; slot < 8; slot++)
            {
                Party[slot].Pos = CosmoArrowPlan.SpawnArea.Sample(_random);
                Party[slot].Heading = Heading(CosmoArrowPlan.Center - Party[slot].Pos);
                if (slot != SelfSlot)
                    Party[slot].MoveForMechanic(Party[slot].Pos, Scheduler.ElapsedSeconds + 1, Scheduler.ElapsedSeconds + 120, urgent: true);
            }
            var pending = 1 + _helpers.Length;
            void Ready()
            {
                if (--pending == 0) Scheduler.ScheduleAfter(3, StartMechanic, "实体就绪后开始宇宙天箭");
            }
            EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 15725, BNpcNameId = 12256, Level = 90,
                Pos = new Vector3(100, 100, 0), Heading = (float)Math.PI,
            }, actor => { _boss = actor; Ready(); });
            for (var i = 0; i < _helpers.Length; i++)
            {
                var index = i;
                Dummy(new Vector3(100, 100, 0), 0, actor => { _helpers[index] = actor; Ready(); });
            }
        }

        private void StartMechanic()
        {
            _origin = Scheduler.ElapsedSeconds;
            _beamOrder = Enumerable.Range(0, 8).ToArray();
            for (var i = 7; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                var value = _beamOrder[i]; _beamOrder[i] = _beamOrder[j]; _beamOrder[j] = value;
            }
            _chargeTarget = _random.Next(8);
            EntityManager.SetPartyEnmity(_boss, 100, 90, 30, 30, 50, 50, 50, 50);
            _host.LogSim($"[宇宙天箭] 你的职能为 {CosmoArrowPlan.Roles[SelfSlot]}；{(Occurrence == ArrowOccurrence.First ? "第一次，后接宇宙龙炎" : "第二次，后接波动炮")}；{(ArrowMode == ArrowMode.InsideFirst ? "先内侧" : "先外侧")}。");
            var timeline = new SimTimeline();
            var finish = CosmoArrowPlan.FollowupHit(Occurrence);
            timeline.At(0, () => _boss.Cast(0x7BA2), "宇宙天箭读条");
            var nextHelper = 0;
            for (var set = 0; set < 2; set++)
            {
                var lines = CosmoArrowPlan.InitialLines((ArrowMode == ArrowMode.InsideFirst) == (set == 0));
                foreach (var shot in lines)
                {
                    var index = nextHelper++;
                    timeline.At(set * CosmoArrowPlan.Interval, () =>
                    {
                        PlaceHelper(index, shot.Position, shot.Heading).Cast(0x7BA3);
                        PlaceHelper(index + 6, shot.Position, shot.Heading).Cast(0x7E51);
                    }, "天箭宽直条预兆");
                }
            }
            for (var wave = 0; wave < 8; wave++)
            {
                var index = wave;
                var shots = CosmoArrowPlan.Wave(ArrowMode, wave);
                if (shots.Length != 0)
                    timeline.At(CosmoArrowPlan.Hit(wave), () => ResolveArrows(index, shots), $"第 {wave + 1} 次天箭判定");
            }
            for (var slot = 0; slot < 8; slot++)
            {
                if (slot == SelfSlot) continue;
                var actor = Party[slot];
                foreach (var move in CosmoArrowPlan.Route(ArrowMode, Occurrence, slot))
                    timeline.At(move.Start, () => actor.MoveForMechanic(move.Area,
                        _origin + move.Arrival, _origin + finish + 1, urgent: true), $"{CosmoArrowPlan.Roles[slot]} 天箭走位");
            }
            timeline.At(CosmoArrowPlan.FollowupCast(Occurrence), () => _boss.Cast(Occurrence == ArrowOccurrence.First ? 0x7BA6u : 0x7BA9u), "天箭后续读条");
            if (Occurrence == ArrowOccurrence.Second)
                for (var wave = 0; wave < 2; wave++)
                {
                    var index = wave;
                    timeline.At(CosmoArrowPlan.BeamHit(wave), () => ResolveBeams(index), $"第 {wave + 1} 组四人波动炮");
                }
            timeline.At(finish, () => { if (Occurrence == ArrowOccurrence.First) ResolveDive(); else ResolveCharge(); }, "天箭后续分摊判定");
            timeline.At(finish + 1, () =>
            {
                if (_mistakes == 0) ShowMarker(SelfSlot, "m0489trg_a0c");
                _host.LogSim($"[宇宙天箭] 流程结束，共 {_mistakes} 项失误，3 秒后回收模拟实体。");
            }, "天箭结果提示");
            timeline.At(finish + 4, () => { EntityManager.Clear(); Scheduler.Cancel(); }, "回收天箭模拟小队");
            Scheduler.Schedule(_origin, () => Play(timeline), "开始天箭时间轴");
        }

        private SimActor PlaceHelper(int index, Vector2 position, float heading)
        {
            var actor = _helpers[index];
            actor.Pos = position;
            actor.Heading = heading;
            return actor;
        }

        private void Report(MechanicError error)
        {
            _mistakes++;
            foreach (var slot in error.Offenders)
                if (_marked.Add(slot)) ShowMarker(slot, "m0489trg_b0c");
            var message = $"[宇宙天箭] {Scheduler.ElapsedSeconds - _origin:0.000} 秒：" + error.Format(SelfSlot, slot => CosmoArrowPlan.Roles[slot]);
            _host.LogWarning(message);
            try { TriggernometryInterop.InvokeNamedCallback("command", "/e <se.11>\n" + message); }
            catch (Exception errorSending) { _host.LogWarning("[宇宙天箭] 默语提示发送失败：" + errorSending.Message); }
        }

        private void ShowMarker(int slot, string marker)
        {
            try { TriggernometryInterop.InvokeNamedCallback("LockOn", $"{Party[slot].Address}, {marker}"); }
            catch (Exception error) { _host.LogWarning("[宇宙天箭] 结果提示特效显示失败：" + error.Message); }
        }

        private static float Heading(Vector2 direction) => (float)Math.Atan2(direction.X, direction.Y);
    }
}
