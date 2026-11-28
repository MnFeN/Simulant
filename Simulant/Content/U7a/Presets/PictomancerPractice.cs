using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(2.6)]
    internal sealed class PictomancerPractice : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "绘灵法师木人练习";
        public override string Author => "Simulant";
        public override DateTime LastUpdated => new DateTime(2026, 11, 28);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(PictomancerPracticeLogic);
        public override string Description => "选中本地木人，用游戏热栏练习绘灵法师的读条、滑步、连段和爆发。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>();
    }

    internal sealed class PictomancerPracticeLogic : SimLogicBase
    {
        private Character _enteredCombat;

        public PictomancerPracticeLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            var player = _host.EntityProvider.GetMyself();
            if (player?.Job != Job.PCT)
                throw new InvalidOperationException("请切换到绘灵法师后开始练习。");

            var heading = player.Heading;
            var position = player.Pos3D + new Vector3((float)Math.Sin(heading) * 6, (float)Math.Cos(heading) * 6, 0);
            var dummy = new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 0x385,
                BNpcNameId = 541,
                Level = 100,
                Pos = position,
                Heading = heading,
                MaxHealthPoints = 1_000_000
            };
            timeline.At(0, () => EntityManager.SpawnNetworkBNpc(dummy, _ =>
            {
                if (player.IsAlive && !player.Native.InCombat.Get())
                {
                    _enteredCombat = player;
                    player.ActorControl(ActorControlCategory.SetBattle, 1);
                }
            }), "生成绘灵练习木人");
        }

        protected override void OnStop()
        {
            if (_enteredCombat?.IsAlive == true)
                _enteredCombat.ActorControl(ActorControlCategory.SetBattle, 0);
            _enteredCombat = null;
        }
    }
}
