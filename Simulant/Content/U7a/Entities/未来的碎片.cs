using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Numerics;

namespace Simulant.Content.U7a.Entities
{
    /// <summary>两个 P4 练习共用的水晶与浮空幻影场景。</summary>
    internal sealed class 未来的碎片
    {
        internal static readonly Vector2 Position = new Vector2(100, 84);
        internal SimActor Actor { get; }
        internal Vector2 Pos => Actor.Pos;
        internal float HitboxRadius => Actor.Entity.HitboxRadius;
        private readonly Action<uint, ushort> _map;
        private readonly Action<string> _logHit;
        private bool _broken;

        internal 未来的碎片(SimActor actor, Action<uint, ushort> map, Action<string> logHit = null)
        {
            Actor = actor;
            _map = map;
            _logHit = logHit;
        }

        /// <summary>在原有范围判定中检查水晶，命中时保留原演出并追加击碎和失败处理。</summary>
        internal AreaMechanic Protect(AreaMechanic mechanic, int target, int self, Action<MechanicError, MechanicResult> report)
        {
            var centerAtResolve = mechanic.CenterAtResolve;
            var check = mechanic.Check;
            var visual = mechanic.Visual;
            var failed = mechanic.Failed;
            var hit = false;
            mechanic.CenterAtResolve = snapshot =>
            {
                var center = centerAtResolve?.Invoke(snapshot) ?? mechanic.Center;
                var crystalPosition = Pos;
                var radius = HitboxRadius;
                hit = !_broken && mechanic.Area.IntersectsCircle(crystalPosition, radius, center, mechanic.Heading);
                if (hit)
                {
                    mechanic.FailureSeverity = MechanicFailureSeverity.Fatal;
                    _logHit?.Invoke(FormattableString.Invariant($"{mechanic.Name} 水晶命中判定：技能中心 ({center.X:0.000}, {center.Y:0.000})，水晶 ({crystalPosition.X:0.000}, {crystalPosition.Y:0.000})，水晶半径 {radius:0.000} 米，中心距离 {Vector2.Distance(center, crystalPosition):0.000} 米。"));
                }
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
                    Break();
            };
            mechanic.Failed = result =>
            {
                if (hit)
                    report(MechanicError.Rule(mechanic.Name, new[] { target }, result.Reason)
                        .WithAttribution(target == self ? new[] { self } : Array.Empty<int>(), "自身点名范围击中了北侧水晶"), result);
                else
                    failed?.Invoke(result);
            };
            return mechanic;
        }

        private void Break()
        {
            if (_broken)
                return;
            _broken = true;
            Actor.StopCasting();
            Actor.Entity.SetNetworkVitals(0);
            Actor.ActorControl(ActorControlCategory.DeathAnimation);
            Actor.ActorControl(ActorControlCategory.SetStatus, 2);
            _map(0x2E, 0x10);
        }

        internal static void Spawn(SimEntityManager entities, SimScheduler scheduler, Action<SimActor> crystalReady,
            Action ready, uint health = 169245)
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
                        crystalReady(actor);
                        ready();
                        return;
                    }

                    // 不继承前一阶段的显示标志；保留不可选中，但让人物正常绘制。
                    var ryne = spec.BNpcBaseId == 17844;
                    actor.ActorControl(ActorControlCategory.PlayActionTimeline, ryne ? 11415u : 3207u);
                    scheduler.ScheduleAfter(1.084, () =>
                    {
                        actor.ActorControl(ActorControlCategory.SetModelState, ryne ? 5u : 31u);
                        ready();
                    }, "冻结水晶内的幻影");
                });
            }
        }
    }
}
