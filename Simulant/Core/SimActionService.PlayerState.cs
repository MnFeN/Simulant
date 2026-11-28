using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.UI;

namespace Simulant.Core
{
    internal sealed partial class SimActionService
    {
        private byte? _levelBeforeFirewall;

        internal void RecordPlayerLevel() => _levelBeforeFirewall = _host.EntityProvider.GetMyself()?.Level;

        private void PrepareSimPlayer(byte level)
        {
            GameInterop.Connection.RunInFrame(() =>
            {
                var player = _host.EntityProvider.GetMyself();
                SetSimLevel(player, level, player.MaxHP, checked((ushort)player.MaxMp));

                SelectJobActions(player.Job)?.OnSimStart();
            });
        }

        internal void ForgetSimPlayer() => _levelBeforeFirewall = null;

        internal void RestoreSimPlayer()
        {
            if (!(_levelBeforeFirewall is byte level))
                return;

            if (GameInterop.IsReady)
            {
                Reset();
                var player = _host.EntityProvider.GetMyself();
                if (player != null)
                    GameInterop.Connection.RunInFrame(() => SetSimLevel(player, level));
            }
            ForgetSimPlayer();
        }

        private static void SetSimLevel(Character player, byte level, uint? hp = null, ushort? mp = null)
        {
            var state = PlayerState.Instance();
            var syncedLevel = level == state.CurrentLevel.Get() ? (byte)0 : level;
            player.SetNetworkVitals(hp, mp, level, syncedLevel);
            state.SetSyncedLevel(syncedLevel);
        }
    }
}
