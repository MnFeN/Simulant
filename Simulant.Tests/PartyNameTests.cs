using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Text;

namespace Simulant.Tests
{
    internal static class PartyNameTests
    {
        internal static void PartyNamesCombineRoleAndActualJob()
        {
            Assert(SimPartyNames.Get(1, Job.DRK) == "MT黑骑" && SimPartyNames.Get(5, Job.VPR) == "D1蝰蛇" &&
                SimPartyNames.Get(3, Job.WHM) == "H1白魔" && SimPartyNames.Get(8, Job.PCT) == "D4绘灵",
                "名字应由位置职能加实际职业的中文简称组成。");
            Throws<ArgumentOutOfRangeException>(() => SimPartyNames.Get(0, Job.DRK));
            Throws<ArgumentOutOfRangeException>(() => SimPartyNames.Get(9, Job.VPR));
            Throws<ArgumentOutOfRangeException>(() => SimPartyNames.Get(1, (Job)255));
        }

        internal static void TemporaryNamesRestoreBorrowedPlayersOnStopAndDetach()
        {
            using (var scene = new Scene())
            {
                scene.Manager.SetTemporaryName(scene.Player, "MT黑骑");
                Assert(scene.Name == "MT黑骑" && scene.Manager.HasPendingCleanup, "改名后应持有原名恢复责任。");
                scene.Manager.Clear();
                Assert(scene.Name == "原来的名字" && scene.Player.Entity.IsAlive && !scene.Manager.HasPendingCleanup,
                    "停止时必须还原借用玩家原名，且不回收玩家。");
            }
            using (var scene = new Scene())
            {
                scene.Manager.SetTemporaryName(scene.Player, "D1蝰蛇");
                scene.Manager.Detach(scene.Player);
                Assert(scene.Name == "原来的名字" && !scene.Player.IsActive && !scene.Manager.HasPendingCleanup,
                    "直接解除关联也必须先还原名字。");
            }
        }

        internal static void TemporaryNamesRetainRestoreResponsibilityAfterFailedCalls()
        {
            using (var scene = new Scene())
            using (var nextScheduler = new SimScheduler())
            {
                scene.Player.Entity.AfterNameWrite = () => { throw new InvalidOperationException("改名生效后返回失败。"); };
                Throws<InvalidOperationException>(() => scene.Manager.SetTemporaryName(scene.Player, "MT黑骑"));
                scene.Player.Entity.AfterNameWrite = null;
                scene.Player.Entity.BeforeNameWrite = () => { throw new InvalidOperationException("恢复暂时失败。"); };
                scene.Manager.Clear();
                Assert(scene.Name == "MT黑骑" && scene.Manager.HasPendingCleanup, "恢复失败必须保留记录以便下次重试。");
                Throws<InvalidOperationException>(() => scene.Manager.Scheduler = nextScheduler);
                scene.Player.Entity.BeforeNameWrite = null;
                scene.Manager.Clear();
                Assert(scene.Name == "原来的名字" && !scene.Manager.HasPendingCleanup, "再次停止应还原原名并解除重启限制。");
                scene.Manager.Scheduler = nextScheduler;
            }
        }

        internal static void TemporaryNamesPreserveExternalChangesAndReusedEntities()
        {
            using (var scene = new Scene())
            {
                scene.Manager.SetTemporaryName(scene.Player, "MT黑骑");
                scene.Player.Entity.SetNameBytes(Encode("外部新名字"));
                scene.Manager.Clear();
                Assert(scene.Name == "外部新名字" && !scene.Manager.HasPendingCleanup, "别名被外部替换后不得覆盖新名字。");
            }
            using (var scene = new Scene())
            {
                scene.Manager.SetTemporaryName(scene.Player, "MT黑骑");
                scene.Spawner.World.Registry.Reset();
                scene.Player.Entity.BeforeNameWrite = () => { throw new Exception("不能写旧世界角色。"); };
                scene.Manager.Clear();
                Assert(!scene.Manager.HasPendingCleanup, "跨世界或槽位复用后应放弃旧名字恢复，不能写入复用地址。");
            }
        }

        internal static void TemporaryNamesRejectRepeatedOrForeignOwners()
        {
            using (var scene = new Scene())
            using (var scheduler = new SimScheduler())
            {
                var other = new SimEntityManager(scene.Spawner) { Scheduler = scheduler };
                Throws<InvalidOperationException>(() => other.SetTemporaryName(scene.Player, "MT黑骑"));
                Assert(scene.Name == "原来的名字" && !other.HasPendingCleanup, "不能修改未关联的借用玩家。");
                scene.Manager.SetTemporaryName(scene.Player, "MT黑骑");
                Throws<InvalidOperationException>(() => scene.Manager.SetTemporaryName(scene.Player, "ST黑骑"));
                scene.Manager.Clear();
                Assert(scene.Name == "原来的名字", "重复请求不得覆盖首次原名快照。");
                other.Clear();
            }
        }

        private static byte[] Encode(string name)
        {
            var result = new byte[64];
            var bytes = Encoding.UTF8.GetBytes(name);
            Array.Copy(bytes, result, bytes.Length);
            return result;
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
        private sealed class Scene : IDisposable
        {
            internal readonly EntitySpawner Spawner = new EntitySpawner();
            internal readonly SimEntityManager Manager;
            internal readonly SimActor Player;
            private readonly SimScheduler _scheduler = new SimScheduler();
            internal string Name => Encoding.UTF8.GetString(Player.Entity.NameBytes).TrimEnd('\0');
            internal Scene()
            {
                Manager = new SimEntityManager(Spawner) { Scheduler = _scheduler };
                Player = Manager.Attach(Spawner.World.CreateCharacter(0));
                Player.Entity.NameBytes = Encode("原来的名字");
            }
            public void Dispose()
            {
                Player.Entity.BeforeNameWrite = null;
                Player.Entity.AfterNameWrite = null;
                _scheduler.Dispose();
                Manager.Clear();
            }
        }
    }
}
