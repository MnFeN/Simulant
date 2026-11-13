using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using System;
using System.Numerics;

namespace Simulant
{
    internal partial class Test
    {
        private void BeginNetworkPlayerTest(EntityBehaviorRun run)
        {
            EnsureEntityTestReady();
            var source = _host.EntityProvider.GetMyself();
            if (source == null || !source.IsAlive)
                throw new InvalidOperationException("未找到有效的自身玩家，无法复制外观。");
            var origin = source.Pos3D;
            var heading = source.Heading;
            var forward = new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
            var right = new Vector2(-forward.Y, forward.X);
            var jobs = new[] { Job.PLD, Job.WHM, Job.PCT };
            var names = new[] { "模拟骑士", "模拟白魔", "模拟画家" };
            var actors = new SimActor[jobs.Length];
            var readyCount = 0;
            for (var i = 0; i < jobs.Length; i++)
            {
                var index = i;
                var start = new Vector2(origin.X, origin.Y) + forward * 5 + right * ((i - 1) * 5);
                var spec = new NetworkPlayerSpawnSpec
                {
                    Job = jobs[i], Name = names[i], Pos = new Vector3(start, origin.Z), Heading = heading,
                    AppearanceSource = source
                };
                run.Entities.SpawnNetworkPlayer(spec, actor =>
                {
                    actors[index] = actor;
                    run.Actors.Add(actor);
                    CheckNetworkEntityTables(run, actor);
                    CheckEntityTest(run, actor.Entity.ObjectKind == ObjectKind.Pc && actor.Entity.Job == spec.Job,
                        $"{spec.Name} 的玩家类型与职业正确");
                    CheckEntityTest(run, Vector3.Distance(actor.Pos3D, spec.Pos) < 0.15f,
                        $"{spec.Name} 的出生位置正确");
                    LogEntityTest(run, $"{spec.Name}：实体 {actor.Id:X8}，网络槽 {actor.Entity.Identity.Slot / 2}；" +
                        (JobEquipment.HasSet(spec.Job) ? "使用 99 级职业蓝装，外貌复制自身。" : "没有对应的职业套装，保留自身装备与外貌。"));
                    WaitForNetworkTargetability(run, actor, true, () =>
                    {
                        CheckEntityTest(run, true, $"{spec.Name} 完成入场并可选中");
                        if (++readyCount != actors.Length) return;
                        LogEntityTest(run, "三名模拟玩家已就绪，停留四秒供检查外貌、装备、染色和名字；之后同时向前移动。");
                        run.Scheduler.ScheduleAfter(4, () => MoveNetworkPlayers(run, actors, forward, heading), "移动网络模拟玩家");
                    });
                });
            }
        }

        private void MoveNetworkPlayers(EntityBehaviorRun run, SimActor[] actors, Vector2 forward, float heading)
        {
            var arrivedCount = 0;
            foreach (var actor in actors)
            {
                var position = actor.Pos3D;
                var target = new Vector3(new Vector2(position.X, position.Y) + forward * 6, position.Z);
                actor.Entity.ActorMove(target, heading, 30);
                WaitForNetworkMove(run, actor, target, run.Scheduler.ElapsedSeconds + 6, () =>
                {
                    CheckEntityTest(run, actor.Entity.IsTargetable, $"模拟玩家 {actor.Id:X8} 移动后仍可选中");
                    if (++arrivedCount != actors.Length) return;
                    LogEntityTest(run, "三名模拟玩家均已到达，两秒后检查网络槽清空并结束测试。");
                    run.Scheduler.ScheduleAfter(2, () =>
                    {
                        foreach (var current in actors)
                        {
                            var slot = current.Entity.Identity.Slot;
                            run.Entities.Delete(current);
                            CheckEntityTest(run, !current.IsActive && !current.Entity.IsAlive &&
                                CharacterManager.Instance().GetBattleCharaPtr(slot / 2) == IntPtr.Zero &&
                                GameObjectManager.Instance().Objects.IndexSorted[slot].IsNull(),
                                $"模拟玩家 {current.Id:X8} 已回收，网络槽已清空");
                        }
                        StopEntityBehaviorTest(run, "网络玩家测试正常完成");
                    }, "回收网络模拟玩家");
                });
            }
        }
    }
}
