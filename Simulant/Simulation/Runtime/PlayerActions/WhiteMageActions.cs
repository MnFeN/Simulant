using Simulant.Game.FFCS.Client.Game.Gauge;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;
using static Simulant.Simulation.Runtime.PlayerActions.PlayerActionRules;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal static class WhiteMageActions
    {
        static WhiteMageActions()
        {
            JobActionsOnSimStart.Add(Job.WHM, OnSimStart);
        }

        private static WhiteMageGauge GetGauge()
            => PlayerActionRules.GetGauge<WhiteMageGauge>(Job.WHM);

        private static void OnSimStart()
        {
            var gauge = GetGauge();
            gauge.LilyTimer.Set(0);
            gauge.Lily.Set(3);
            gauge.BloodLily.Set(3);
        }
    }
}
