using Simulant.Core;
using Simulant.Game.FFCS.Client.Game.Gauge;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal sealed class WhiteMageActions : JobActions<WhiteMageActions, WhiteMageGauge>
    {
        private WhiteMageActions() : base(Job.WHM)
        {
        }

        internal override bool TryHandleAction(ActionRequest request) => false;

        internal override void OnSimStart()
        {
            var gauge = GetGauge();
            gauge.LilyTimer.Set(0);
            gauge.Lily.Set(3);
            gauge.BloodLily.Set(3);
        }

        internal override void OnSimEnd(bool canWritePlayerState)
        {
        }
    }
}
