using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Game.FFCS.Client.UI;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class SimulantUI
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys key);

        private void BindGroundTeleport()
        {
            var keyWasDown = false;
            var timer = new Timer(components) { Interval = 10 };
            timer.Tick += (_, __) =>
            {
                var key = _host.Configuration.GroundTeleportKey;
                var down = key != Keys.None && GetAsyncKeyState(key) < 0;
                var pressed = down && !keyWasDown;
                keyWasDown = down;
                if (!pressed)
                    return;

                try
                {
                    TeleportToCursor();
                }
                catch (Exception error)
                {
                    _host.LogWarning("鼠标传送失败：" + error.Message);
                }
            };
            timer.Start();
        }

        private void TeleportToCursor()
        {
            var connection = GameInterop.Connection;
            var world = _host.WorldGeneration;
            if (connection == null || !_host.CanRunDebug || !_host.FirewallEnabled || !_host.IsGameForeground)
                return;

            connection.RunInFrame(() =>
            {
                if (connection != GameInterop.Connection || world != _host.WorldGeneration ||
                    _host.IsSwitchingTerritory || _host.Disposed)
                    return;

                var ui = Framework.Instance.ThrowIfNull().UIModule.ThrowIfNull();
                if (ui.AtkModule.ThrowIfNull().IsTextInputActive())
                    return;

                var actions = ActionManager.Instance.ThrowIfNull();
                if (actions.AreaTargetingActionId.Get() != 0)
                    return;

                var player = _host.EntityProvider.GetMyself();
                if (player == null)
                    return;
                var point = actions.GetGroundTargetCandidate();
                if (!point.HasValue)
                    return;

                var p = point.Value;
                player.ActorSetPos(new Vector3(p.X, p.Z, p.Y), player.Heading);
                UIGlobals.PlaySoundEffect(2);
            });
        }
    }
}
