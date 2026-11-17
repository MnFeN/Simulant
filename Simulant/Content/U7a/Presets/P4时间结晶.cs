using Simulant.ACT;
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
    internal sealed class P4时间结晶 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P4 二运：时间结晶";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 15);
        public override PhaseData Phase => U7a.P4;
        public override int Level => 100;
        public override Type SimLogicType => typeof(CrystallizeTimeLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => 
            "红色同点名按优先级，高左低右；\n" +
            "蓝色固定土水冰暗分别标记 1、2、3、4，面朝南侧从左至右吃白圈。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<int>(nameof(CrystallizeTimeLogic.PlayerSlot), "自身职能", 1,
                new Map<int>
            {
                [1] = "MT", [2] = "ST", [3] = "H1", [4] = "H2", [5] = "D1", [6] = "D2", [7] = "D3", [8] = "D4"
            }),
            new ComboBoxOption<CrystalSolution>(nameof(CrystallizeTimeLogic.Solution), "解法", CrystalSolution.YAntiKnockback,
                new Map<CrystalSolution>
            {
                [CrystalSolution.Knockback] = "正攻击退", [CrystalSolution.YAntiKnockback] = "Y 字防击退逃课"
            }),
            new ComboBoxOption<string>(nameof(CrystallizeTimeLogic.RedPriority), "红组优先级", CrystallizeTimePlan.DefaultRedPriority,
                new Map<string>
                {
                    ["12345678"] = "MT-D4", 
                    ["34125678"] = "H1 H2 MT ST D1-4（国服）",
                    ["43215678"] = "H2 H1 ST MT D1-4（美服）", 
                    ["23184567"] = "H1 MT ST D1-4 H2（日服）"
                },
                "红冰、红风各自比较优先级，高优先级去左，低优先级去右。"),
            new ComboBoxOption<CrystalDebuff>(nameof(CrystallizeTimeLogic.InitialDebuff), "初始点名", CrystalDebuff.Random,
                new Map<CrystalDebuff> {
                    [CrystalDebuff.Random] = "全随机",
                    [CrystalDebuff.RandomRed] = "随机红龙",
                    [CrystalDebuff.RandomBlue] = "随机蓝龙",
                    [CrystalDebuff.RedWind] = "红龙 + 黑暗暴风（风）",
                    [CrystalDebuff.RedIce] = "红龙 + 黑暗冰封（冰）",
                    [CrystalDebuff.BlueWater] = "蓝龙 + 黑暗狂水（水）",
                    [CrystalDebuff.BlueEarth] = "蓝龙 + 黑暗神圣（土）",
                    [CrystalDebuff.BlueDark] = "蓝龙 + 暗炎喷发（暗）",
                    [CrystalDebuff.BlueIce] = "蓝龙 + 黑暗冰封（冰）"
                }),
            new ComboBoxOption<CrystalWave>(nameof(CrystallizeTimeLogic.Wave), "光波起始方向", CrystalWave.Random,
                new Map<CrystalWave>
                {
                    [CrystalWave.Random] = "随机", 
                    [CrystalWave.LeftUp] = "左 - 上", 
                    [CrystalWave.LeftDown] = "左 - 下",
                    [CrystalWave.RightUp] = "右 - 上", 
                    [CrystalWave.RightDown] = "右 - 下"
                }),
            new ComboBoxOption<CrystalPurple>(nameof(CrystallizeTimeLogic.Purple), "紫线方向", CrystalPurple.Random,
                new Map<CrystalPurple>
                {
                    [CrystalPurple.Random] = "随机", 
                    [CrystalPurple.MainDiagonal] = "主斜向（＼）", 
                    [CrystalPurple.AntiDiagonal] = "副斜向（／）"
                }),
            new CheckBoxOption(nameof(CrystallizeTimeLogic.FastOpening), "开场读条加速", false,
                "开场咏唱缩短为 1 秒，后续机制同步提前。")
        };
    }

    internal sealed partial class CrystallizeTimeLogic : SimLogicBase
    {
        [SimOption]
        internal int PlayerSlot { get; set; }

        [SimOption]
        internal CrystalSolution Solution { get; set; }

        [SimOption]
        internal CrystalDebuff InitialDebuff { get; set; }

        [SimOption]
        internal string RedPriority { get; set; } = CrystallizeTimePlan.DefaultRedPriority;

        [SimOption]
        internal CrystalWave Wave { get; set; }

        [SimOption]
        internal CrystalPurple Purple { get; set; }

        [SimOption]
        internal bool FastOpening { get; set; }

        private int Self => PlayerSlot - 1;

        private readonly Random _random = new Random();
        private CrystallizeTimePlan _plan;
        private SimActor _shiva, _gaia, _crystal, _wyrm;
        private readonly SimActor[] _sand = new SimActor[6], _heads = new SimActor[2], _helpers = new SimActor[19];
        private readonly EventObject[] _puddles = new EventObject[4];
        private readonly Vector2[] _puddlePositions = new Vector2[4];
        private readonly Vector3[] _rewind = new Vector3[8];
        private readonly bool[] _cleansed = new bool[8], _taken = new bool[4];
        private readonly int[] _headHits = new int[2];
        private readonly HashSet<uint> _mapChanges = new HashSet<uint>();
        private readonly HashSet<int> _marked = new HashSet<int>();
        private int[] _firstFront;
        private bool _markersOwned;
        private bool _crystalBroken;
        private int _mistakes;
        private double _origin;
        private double Now => Scheduler.ElapsedSeconds - _origin;
        private static Vector2 Center => CrystallizeTimePlan.Center;

        public CrystallizeTimeLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            _plan = new CrystallizeTimePlan(Self, InitialDebuff, Wave, Purple, _random, RedPriority, Solution);
            timeline.At(0, () => GeneratePartyMembers(PlayerSlot, PrepareActors), "准备时间结晶小队");
        }

        private void PrepareActors()
        {
            for (var slot = 0; slot < 8; slot++)
            {
                Party[slot].Pos = CrystallizeTimePlan.Circle(Center, 5).Sample(_random);
                Party[slot].Heading = (float)Math.PI;
            }

            var pending = 6 + _sand.Length + _helpers.Length;

            void Ready()
            {
                if (--pending != 0)
                    return;

                Scheduler.ScheduleAfter(3, BeginEncounter, "开始时间结晶双读条");
            }

            void Spawn(uint id, uint name, Vector2 pos, Action<SimActor> accept, bool hidden = false, uint layout = 0, float heading = (float)Math.PI,
                ushort? modelCharaId = null)
                => EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = id,
                    BNpcNameId = name,
                    ModelCharaId = modelCharaId,
                    LayoutId = layout,
                    Pos = new Vector3(pos, 0),
                    Heading = heading,
                    Level = 100,
                    Targetable = !hidden
                }, actor =>
                {
                    accept(actor);

                    if (hidden)
                        actor.Entity.DisableDraw();

                    Ready();
                });

            // 直接使用 Transformation 547 对应的圣龙模型，跳过 StanceChange 45 的变身动画。
            Spawn(17833, 12809, Center + new Vector2(0, 5), actor => _shiva = actor, layout: 0xA55C01, heading: 0, modelCharaId: 4376);
            Spawn(17835, 9832, Center, actor => _gaia = actor, layout: 0xA55C02);

            PrepareCrystal(Ready);

            Spawn(17834, 9324, Center + new Vector2(0, 5), actor =>
            {
                _wyrm = actor;
                actor.Entity.SetTargetable(false);
            }, heading: 0);

            for (var i = 0; i < 6; i++)
            {
                var index = i;
                Spawn(17837, 9823, _plan.Hourglass(i), actor => _sand[index] = actor, true, 0xA55C04);
            }

            for (var i = 0; i < _helpers.Length; i++)
            {
                var index = i;

                Dummy(new Vector3(Center, 0), 0, actor =>
                {
                    _helpers[index] = actor;
                    Ready();
                });
            }
        }

        private void PrepareCrystal(Action ready)
            => U7aCrystal.Spawn(EntityManager, Scheduler, actor =>
            {
                if (actor.Entity.Native.BaseId.Get() == 17841)
                    _crystal = actor;
                ready();
            });

        private void BeginEncounter()
        {
            var start = Scheduler.ElapsedSeconds;
            // 保留回放的相对时间，统一提前节点、走位期限与龙头轨迹。
            _origin = start - (FastOpening ? 9 : 0);
            EntityManager.SetCombatTarget(_shiva);
            // 回放双读条时：南侧希瓦朝南，中央暗之巫女朝北；目标不再驱动模型转向。
            _shiva.Entity.SetNetworkTarget(_shiva.Entity);
            _gaia.Entity.SetNetworkTarget(_gaia.Entity);
            _shiva.FaceNetwork(0);
            _gaia.FaceNetwork((float)Math.PI);
            Map(0x2E, 2);
            _host.LogSim($"[时间结晶] 你的职能为 {CrystallizeTimePlan.Roles[Self]}，点名为 {CrystallizeTimePlan.Label(_plan.Debuffs[Self])}。");
            foreach (var node in BuildMechanicTimeline().Snapshot())
                Scheduler.Schedule(Math.Max(start, _origin + node.Seconds), node.Action, node.Name);

            CheckArena();
        }

        private void SpawnHeads()
        {
            // 回放在连线后 0.083 秒直接生成龙头，没有提前生成后的额外显现控制。
            for (var i = 0; i < 2; i++)
            {
                var index = i;
                EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17836,
                    BNpcNameId = 9323,
                    LayoutId = 0xA55C03,
                    Level = 100,
                    Targetable = false,
                    Pos = new Vector3(i == 0 ? 98 : 102, 87, 0),
                    Heading = (float)Deg(i == 0 ? -90 : 90)
                }, actor => _heads[index] = actor, timeoutSeconds: 3);
            }
        }

        private void Move(int slot, SimPosition area, double arrival, double hold = 120, bool urgent = false)
        {
            if (slot == Self)
                return;

            Party[slot].MoveForMechanic(area, _origin + arrival, _origin + hold, urgent);
        }

        private void MoveAll(Func<int, SimPosition> area, double arrival, double hold = 120, bool urgent = false)
        {
            for (var slot = 0; slot < 8; slot++)
                Move(slot, area(slot), arrival, hold, urgent);
        }

        private SimActor Helper(int index, Vector2 position, float heading = 0)
        {
            var actor = _helpers[index];
            actor.Entity.ActorMove(new Vector3(position, 0), heading, 0);
            return actor;
        }

        private static float Heading(Vector2 direction) => (float)Math.Atan2(direction.X, direction.Y);
        private void Effect(int helper, uint action, int target) => Helper(helper, Party[target].Pos).Entity.ExecuteActionEffect(action, Party[target].Entity);
        private void Status(int slot, ushort id, float seconds) => EntityManager.SetStatus(Party[slot], id, seconds, 0, _gaia);
        private void ClearStatus(int slot, ushort id) => EntityManager.RemoveStatus(Party[slot], id);

        private void Assign()
        {
            for (var slot = 0; slot < 8; slot++)
            {
                var debuff = _plan.Debuffs[slot];
                Status(slot, CrystallizeTimePlan.Status(debuff), debuff == CrystalDebuff.BlueWater ? 12 : debuff == CrystalDebuff.BlueEarth ? 17 : 14);
                Status(slot, _plan.IsRed(slot) ? (ushort)3263 : (ushort)3264, debuff == CrystalDebuff.RedIce ? 17 : 40);
                Status(slot, 4208, 33);
                // 头顶计时图标由无名状态独立控制，持续到碎灵一击之后。
                Status(slot, 2459, 37);
            }

            foreach (var slot in Enumerable.Range(0, 8).OrderBy(_ => _random.Next()).Take(3))
                Status(slot, 4174, 31);

            _markersOwned = true;

            for (var i = 0; i < 4; i++)
                Mark(i + 1, Party[_plan.BlueOrder[i]].Id);

            MoveAll(_plan.Initial, CrystallizeTimePlan.SandHits[0] - 1.2);
        }

        private static void Mark(int number, uint id) => TriggernometryInterop.InvokeNamedCallback("mark",
            "{\"ActorID\":" + id + ",\"MarkType\":\"attack" + number + "\",\"LocalOnly\":true}");

        private void Map(uint index, ushort animation)
        {
            _mapChanges.Add(index);

            if (!_host.EnvironmentService.PlayMapEffect(index, animation))
                throw new InvalidOperationException($"时间结晶场地特效调用失败：位置 {index}，动画 {animation}。");
        }

        protected override void OnStop()
        {
            if (_gaia == null || !_gaia.IsActive)
                return;

            var failures = new List<Exception>();

            if (_markersOwned)
                for (var i = 1; i <= 4; i++)
                    try
                    {
                        Mark(i, 0xE0000000);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex);
                    }

            foreach (var index in _mapChanges.ToArray())
                try
                {
                    Map(index, index == 0x28 || index == 0x29 || index == 0x2E ? (ushort)2 : (ushort)4);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }

            if (failures.Count != 0)
                throw new AggregateException("时间结晶标记与场地复位失败。", failures);

            _markersOwned = false;
            _mapChanges.Clear();
        }

        private void Report(MechanicError error, MechanicResult result = null)
        {
            _mistakes++;

            foreach (var slot in error.Offenders)
                if (_marked.Add(slot))
                    TriggernometryInterop.InvokeNamedCallback("LockOn", $"{Party[slot].Address}, m0489trg_b0c");

            var message = FormattableString.Invariant($"[时间结晶] {Now:0.000} 秒：") + error.Format(Self, slot => CrystallizeTimePlan.Roles[slot]);
            _host.LogWarning(message);

            try
            {
                TriggernometryInterop.InvokeNamedCallback("command", "/e <se.11>\n" + message);
                if (result != null)
                    result.FailureReported = true;
            }
            catch (Exception ex)
            {
                _host.LogWarning("[时间结晶] 默语提示发送失败：" + ex.Message);
            }
        }
    }
}
