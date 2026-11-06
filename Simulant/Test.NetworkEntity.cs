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
        private void BeginNetworkEntityTest(EntityBehaviorRun run, float castTime)
        {
            EnsureEntityTestReady();
            var player = _host.EntityProvider.GetMyself();
            if (player == null || !player.IsAlive)
                throw new InvalidOperationException("未找到有效的玩家实体，无法确定网络测试位置。");
            var origin = player.Pos3D;
            var heading = player.Heading;
            var forward = new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
            var start = new Vector3(new Vector2(origin.X, origin.Y) + forward * 5, origin.Z);
            var target = new Vector3(new Vector2(start.X, start.Y) + forward * 8, origin.Z);
            var moveHeading = heading + (float)Math.PI / 4;
            var spec = new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 4909, BNpcNameId = 3765, Level = 90, Pos = start, Heading = heading
            };
            var requestedAt = run.Scheduler.ElapsedSeconds;
            run.Entities.SpawnNetworkBNpc(spec, actor =>
            {
                run.Actors.Add(actor);
                CheckNetworkEntityTables(run, actor);
                CheckEntityTest(run, Vector3.Distance(actor.Pos3D, start) < 0.15f, "网络包中的生成坐标生效");
                LogEntityTest(run, $"网络角色 {actor.Id:X8} 在 {run.Scheduler.ElapsedSeconds - requestedAt:0.000} 秒后就绪，对象槽 {actor.Entity.Identity.Slot}。请观察模型、名称与可选中状态。");

                WaitForNetworkTargetability(run, actor, true, () =>
                {
                    CheckEntityTest(run, true, "网络角色默认可选中（标志位及客户端判断均为真）");
                    actor.Entity.SetTargetable(false);
                    WaitForNetworkTargetability(run, actor, false, () =>
                    {
                        CheckEntityTest(run, true, "ActorControl 已关闭可选中状态");
                        actor.Entity.SetTargetable(true);
                        WaitForNetworkTargetability(run, actor, true, () =>
                        {
                            CheckEntityTest(run, true, "ActorControl 已开启可选中状态");
                            BeginNetworkMovementTest(run, actor, spec, target, moveHeading, castTime);
                        });
                    });
                });
            });
        }

        private void BeginNetworkMovementTest(EntityBehaviorRun run, SimActor actor, NetworkNpcSpawnSpec spec,
            Vector3 target, float moveHeading, float castTime)
        {
            // 此段只发送移动包，不运行 SimActor.MoveTo 或直接写位置。
            actor.Entity.ActorMove(target, moveHeading, 40);
            var moveStartedAt = run.Scheduler.ElapsedSeconds;
            LogEntityTest(run, "已发送 ActorMove：向前 8 单位，朝向转动 45 度，速度参数 40（约每秒 4 单位）。请观察客户端移动插值和移动动画，移动方向与朝向有意不同。");
            WaitForNetworkMove(run, actor, target, moveStartedAt + 6, () =>
            {
                CheckEntityTest(run, actor.Entity.IsTargetable, "移动结束后仍可选中");
                var delta = actor.Heading - moveHeading;
                CheckEntityTest(run, Math.Abs(Math.Atan2(Math.Sin(delta), Math.Cos(delta))) < 0.02,
                    "ActorMove 的朝向生效");
                LogEntityTest(run, $"ActorMove 在 {run.Scheduler.ElapsedSeconds - moveStartedAt:0.000} 秒后到达；接下来读条并显示预兆。");
                actor.Cast(EntityTestActionId);
                run.Scheduler.ScheduleAfter(castTime + 0.5, () =>
                {
                    CheckEntityTest(run, actor.Entity.CastingActionId == 0 && actor.Entity.IsTargetable,
                        "网络角色完成读条且仍可选中");
                    var oldId = actor.Id;
                    var slot = actor.Entity.Identity.Slot;
                    run.Entities.Delete(actor);
                    CheckEntityTest(run, !actor.IsActive && !actor.Entity.IsAlive &&
                        CharacterManager.Instance().GetBattleCharaPtr(slot / 2) == IntPtr.Zero &&
                        GameObjectManager.Instance().Objects.IndexSorted[slot].IsNull(),
                        "Despawn 已清空战斗列表及对象表，旧引用失效");
                    run.Entities.SpawnNetworkBNpc(spec, next =>
                    {
                        run.Actors.Add(next);
                        CheckNetworkEntityTables(run, next);
                        CheckEntityTest(run, next.Id != oldId, "回收后重新生成使用新的实体身份");
                        WaitForNetworkTargetability(run, next, true, () =>
                        {
                            CheckEntityTest(run, true, "重新生成的角色默认可选中");
                            next.Entity.ActorMove(target, spec.Heading, 20);
                            LogEntityTest(run, "已重新生成并发送移动包，半秒后整体停止并回收。还可重新运行并立即停止，验证尚未就绪的生成请求能够取消。");
                            run.Scheduler.ScheduleAfter(0.5, () => StopEntityBehaviorTest(run, "网络测试正常完成"), "停止网络实体测试");
                        });
                    });
                }, "回收并重新生成网络角色");
            });
        }

        private void WaitForNetworkTargetability(EntityBehaviorRun run, SimActor actor, bool expected, Action ready)
        {
            // 登记早于模型加载完成；只轮询检查，不反复发送控制包掩盖后续状态覆盖。
            var deadline = run.Scheduler.ElapsedSeconds + 3;
            Action poll = null;
            poll = () =>
            {
                var flags = (ObjectTargetableFlags)actor.Entity.Native.TargetableStatus;
                var flag = (flags & ObjectTargetableFlags.IsTargetable) != 0;
                var targetable = actor.Entity.IsTargetable;
                var detail = $"实体 {actor.Id:X8}，可选中标志 0x{(byte)flags:X2}，客户端判断：{(targetable ? "可选中" : "不可选中")}";
                if (flag == expected && targetable == expected)
                {
                    LogEntityTest(run, detail);
                    ready();
                }
                else if (run.Scheduler.ElapsedSeconds >= deadline)
                    throw new TimeoutException($"等待{(expected ? "开启" : "关闭")}可选中状态超时：{detail}。");
                else
                    run.Scheduler.ScheduleAfter(0.05, poll, "等待网络角色可选中状态");
            };
            run.Scheduler.ScheduleAfter(0.05, poll, "等待网络角色可选中状态");
        }

        private void CheckNetworkEntityTables(EntityBehaviorRun run, SimActor actor)
        {
            var slot = actor.Entity.Identity.Slot;
            CheckEntityTest(run, slot >= 2 && slot < 200 && slot % 2 == 0 &&
                CharacterManager.Instance().GetBattleCharaPtr(slot / 2) == actor.Entity.Address &&
                GameObjectManager.Instance().Objects.IndexSorted[slot].Ptr == actor.Entity.Address,
                "角色已进入网络战斗列表，两个对象表指向同一实体");
        }

        private void WaitForNetworkMove(EntityBehaviorRun run, SimActor actor, Vector3 target, double deadline, Action arrived)
        {
            run.Scheduler.ScheduleAfter(0.05, () =>
            {
                if (!actor.IsActive) throw new InvalidOperationException("等待网络移动时角色意外失效。");
                if (Vector3.Distance(actor.Pos3D, target) < 0.15f)
                {
                    CheckEntityTest(run, true, "ActorMove 在未直接写坐标的情况下到达目标（容许网络坐标量化误差）");
                    arrived();
                }
                else if (run.Scheduler.ElapsedSeconds >= deadline)
                    throw new TimeoutException($"ActorMove 未到达目标，当前位置 {actor.Pos3D}，目标 {target}。");
                else
                    WaitForNetworkMove(run, actor, target, deadline, arrived);
            }, "等待客户端消费移动包");
        }
    }
}
