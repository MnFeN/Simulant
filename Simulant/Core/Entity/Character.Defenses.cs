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
    }
}
