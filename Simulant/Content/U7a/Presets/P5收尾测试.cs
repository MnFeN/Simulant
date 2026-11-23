using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Game.FFCS.Client.Network;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(5.01)]
    internal sealed class P5收尾测试 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P5 收尾测试";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 23);
        public override PhaseData Phase => U7a.P5;
        public override int Level => 100;
        public override Type SimLogicType => typeof(PandoraClearTestLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "从失乐园读条开始，检查 P5 的过本收尾。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new CheckBoxOption(nameof(PandoraClearTestLogic.UseNativeClearEvent), "播放原生过场（测试）", false,
                "开启防火墙后，按回放执行副本过本 Event。")
        };
    }

    internal sealed class PandoraClearTestLogic : PandoraLogic
    {
        private const byte ClearEventType = 0x0D;
        private uint _clearEventId;

        [SimOption] public bool UseNativeClearEvent { get; set; }

        public PandoraClearTestLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 1)
        {
        }

        protected override double OpeningLeadIn => 0;

        protected override void SchedulePrelude()
        {
            Pandora.Entity.SetNetworkVitals(4_419_532);
            Pandora.SetTargetable(true);
            EntityManager.ShowLimitBreakGauge(Party[Self], 3, 30_000);
        }

        protected override SimTimeline BuildExercise() => ParadiseLost();

        protected override void StartClearTransition()
        {
            if (!UseNativeClearEvent)
            {
                base.StartClearTransition();
                return;
            }

            if (!_host.FirewallService.IsEnabled)
                throw new InvalidOperationException("播放原生过场测试需要开启防火墙。");

            var eventId = _host.ZoneService.ContentDirectorId;
            if (eventId == 0 || !PacketDispatcher.EventHandlersReady)
                throw new InvalidOperationException("当前副本的原生过场入口未就绪。");

            // 国服 7.58 两份回放的 EventStart 与 EventPlay8 参数一致。
            var playerId = (ulong)Party[Self].Id;
            _clearEventId = eventId;
            PacketDispatcher.HandleEventStartPacket(eventId, playerId, ClearEventType, 1, 1);
            PacketDispatcher.HandleEventPlayPacket(playerId, eventId, 3, 0x08502403,
                new uint[] { 3560, 1, 18, 0, 0 });
        }

        protected override void FinishClearTransition(uint frames)
        {
            if (!UseNativeClearEvent)
            {
                base.FinishClearTransition(frames);
                return;
            }

            if (_clearEventId != 0)
            {
                PacketDispatcher.HandleEventFinishPacket(_clearEventId, ClearEventType, 1, 1);
                _clearEventId = 0;
            }
        }
    }
}
