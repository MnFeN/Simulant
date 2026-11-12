using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7b.Presets
{
    internal sealed class P2遗弃末世 : SimPresetBase
    {
        public override int TerritoryId => 1363;
        public override string Name => "P2遗弃末世";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 13);
        public override PhaseData Phase => U7b.P2_2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(P2遗弃末世Logic);
        public override string Description => "随机点名、塔方向与前后刀；按野队解法，A 组 1238、B 组 4567，闲人 TH 左、DPS 右。假队员在判定前最后一秒就位。判定失败会记录原因，不扣除真实玩家血量。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<int>(nameof(P2遗弃末世Logic.PlayerSlot), "自己的职能", 0,
                new Map<int> { [0] = "按当前职业", [1] = "MT", [2] = "ST", [3] = "H1", [4] = "H2",
                    [5] = "D1", [6] = "D2", [7] = "D3", [8] = "D4" }),
        };
    }

    internal sealed partial class P2遗弃末世Logic : SimLogicBase
    {
        private const ushort TroubleStatus = 0x13DB;
        private static readonly string[] Roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        private readonly SimActor[] _casters = new SimActor[4];
        private readonly SimActor[] _helpers = new SimActor[10];
        private readonly int[] _stacks = new int[8];
        private readonly ForsakenMarker[] _markers = new ForsakenMarker[8];
        private readonly List<int>[] _triggered = new List<int>[8];
        private readonly Vector2[] _cleaveOrigins = new Vector2[4];
        private readonly float[] _cleaveHeadings = new float[4];
        private readonly HashSet<int> _failedPlayers = new HashSet<int>();
        private ForsakenPlan _plan;
        private int _selfSlot;
        private int _mistakes;
        private int[] _jumpTargets;
        private double _origin;
        private bool _environmentChanged;

        [SimOption] internal int PlayerSlot { get; set; }

        public P2遗弃末世Logic(PluginHost host, SimPresetBase preset) : base(host, preset) { }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            _mistakes = 0;
            _failedPlayers.Clear();
            Array.Clear(_casters, 0, _casters.Length);
            Array.Clear(_helpers, 0, _helpers.Length);
            var me = _host.EntityProvider.GetMyself();
            if (me == null || !me.IsAlive) throw new InvalidOperationException("未找到自身玩家，无法准备遗弃末世。");
            _selfSlot = PlayerSlot == 0 ? GetDefaultSlot(me) : PlayerSlot - 1;
            if (_selfSlot < 0 || _selfSlot >= 8) throw new InvalidOperationException("遗弃末世的职能必须在 MT 至 D4 范围内。");
            var seed = Environment.TickCount;
            _plan = ForsakenPlan.Create(new Random(seed));
            for (var i = 0; i < 8; i++)
            {
                _stacks[i] = 4;
                _lastMagicHit[i] = double.NegativeInfinity;
                _markers[i] = _plan.InitialMarkers[i];
                _triggered[i] = new List<int>();
            }
            _host.LogSim($"[遗弃末世] 本次随机种子 {seed}，你的职能为 {Roles[_selfSlot]}；假队员正常跑速，最后一秒就位。");

            timeline.At(0, () => GeneratePartyMembers(_selfSlot + 1, PrepareEnemies), "准备遗弃末世小队");
        }

        private void PrepareEnemies()
        {
            // 开场全队按职能预站位；进入机制后只控制假队员，真实玩家自行走位。
            for (var i = 0; i < 8; i++)
            {
                Party[i].Pos = ForsakenPlan.InitialPosition(i);
                Party[i].Heading = Heading(Party[i].Pos, ForsakenPlan.Center);
            }

            var remaining = _casters.Length + _helpers.Length;
            Action ready = () =>
            {
                if (--remaining != 0) return;
                EntityManager.SetPartyEnmity(_casters[0], 100, 90, 30, 30, 50, 50, 50, 50);
                Scheduler.ScheduleAfter(3, StartMechanic, "演出实体就绪后开始遗弃末世");
            };
            for (var i = 0; i < _casters.Length; i++)
            {
                var index = i;
                EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = i == 0 ? 19506u : 19513u,
                    BNpcNameId = 7131, ModelCharaId = (ushort)(i == 0 ? 4967 : 2137),
                    LayoutId = i == 0 ? 0xBC1728u : 0xBC172Fu,
                    Level = 100, Pos = new Vector3(100, 100, 0), Targetable = i == 0,
                }, actor =>
                {
                    _casters[index] = actor;
                    actor.Entity.Scale = 3.5f;
                    if (index != 0) actor.Entity.DisableDraw();
                    ready();
                });
            }
            for (var i = 0; i < _helpers.Length; i++)
            {
                var index = i;
                Dummy(new Vector3(100, 100, 0), 0, actor => { _helpers[index] = actor; ready(); });
            }
        }

        private void ApplyMarker(int slot, ForsakenMarker marker)
        {
            for (ushort id = 0x13DC; id <= 0x13DE; id++) EntityManager.RemoveStatus(Party[slot], id);
            if (marker == ForsakenMarker.None) return;
            var status = (ushort)(0x13DC + (ushort)marker - (ushort)ForsakenMarker.Stack);
            EntityManager.SetStatus(Party[slot], status, 9999, 0, _casters[0]);
            // 头标本身使用原生收包入口；第二参数是被点名者 ID。
            Party[slot].Entity.ActorControl(0x22, (uint)marker, Party[slot].Id);
        }

        private void SetTrouble(int slot)
        {
            if (_stacks[slot] == 0) EntityManager.RemoveStatus(Party[slot], TroubleStatus);
            else EntityManager.SetStatus(Party[slot], TroubleStatus, 30, (ushort)_stacks[slot], _casters[0]);
        }

        private void Effect(int helper, Vector2 position, float heading, uint action)
        {
            var actor = _helpers[helper];
            actor.Pos = position;
            actor.Heading = heading;
            actor.Execute(action);
        }

        private void MapEffect(uint index, ushort timeline)
        {
            _environmentChanged = true;
            if (!_host.EnvironmentService.PlayMapEffect(index, timeline))
                throw new InvalidOperationException($"遗弃末世场地特效调用失败：位置 {index}，动画 {timeline}。");
        }

        protected override void OnStop()
        {
            if (!_environmentChanged) return;
            // 切地图或切进程会先使旧实体身份失效；不能把旧副本的清理写进新的世界。
            if (_casters[0] == null || !_casters[0].Entity.IsAlive)
            {
                _environmentChanged = false;
                return;
            }
            List<Exception> errors = null;
            // 使用阶段基础动画复位；包的 00020001/00080004 对应高 16 位的动画 2/8。
            foreach (var index in new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 0x11, 0x12 })
            {
                try { MapEffect(index, 4); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
            try { _host.EnvironmentService.SetWeather(_preset.Phase.Weather, 0); }
            catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            if (errors != null) throw new AggregateException("遗弃末世场地复位失败。", errors);
            _environmentChanged = false;
        }

        private int GetDefaultSlot(Character me)
        {
            if (JobsByRole[JobRole.Tank].Contains(me.Job)) return 0;
            if (JobsByRole[JobRole.Healer].Contains(me.Job)) return 2;
            if (JobsByRole[JobRole.PhysicalRanged].Contains(me.Job)) return 6;
            if (JobsByRole[JobRole.MagicalRanged].Contains(me.Job)) return 7;
            return 4;
        }

        private void CompleteMechanic()
        {
            if (_mistakes == 0)
                ShowResultMarker("m0489trg_a0c");
            _host.LogSim($"[遗弃末世] 八轮塔与四轮前后刀结束，记录 {_mistakes} 项失误，涉及 {_failedPlayers.Count} 名队员；3 秒后回收。");
        }

        private void ShowResultMarker(string marker)
        {
            try { TriggernometryInterop.InvokeNamedCallback("LockOn", $"{Party[_selfSlot].Address}, {marker}"); }
            catch (Exception error) { _host.LogWarning("[遗弃末世] 结果提示特效显示失败：" + error.Message); }
        }
    }
}
