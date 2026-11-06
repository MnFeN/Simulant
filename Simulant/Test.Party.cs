using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Group;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using Simulant.Simulation.Runtime;
using System;
using System.Numerics;

namespace Simulant
{
    internal partial class Test
    {
        private const ushort PartyTestRegen = 158;
        private const ushort PartyTestWeakness = 43;

        private void BeginPartyTest(EntityBehaviorRun run)
        {
            EnsureEntityTestReady();
            if (StatusManager.SetStatusFuncPtr == IntPtr.Zero || StatusManager.RemoveStatusFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("状态设置或移除函数尚未定位，无法运行小队列表测试。");
            var source = _host.EntityProvider.GetMyself();
            if (source == null || !source.IsAlive)
                throw new InvalidOperationException("未找到有效的自身玩家，无法准备测试小队。");
            var origin = source.Pos3D;
            var heading = source.Heading;
            var forward = new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
            var right = new Vector2(-forward.Y, forward.X);
            var members = new SimActor[8];
            members[0] = run.Entities.Attach(source);
            var jobs = new[] { Job.PLD, Job.WHM, Job.SCH, Job.MNK, Job.DRG, Job.BRD, Job.BLM };
            var names = new[] { "小队测试骑士", "小队测试白魔", "小队测试学者", "小队测试武僧", "小队测试龙骑", "小队测试诗人", "小队测试黑魔" };
            SimActor boss = null;
            var readyCount = 0;
            Action ready = () =>
            {
                if (++readyCount != 8) return;
                run.Entities.Party.AddRange(members);
                run.Entities.ShowParty(_host.PartyService);
                WaitForPartyTestRoster(run, members, boss, run.Scheduler.ElapsedSeconds + 5);
            };
            for (var i = 0; i < jobs.Length; i++)
            {
                var index = i + 1;
                var position = new Vector2(origin.X, origin.Y) + forward * 5 + right * ((i - 3) * 3);
                run.Entities.SpawnNetworkPlayer(new NetworkPlayerSpawnSpec
                {
                    Job = jobs[i], Name = names[i], AppearanceSource = source,
                    Pos = new Vector3(position, origin.Z), Heading = heading,
                    MaxHealthPoints = 10000, HealthPoints = 10000
                }, actor =>
                {
                    members[index] = actor;
                    run.Actors.Add(actor);
                    CheckNetworkEntityTables(run, actor);
                    ready();
                });
            }
            run.Entities.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 4909, BNpcNameId = 3765, Level = 90,
                Pos = new Vector3(new Vector2(origin.X, origin.Y) + forward * 12, origin.Z), Heading = heading
            }, actor =>
            {
                boss = actor;
                run.Actors.Add(actor);
                CheckNetworkEntityTables(run, actor);
                ready();
            });
        }

        private void WaitForPartyTestRoster(EntityBehaviorRun run, SimActor[] members, SimActor boss, double deadline)
        {
            run.Scheduler.ScheduleAfter(0.1, () =>
            {
                var group = GroupManager.Instance().ThrowIfNull().MainGroup;
                var matches = group.MemberCount.Get() == members.Length;
                for (var i = 0; matches && i < members.Length; i++)
                    matches = group.GetPartyMember(i).EntityId.Get() == members[i].Id;
                if (matches)
                {
                    CheckEntityTest(run, true, "原生小队包含八人，实体编号与指定顺序一致");
                    BeginPartyTestObservation(run, members, boss);
                }
                else if (run.Scheduler.ElapsedSeconds >= deadline)
                    throw new TimeoutException("原生小队名单未在五秒内更新为本次测试的八名成员。");
                else
                    WaitForPartyTestRoster(run, members, boss, deadline);
            }, "等待原生小队名单更新");
        }

        private void BeginPartyTestObservation(EntityBehaviorRun run, SimActor[] members, SimActor boss)
        {
            // 只操作本次生成的两名玩家；索引 0 是借用的自身，不修改其血量或状态。
            var first = members[1];
            var second = members[2];
            var firstStatusSlot = FindEmptyPartyTestStatusSlot(first);
            var secondStatusSlot = FindEmptyPartyTestStatusSlot(second);
            boss.Entity.ActorControl(0x04, 1);
            SetPartyTestBossTarget(boss, first);
            run.Entities.SetPartyEnmity(boss, 5, 100, 90, 70, 60, 50, 40, 30);
            LogEntityTest(run, $"八人小队已就绪，从现在开始观察三十秒。请选中前方 NPC（{boss.Id:X8}）查看仇恨：测试骑士最高、测试白魔其次；自身会临时进入战斗，结束时恢复原状态，不修改自身血量和状态效果。");

            run.Scheduler.ScheduleAfter(5, () =>
            {
                first.Entity.SetNetworkVitals(2500);
                second.Entity.SetNetworkVitals(5000);
                SetPartyTestStatus(first, firstStatusSlot, PartyTestRegen, 30);
                SetPartyTestStatus(second, secondStatusSlot, PartyTestWeakness, 30);
                LogEntityTest(run, "第一阶段：测试骑士降至 25% 血并获得“再生”，测试白魔降至 50% 血并获得“衰弱”。请观察小队血条和状态图标。");
                CheckPartyTestVitals(run, first, second, 2500, 5000, PartyTestRegen, PartyTestWeakness,
                    "首次血量和状态已写入模拟玩家");
            }, "小队测试降低血量并添加状态");

            run.Scheduler.ScheduleAfter(15, () =>
            {
                first.Entity.SetNetworkVitals(7500);
                second.Entity.SetNetworkVitals(2000);
                RemovePartyTestStatus(first, firstStatusSlot, PartyTestRegen);
                RemovePartyTestStatus(second, secondStatusSlot, PartyTestWeakness);
                SetPartyTestStatus(second, secondStatusSlot, PartyTestRegen, 30);
                SetPartyTestBossTarget(boss, second);
                run.Entities.SetPartyEnmity(boss, 10, 40, 100, 90, 80, 70, 60, 50);
                LogEntityTest(run, "第二阶段：测试骑士改为 75% 血且移除状态，测试白魔改为 20% 血并换成“再生”；测试白魔的仇恨变为 100，测试骑士变为 40。");
                CheckPartyTestVitals(run, first, second, 7500, 2000, 0, PartyTestRegen,
                    "第二次血量和状态已写入模拟玩家");
            }, "小队测试更新血量状态和仇恨");

            run.Scheduler.ScheduleAfter(23, () =>
            {
                first.Entity.SetNetworkVitals(first.Entity.MaxHP);
                second.Entity.SetNetworkVitals(second.Entity.MaxHP);
                RemovePartyTestStatus(second, secondStatusSlot, PartyTestRegen);
                LogEntityTest(run, "第三阶段：两名测试玩家恢复满血，本次添加的状态已移除；三十秒时恢复原小队并回收测试角色。");
                CheckPartyTestVitals(run, first, second, 10000, 10000, 0, 0,
                    "模拟玩家已恢复满血并移除测试状态");
            }, "小队测试恢复模拟玩家状态");

            run.Scheduler.ScheduleAfter(30, () => StopEntityBehaviorTest(run, "小队列表测试正常完成"),
                "恢复原小队并结束小队列表测试");
        }

        private static int FindEmptyPartyTestStatusSlot(SimActor actor)
        {
            var statuses = actor.Entity.Native.StatusManager;
            // 小队包包含前 30 项；测试也限制在这些槽中，避免把截断当成刷新失败。
            for (var i = 0; i < 30; i++)
                if (statuses.GetStatus(i).StatusId.Get() == 0) return i;
            throw new InvalidOperationException("模拟玩家没有可用于小队测试的空状态槽。");
        }

        private static void SetPartyTestBossTarget(SimActor boss, SimActor target)
            => PacketDispatcher.HandleActorControl(boss.Id, 0x32, 0, 0, 0, 0, 0, 0, 0, 0, target.Id, 0);

        private static void SetPartyTestStatus(SimActor actor, int slot, ushort statusId, float remaining)
        {
            var statuses = actor.Entity.Native.StatusManager;
            if (statuses.GetStatus(slot).StatusId.Get() != 0)
                throw new InvalidOperationException("小队测试的状态槽已被占用，拒绝覆盖其他状态。");
            statuses.SetStatus(slot, statusId, remaining, 0, new GameObjectId { ObjectId = actor.Id }, true);
            if (statuses.GetStatus(slot).StatusId.Get() != statusId)
                throw new InvalidOperationException("原生状态设置后未读到预期的测试状态。");
        }

        private static void RemovePartyTestStatus(SimActor actor, int slot, ushort expectedStatusId)
        {
            var statuses = actor.Entity.Native.StatusManager;
            var status = statuses.GetStatus(slot);
            var statusId = status.StatusId.Get();
            if (statusId == 0) return;
            if (statusId != expectedStatusId || status.SourceObject.Get().ObjectId != actor.Id)
                throw new InvalidOperationException("小队测试状态已被替换，拒绝移除不属于本次测试的状态。");
            statuses.RemoveStatus(slot);
            if (statuses.GetStatus(slot).StatusId.Get() != 0)
                throw new InvalidOperationException("原生状态移除后测试状态仍然存在。");
        }

        private void CheckPartyTestVitals(EntityBehaviorRun run, SimActor first, SimActor second,
            uint firstHp, uint secondHp, ushort firstStatus, ushort secondStatus, string description)
        {
            CheckEntityTest(run, first.Entity.HP == firstHp && second.Entity.HP == secondHp &&
                PartyTestStatusMatches(first.Entity.Native.StatusManager, firstStatus) &&
                PartyTestStatusMatches(second.Entity.Native.StatusManager, secondStatus), description);
            var group = GroupManager.Instance().MainGroup;
            var a = group.GetPartyMember(1);
            var b = group.GetPartyMember(2);
            CheckEntityTest(run, a.EntityId.Get() == first.Id && b.EntityId.Get() == second.Id &&
                a.CurrentHP.Get() == firstHp && b.CurrentHP.Get() == secondHp,
                $"原生小队血量已同步（骑士 {a.CurrentHP.Get()}，白魔 {b.CurrentHP.Get()}）");
            LogEntityTest(run, "角色和原生小队的血量均已核对，请再确认小队界面的血条、状态和仇恨显示。");
        }

        private static bool PartyTestStatusMatches(StatusManager statuses, ushort expectedStatus)
        {
            var bytes = statuses.Ptr.ReadBytes(PartyMember.StatusCount * 0x10, 0x8);
            var found = expectedStatus == 0;
            for (var i = 0; i < PartyMember.StatusCount; i++)
            {
                var statusId = BitConverter.ToUInt16(bytes, i * 0x10);
                if (statusId == expectedStatus) found = true;
                if ((statusId == PartyTestRegen || statusId == PartyTestWeakness) && statusId != expectedStatus)
                    return false;
            }
            return found;
        }
    }
}
