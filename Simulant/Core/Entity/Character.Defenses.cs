using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using System;

namespace Simulant.Core.Entity
{
    public sealed partial class Character
    {
        private static readonly ushort[] InvulnerabilityStatuses = { 82, 409, 810, 811, 1836 };

        internal bool IsKnockbackImmune => HasStatus(160) || HasStatus(1209);

        internal bool IsInvulnerable
        {
            get
            {
                foreach (var status in InvulnerabilityStatuses)
                    if (HasStatus(status))
                        return true;

                return false;
            }
        }

        internal bool ReadInvulnerability()
        {
            var statuses = Native.StatusManager.ReadStatusTable();
            for (var offset = 0; offset < statuses.Length; offset += StatusManager.StatusSize)
                if (Array.IndexOf(InvulnerabilityStatuses, StatusManager.ReadStatusId(statuses, offset)) >= 0)
                    return true;

            return false;
        }

        internal bool AreActionsLocked()
        {
            // 读取角色的实际状态，覆盖由其他会话施加的眩晕等限制。
            var statuses = Native.StatusManager.ReadStatusTable();
            var sheet = GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>();
            for (var offset = 0; offset < statuses.Length; offset += StatusManager.StatusSize)
            {
                var statusId = StatusManager.ReadStatusId(statuses, offset);
                if (statusId != 0 && sheet.TryGetRow(statusId, out var row) && row.LockActions)
                    return true;
            }

            return false;
        }
    }
}
