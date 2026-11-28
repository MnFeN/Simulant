using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Control;
using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Runtime.PlayerActions;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Core
{
    internal sealed partial class SimActionService
    {
        private const double AutoAttackInterval = 2.3;
        private const double AutoAttackPollInterval = 0.05;
        private const double PeriodicTickInterval = 3;

        private void StartPlayerTicks(SimScheduler scheduler, SimEntityManager entities)
        {
            double nextAttack = 0;
            EntityIdentity? attacker = null;

            void AutoAttack()
            {
                var player = Enabled ? _host.EntityProvider.GetMyself() : null;
                var now = scheduler.ElapsedSeconds;
                if (attacker != player?.Identity)
                {
                    attacker = player?.Identity;
                    nextAttack = now;
                }

                if (player != null && player.HP != 0 && now >= nextAttack && TryAutoAttack(entities, player))
                    nextAttack = now + AutoAttackInterval;

                if (scheduler.IsRunning)
                    scheduler.ScheduleAfter(AutoAttackPollInterval, AutoAttack, "检查本机自动攻击");
            }

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

            scheduler.ScheduleAfter(AutoAttackPollInterval, AutoAttack, "检查本机自动攻击");
            scheduler.Schedule(PeriodicTickInterval, Tick, "自然回复与持续效果");
        }

        private bool TryAutoAttack(SimEntityManager entities, Character player)
        {
            if (UIState.InstancePtr == IntPtr.Zero || TargetSystem.InstancePtr == IntPtr.Zero ||
                !UIState.Instance().WeaponState.AutoAttackState.IsAutoAttacking ||
                player.Native.CastInfo.IsCasting)
                return false;

            var selected = TargetSystem.Instance().Target.Get();
            if (selected == IntPtr.Zero)
                return false;

            var target = _host.EntityProvider.GetCharacters().FirstOrDefault(character => character.Address == selected);
            if (!PlayerActionRules.IsEnemy(target))
                return false;

            var job = player.Job;
            var ranged = job == Job.BRD || job == Job.MCH || job == Job.DNC;
            var actionId = ranged ? 8u : 7u;
            var range = ActionManager.GetActionRange(actionId);
            if (Vector2.Distance(player.Pos, target.Pos) > range + target.HitboxRadius + player.HitboxRadius ||
                player.AreActionsLocked())
                return false;

            // 7 和 8 是实战结算使用的近身／远程平A技能。技能表使用武器默认类型时按职业武器填写。
            byte weaponAttackType;
            if (ranged || job == Job.DRG || job == Job.RDM)
                weaponAttackType = 2;
            else if (job == Job.MNK || SimJobActions.For(job)?.Caster == true)
                weaponAttackType = 3;
            else
                weaponAttackType = 1;
            entities.ResolveAutoAttack(player, target, actionId, weaponAttackType);
            if (job == Job.PLD)
                PaladinActions.OnAutoAttack();
            return true;
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
