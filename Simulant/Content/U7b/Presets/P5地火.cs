using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static System.Math;

namespace Simulant.Content.U7b.Presets
{
    internal class P5地火 : SimPresetBase
    {
        public override int TerritoryId { get; } = 1363;
        public override string Name { get; } = "P5地火";
        public override string Author { get; } = "阿洛";
        public override DateTime LastUpdated { get; } = new DateTime(2026, 6, 12);
        public override PhaseData Phase { get; } = U7b.P5;
        public override int Level => 100;
        public override Type SimLogicType { get; } = typeof(P5地火Logic);
        public override string Description { get; } = "当前 Boss 模型与读条名称疑似因高难副本加密而无法正常显示。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<ExaFlareOrder>(
                nameof(P5地火Logic.LeftExaFlareOrder),
                "左上方三轮地火出现顺序（视角面向左上）",
                ExaFlareOrder.Random,
                new Map<ExaFlareOrder>
                {
                    [ExaFlareOrder.Random] = "随机",
                    [ExaFlareOrder.Line14_25_36] = "1/4列 - 2/5列 - 3/6列",
                    [ExaFlareOrder.Line14_36_25] = "1/4列 - 3/6列 - 2/5列",
                    [ExaFlareOrder.Line25_14_36] = "2/5列 - 1/4列 - 3/6列",
                    [ExaFlareOrder.Line25_36_14] = "2/5列 - 3/6列 - 1/4列",
                    [ExaFlareOrder.Line36_14_25] = "3/6列 - 1/4列 - 2/5列",
                    [ExaFlareOrder.Line36_25_14] = "3/6列 - 2/5列 - 1/4列",
                }),

            new ComboBoxOption<ExaFlareOrder>(
                nameof(P5地火Logic.RightExaFlareOrder),
                "右上方三轮地火出现顺序（视角面向右上）",
                ExaFlareOrder.Random,
                new Map<ExaFlareOrder>
                {
                    [ExaFlareOrder.Random] = "随机",
                    [ExaFlareOrder.Line14_25_36] = "1/4列 - 2/5列 - 3/6列",
                    [ExaFlareOrder.Line14_36_25] = "1/4列 - 3/6列 - 2/5列",
                    [ExaFlareOrder.Line25_14_36] = "2/5列 - 1/4列 - 3/6列",
                    [ExaFlareOrder.Line25_36_14] = "2/5列 - 3/6列 - 1/4列",
                    [ExaFlareOrder.Line36_14_25] = "3/6列 - 1/4列 - 2/5列",
                    [ExaFlareOrder.Line36_25_14] = "3/6列 - 2/5列 - 1/4列",
                }),

            new CheckBoxOption(
                nameof(P5地火Logic.EnableAuxiliaryLines),
                "启用斜向辅助线",
                false,
                "在地火前进方向添加斜向辅助线，辅助走位。"),

            new CheckBoxOption(
                nameof(P5地火Logic.UseSpreadDummies),
                "生成七个无敌的假队友干扰最终分散",
                false),
        };
    }

    internal enum ExaFlareOrder
    {
        [RandomEnum(Line14_25_36, Line14_36_25, Line25_14_36, Line25_36_14, Line36_14_25, Line36_25_14)]
        Random,
        Line14_25_36, Line14_36_25, 
        Line25_14_36, Line25_36_14, 
        Line36_14_25, Line36_25_14
    }

    internal partial class P5地火Logic : SimLogicBase
    {
        [SimOption]
        internal ExaFlareOrder LeftExaFlareOrder { get; set; }

        [SimOption]
        internal ExaFlareOrder RightExaFlareOrder { get; set; }

        [SimOption]
        internal bool EnableAuxiliaryLines { get; set; }

        [SimOption]
        internal bool UseSpreadDummies { get; set; }

        public P5地火Logic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        private bool _isPerfect;
        private int _remainingJudgements;
        private bool _feedbackDue;
        private ActionArea _exaArea;
        private ActionArea _spreadArea;

        protected override void OnStop()
        {
            _feedbackDue = false;
            RemoveAuxiliaryLines();
        }

        const float ExaFlareGroupDelay = 2.5f;

        protected override void BuildTimeline(SimTimeline timeline)
        {
            _isPerfect = true;
            _remainingJudgements = 12 * 6;
            _feedbackDue = false;
            var leftOrder = ResolveExaFlareOrder(LeftExaFlareOrder);
            _exaArea = ActionArea.FromAction(0xBB3D);
            _spreadArea = ActionArea.FromAction(0xBB3F);
            var rightOrder = ResolveExaFlareOrder(RightExaFlareOrder);

            var me = _host.EntityProvider.GetMyself();
            if (me == null || !me.IsAlive)
                throw new InvalidOperationException("未找到有效的自身玩家，无法准备地火模拟。");
            var myIdx = GetFakePartyIndex(me);

            timeline.At(0, () => EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 19511, // Kefka
                BNpcNameId = 7131,
                Level = 100,
                Pos = new Vector3(100, 100, 0),
                Heading = (float)PI,
            }, boss =>
            {
                if (UseSpreadDummies)
                    GeneratePartyMembers(myIdx, () =>
                    {
                        SpreadPartyMembers(me, 3f);
                        StartMechanic(boss, me, leftOrder, rightOrder);
                    });
                else
                    StartMechanic(boss, me, leftOrder, rightOrder);
            }), "生成地火本体与模拟小队");
        }

        private void StartMechanic(SimActor boss, Character me, List<int> leftOrder, List<int> rightOrder)
        {
            if (EnableAuxiliaryLines)
            {
                CreateAuxiliaryLines();
            }

            LogExecute(boss, 0xC654);

            var mechanic = new SimTimeline();
            mechanic.At(0, () =>
            {
                boss.Cast(0xBB3B); // 混沌末世 本体读条
                LogCast(boss, 0xBB3B, boss.Pos3D, boss.Heading);
            }, "混沌末世读条");

            // 左右各三组，每组两条，左右交替间隔 2.5 秒。
            for (int group = 0; group < 3; group++)
            {
                var leftTime = ExaFlareGroupDelay * group * 2;
                var rightTime = leftTime + ExaFlareGroupDelay;
                for (int line = group * 2; line < group * 2 + 2; line++)
                {
                    mechanic.At(leftTime, SingleExaFlare(boss, leftOrder[line], true));
                    mechanic.At(rightTime, SingleExaFlare(boss, rightOrder[line], false));
                }
            }

            mechanic.At(16.2, () =>
            {
                boss.Cast(0xBB3E); // 混沌末世 本体读条
                LogCast(boss, 0xBB3E, boss.Pos3D, boss.Heading);

                if (UseSpreadDummies)
                {
                    SpreadPartyMembers(me, 5.1f);
                }
            }, "分散读条与队友走位");

            mechanic.At(22, () =>
            {
                var playerPositions = _host.EntityProvider.GetCharacters()
                    .Where(e => e.ObjectKind == ObjectKind.Pc)
                    .Select(e => (e.Pos3D, e.Address == me.Address))
                    .ToList();

                _remainingJudgements += playerPositions.Count;
                foreach (var (pos, isSelf) in playerPositions)
                {
                    ExecuteAndValidateSpread(pos, isSelf);
                }
            }, "分散判定");

            mechanic.At(23.5, () =>
            {
                _feedbackDue = true;
                TryReportPerfect();
            }, "无伤完成反馈");

            mechanic.At(27, () =>
            {
                EntityManager.Delete(boss);
                EntityManager.Clear();
            }, "回收模拟实体");
            Scheduler.ScheduleAfter(3, () => Play(mechanic), "本体与小队就绪后开始地火");
        }

        private List<int> ResolveExaFlareOrder(ExaFlareOrder order)
        {
            switch (order)
            {
                case ExaFlareOrder.Line14_25_36:
                    return new List<int> { 1, 4, 2, 5, 3, 6 };
                case ExaFlareOrder.Line14_36_25:
                    return new List<int> { 1, 4, 3, 6, 2, 5 };
                case ExaFlareOrder.Line25_14_36:
                    return new List<int> { 2, 5, 1, 4, 3, 6 };
                case ExaFlareOrder.Line25_36_14:
                    return new List<int> { 2, 5, 3, 6, 1, 4 };
                case ExaFlareOrder.Line36_14_25:
                    return new List<int> { 3, 6, 1, 4, 2, 5 };
                case ExaFlareOrder.Line36_25_14:
                    return new List<int> { 3, 6, 2, 5, 1, 4 };
                default:
                    throw new ArgumentException("Invalid ExaFlareOrder: " + order);
            }
        }

        /// <param name="lineIdx"> 面向地火源头方向，从左至右记为 1-6。</param>
        /// <param name="isTopLeft"> 是左上方的地火，反之为右上方。</param>
        /// <returns></returns>
        private SimTimeline SingleExaFlare(SimActor boss, int lineIdx, bool isTopLeft)
        {
            var initPos = isTopLeft
                   ? new Vector3(100 - 35 + 5 * lineIdx, 100 - 5 * lineIdx, 0)
                   : new Vector3(100 + 5 * lineIdx, 100 - 35 + 5 * lineIdx, 0);

            var heading = (float)PI * (isTopLeft ? 1f : -1f) / 4f;

            var dPos = isTopLeft
                ? new Vector3(5, 5, 0)
                : new Vector3(-5, 5, 0);

            var steps = new SimTimeline();
            // 后六次判定相对初始预兆为 4.582、5.095、5.608、6.121、6.634、7.147 秒。
            steps.Repeat(4.582, 0.513, 6,
                i => ExecuteAndValidateExaFlare(initPos + dPos * (i + 1), heading),
                "地火前进判定");
            var timeline = new SimTimeline();
            timeline.At(0, () => DummyCast(initPos, heading, 0xBB3C, 6f, onCast: () =>
            {
                LogCast(boss, 0xBB3C, initPos, heading);
                Play(steps);
            }), "地火初始预兆");
            return timeline;
        }

        private void ExecuteAndValidateExaFlare(Vector3 pos, float heading)
        {
            DummyExecute(pos, heading, 0xBB3D, 3f, () =>
            {
                var player = _host.EntityProvider.GetMyself();
                if (_exaArea.Contains(player.Pos, new Vector2(pos.X, pos.Y), heading))
                {
                    _isPerfect = false;
                    throw new Simulant.Simulation.Mechanics.SimMechanicFailedException("地火判定失败：自身进入地火伤害范围。");
                }
                CompleteJudgement();
            });
        }

        private void ExecuteAndValidateSpread(Vector3 pos, bool isSelf)
        {
            DummyExecute(pos, 0f, 0xBB3F, 3f, () =>
            {
                if (!isSelf)
                {
                    var player = _host.EntityProvider.GetMyself();
                    if (_spreadArea.Contains(player.Pos, new Vector2(pos.X, pos.Y), 0))
                    {
                        _isPerfect = false;
                        throw new Simulant.Simulation.Mechanics.SimMechanicFailedException("分散判定失败：自身进入其他队员的分散范围。");
                    }
                }
                CompleteJudgement();
            });
        }

        private void CompleteJudgement()
        {
            _remainingJudgements--;
            TryReportPerfect();
        }

        private void TryReportPerfect()
        {
            // 网络生成可能延后实际判定；未完成或已取消的判定不能算作无伤完成。
            if (!_feedbackDue || _remainingJudgements != 0 || !_isPerfect) return;
            _feedbackDue = false;
            TriggernometryInterop.InvokeNamedCallback("LockOn",
                $"{_host.EntityProvider.GetMyself().Address}, m0489trg_a0c");
        }

        const string AuxiliaryLineTag = "Simulant_AuxiliaryLine";
        private string AuxiliaryLinePictoACTCommand(int lineIdx, bool isTopLeft) => $@"
Omen: m131om_setu0f
Tag: {AuxiliaryLineTag}
t: 30
O: 100, 100
DirN4: {(isTopLeft ? 0 : 3)}
Pos: {lineIdx - 3.5} * 5 * √2, -17.5 * √2
Angle: 0
Scale: 0.03, 35 * √2, 0.05
Color: 1, 1, 1, 0.5";

        private void CreateAuxiliaryLines()
        {
            for (int lineIdx = 1; lineIdx <= 6; lineIdx++)
            {
                TriggernometryInterop.InvokeNamedCallback("PictoACT", AuxiliaryLinePictoACTCommand(lineIdx, true));
                TriggernometryInterop.InvokeNamedCallback("PictoACT", AuxiliaryLinePictoACTCommand(lineIdx, false));
            }
        }

        private void RemoveAuxiliaryLines()
        {
            TriggernometryInterop.InvokeNamedCallback("PictoACT", $"Action: Remove\nTag: {AuxiliaryLineTag}");
        }

        private int GetFakePartyIndex(Character me)
        {
            var myJob = me.Job;
            var role = JobsByRole.FirstOrDefault(pair => pair.Value.Contains(myJob)).Key;
            switch (role)
            {
                case JobRole.Tank:
                    return 1;
                case JobRole.Healer:
                    return 3;
                case JobRole.Melee:
                    return 5;
                case JobRole.PhysicalRanged:
                    return 7;
                case JobRole.MagicalRanged:
                    return 8;
                default:
                    return 5;
            }
        }

        private void SpreadPartyMembers(Character me, float minDistance)
        {
            var myPos = me.Pos;
            var members = new List<SimActor>();
            for (int i = 1; i <= 8; i++)
            {
                var member = EntityManager.GetPartyMember(i)
                    ?? throw new Exception($"无法获取队伍成员 {i}");
                if (member.Address != me.Address) members.Add(member);
            }

            // 整组位置全部通过校验后再移动队友，布置失败时由会话统一清理。
            var positions = PlanSpreadPositions(myPos, members.Count, minDistance, new Random());
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var pos = positions[i];
                member.Pos = pos;
                member.Heading = (float)(PI + Atan2(pos.X - myPos.X, pos.Y - myPos.Y));
            }
        }

        private static void LogCast(SimActor src, uint actionId, Vector3 pos, float heading)
        {
            TriggernometryInterop.QueueACTLogEvent($"[{DateTime.Now:HH:mm:ss.fff}] SimulantLog 14:{src.Id:X8}:casterName:{actionId:X2}:actionName:");
            TriggernometryInterop.QueueACTLogEvent($"[{DateTime.Now:HH:mm:ss.fff}] SimulantLog 107:{src.Id:X8}:{actionId:X2}:{pos.X}:{pos.Y}:{pos.Z}:{heading}");
        }

        private static void LogExecute(SimActor src, uint actionId)
        {
            TriggernometryInterop.QueueACTLogEvent($"[{DateTime.Now:HH:mm:ss.fff}] SimulantLog 15:{src.Id:X8}:casterName:{actionId:X2}:actionName:");
            TriggernometryInterop.QueueACTLogEvent($"[{DateTime.Now:HH:mm:ss.fff}] SimulantLog 108:{src.Id:X8}:{actionId:X2}:00000000:0::::");
        }
    }
}
