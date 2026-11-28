using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Core
{
    internal sealed partial class SimActionService
    {
        private const double PeriodicTickInterval = 3;

        private void StartPlayerTicks(SimScheduler scheduler, SimEntityManager entities)
        {
            void Tick()
            {
                if (Enabled && _host.EntityProvider.GetMyself() is Character player)
                {
                    if (player.HP != 0)
                        RecoverVitals(entities, player);
                    entities.TickPeriodicEffects();
                    var encounter = EncounterEntities;
                    if (!ReferenceEquals(encounter, entities))
                        encounter?.TickPeriodicEffects();
                }

                if (scheduler.IsRunning)
                {
                    // 按会话起点对齐三秒刻度，调度延迟时跳过已经错过的刻度。
                    var next = (Math.Floor(scheduler.ElapsedSeconds / PeriodicTickInterval) + 1) * PeriodicTickInterval;
                    scheduler.Schedule(next, Tick, "自然回复与持续效果");
                }
            }

            scheduler.Schedule(PeriodicTickInterval, Tick, "自然回复与持续效果");
        }

        private static void RecoverVitals(SimEntityManager entities, Character player)
        {
            var inCombat = player.Native.InCombat.Get();
            var currentHp = player.HP;
            var maxHp = player.MaxHP;
            var currentMp = player.MP;
            var maxMp = player.MaxMp;

            // 国服 ACT 血蓝同步样本：战斗中每次恢复 1% HP 和 200 MP，脱战 10% HP 和 600 MP。
            var hp = (uint)Math.Min(maxHp, (ulong)currentHp + maxHp / (inCombat ? 100u : 10u));
            var mp = currentMp;
            if (!HasAstralFire(player))
                mp = (uint)Math.Min(maxMp, (ulong)currentMp + (inCombat ? 200u : 600u));
            if (hp == currentHp && mp == currentMp)
                return;

            player.SetNetworkVitals(hp, checked((ushort)mp));
            entities.CombatLog?.HealthAndMana(player);
        }

        private static bool HasAstralFire(Character player)
        {
            if (player.Job != Job.BLM || JobGaugeManager.InstancePtr == IntPtr.Zero)
                return false;

            var manager = JobGaugeManager.Instance();
            var gauge = manager.BlackMage;
            // 从国服 GetActionCost 的黑魔法师分支可核对：量谱的有符号属性值大于零表示星极火。
            return manager.ClassJobId.Get() == (byte)Job.BLM && manager.CurrentGauge == gauge.Ptr &&
                gauge.ElementStance.Get() > 0;
        }
    }
}
