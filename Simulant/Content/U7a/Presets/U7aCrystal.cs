using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    /// <summary>两个 P4 练习共用的水晶与浮空幻影场景。</summary>
    internal static class U7aCrystal
    {
        internal static readonly Vector2 Position = new Vector2(100, 84);

        /// <summary>在原有范围判定中检查水晶，命中时保留原演出并追加击碎和失败处理。</summary>
        internal static AreaMechanic Protect(AreaMechanic mechanic, Func<Vector2, bool> intersects,
            Action breakCrystal, Action<MechanicResult> reportFailure)
        {
            var centerAtResolve = mechanic.CenterAtResolve;
            var check = mechanic.Check;
            var visual = mechanic.Visual;
            var failed = mechanic.Failed;
            var hit = false;
            mechanic.CenterAtResolve = snapshot =>
            {
                var center = centerAtResolve?.Invoke(snapshot) ?? mechanic.Center;
                hit = intersects(center);
                if (hit)
                    mechanic.FailureSeverity = MechanicFailureSeverity.Fatal;
                return center;
            };
            mechanic.Check = members =>
            {
                var reason = check?.Invoke(members);
                return hit ? (reason == null ? "" : reason + " ") + "点名范围击碎了北侧水晶。" : reason;
            };
            mechanic.Visual = result =>
            {
                visual?.Invoke(result);
                if (hit)
                    breakCrystal();
            };
            mechanic.Failed = result =>
            {
                if (hit)
                    reportFailure(result);
                else
                    failed?.Invoke(result);
            };
            return mechanic;
        }

        internal static void Spawn(SimEntityManager entities, SimScheduler scheduler, Action<SimActor> ready, uint health = 169245)
        {
            // 水晶、琳和盖娅是三个独立实体；幻影先播放回放中的动作，再切换冻结姿势。
            var specs = new[]
            {
                new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17841, BNpcNameId = 13559, LayoutId = 0xA55C07,
                    Pos = new Vector3(Position, 0), Heading = 0,
                    MaxHealthPoints = 201481, HealthPoints = health, DisplayFlags = 0x40003,
                    EnemyListVisibility = false
                },
                new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17844, BNpcNameId = 9829, LayoutId = 0xA5AF23,
                    Pos = new Vector3(Position + new Vector2(0.7124f, 0), 0), Heading = -0.837806f,
                    MaxHealthPoints = 188300, ModelState = 7, Targetable = false
                },
                new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17845, BNpcNameId = 9830, LayoutId = 0xA5AF24,
                    Pos = new Vector3(Position - new Vector2(0.7124f, 0), 0), Heading = 1.221691f,
                    MaxHealthPoints = 188300, ModelState = 121, Targetable = false
                }
            };

            foreach (var spec in specs)
            {
                spec.Level = 100;
                spec.CharacterDataFlags = 1;

                // 回放中的实体高度约为 0，视觉抬升由永久「浮空」状态控制
                if (spec.BNpcBaseId != 17841)
                    spec.InitialStatuses = new[]
                    {
                        new CommonSpawnData.StatusEffect
                        {
                            StatusId = 1390, Param = 200, SourceObjectId = 0xE0000000
                        }
                    };

                entities.SpawnNetworkBNpc(spec, actor =>
                {
                    if (spec.BNpcBaseId == 17841)
                    {
                        ready(actor);
                        return;
                    }

                    // 不继承前一阶段的显示标志；保留不可选中，但让人物正常绘制。
                    var ryne = spec.BNpcBaseId == 17844;
                    actor.ActorControl(ActorControlCategory.PlayActionTimeline, ryne ? 11415u : 3207u);
                    scheduler.ScheduleAfter(1.084, () =>
                    {
                        actor.ActorControl(ActorControlCategory.SetModelState, ryne ? 5u : 31u);
                        ready(actor);
                    }, "冻结水晶内的幻影");
                });
            }
        }
    }
}
