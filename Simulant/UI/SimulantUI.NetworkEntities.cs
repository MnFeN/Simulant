using Simulant.Core.Entity;
using Simulant.Simulation.Runtime;
using Simulant.Utils;
using System;
using System.Numerics;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class SimulantUI
    {
        private string _networkNpcInput = "4909, 3765, 90";
        private NetworkPlayerSpawnSpec _networkPlayerInput;

        private ContextMenuStrip ConfigureNetworkEntityMenu()
        {
            var menu = new ContextMenuStrip(components);
            var npc = menu.Items.Add("生成网络 NPC…", null, (_, __) => RunManualEntityAction(SpawnManualNetworkNpc));
            var player = menu.Items.Add("生成网络玩家…", null, (_, __) => RunManualEntityAction(SpawnManualNetworkPlayer));
            var local = menu.Items.Add("本地生成（备用）…", null, (_, __) => RunManualEntityAction(() => SpawnManualEntity(true)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("清理调试与手动生成实体", null, (_, __) => RunManualEntityAction(() =>
            {
                _host.StopDebugEntities();
                if (_host.HasPendingDebugEntityCleanup)
                    _host.LogWarning("调试已停止，部分实体仍待回收；请查看错误日志，再次执行清理可重试。");
                else
                    _host.LogSim("已停止调试测试并清理手动生成实体。");
            }));
            menu.Opening += (_, __) =>
            {
                var ready = _host.PluginReady && !_host.IsInitializing && _host.FirewallEnabled && !_switchingTerritory;
                npc.Enabled = ready && _host.GameDataReady && !_host.GameDataLoading;
                player.Enabled = ready;
                local.Enabled = _host.PluginReady && !_host.IsInitializing && !_host.Disposed && !_switchingTerritory;
            };
            return menu;
        }

        private void RunManualEntityAction(Action action)
        {
            try { action(); }
            catch (Exception error) { _host.LogError("手动实体操作失败：" + error); }
            finally { UpdateControlStates(); }
        }

        private Character GetManualSpawnSource()
        {
            if (!_host.PluginReady || _host.IsInitializing || _host.Disposed || !_host.FirewallEnabled || _switchingTerritory)
                throw new InvalidOperationException("请先初始化插件、开启防火墙，并等待区域切换完成。");
            var source = _host.EntityProvider.GetMyself();
            if (source == null || !source.IsAlive)
                throw new InvalidOperationException("未找到有效的自身玩家，无法确定生成位置。");
            return source;
        }

        private static Vector3 GetManualSpawnPosition(Character source)
        {
            var heading = source.Heading;
            return source.Pos3D + new Vector3((float)Math.Sin(heading) * 3, (float)Math.Cos(heading) * 3, 0);
        }

        private void SpawnManualNetworkNpc()
        {
            GetManualSpawnSource();
            string input;
            using (var form = new SimpleInputForm("生成网络 NPC",
                "依次填写：基础编号、名字编号（可选）、等级（可选）。\n例如：4909, 3765, 90\n\n角色将在你面前生成并保留，直到手动清理或退出模拟。", _networkNpcInput))
                input = form.GetInput();
            if (input == null) return;
            var (baseId, nameId, level) = input.ParseArgs<uint, uint?, uint?>((1, null), (2, null));
            // 对话框打开期间可能切图，提交时重新获取玩家并验证模拟状态。
            var source = GetManualSpawnSource();
            var spec = new NetworkNpcSpawnSpec
            {
                BNpcBaseId = baseId, BNpcNameId = nameId ?? 0, Level = checked((byte)(level ?? 1)),
                Pos = GetManualSpawnPosition(source), Heading = source.Heading
            };
            _host.DebugEntities.SpawnNetworkBNpc(spec, LogManualNetworkEntity);
            _networkNpcInput = input;
            _host.LogSim("网络 NPC 生成请求已提交，正在等待角色就绪。");
        }

        private void SpawnManualNetworkPlayer()
        {
            var source = GetManualSpawnSource();
            NetworkPlayerSpawnSpec spec;
            using (var form = new NetworkPlayerForm(source.Job, source.Level, _networkPlayerInput))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                spec = form.GetSpec();
            }
            source = GetManualSpawnSource();
            spec.Pos = GetManualSpawnPosition(source);
            spec.Heading = source.Heading;
            _host.DebugEntities.SpawnNetworkPlayer(spec, LogManualNetworkEntity);
            _networkPlayerInput = spec;
            _host.LogSim("网络玩家生成请求已提交，正在等待角色就绪。");
        }

        private void LogManualNetworkEntity(SimActor actor)
            => _host.LogSim($"网络角色已就绪：实体编号 {actor.Id:X8}，战斗列表槽号 {actor.Entity.Identity.Slot / 2}。" +
                "请检查外观与选中状态；角色会保留，可从“菜单 → 调试功能 → 添加实体”的右键菜单清理。");
    }
}
