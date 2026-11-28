using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game;
using System.Collections.Generic;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using static Simulant.Simulation.Runtime.PlayerActions.PlayerActionRules;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal static class GeneralActions
    {
        internal enum ActionId : uint
        {
            冲刺 = 3,
            挑衅 = 7533,
            退避 = 7537,
            亲疏自行 = 7548,
            沉稳咏唱 = 7559,
            即刻咏唱 = 7561
        }

        private enum GeneralActionId : uint
        {
            冲刺 = 4
        }

        internal enum StatusId : ushort
        {
            冲刺 = 50,
            复活 = 148,
            沉稳咏唱 = 160,
            即刻咏唱 = 167,
            铁壁 = 1191,
            亲疏自行 = 1209
        }

        internal static bool TryHandleAction(ActionRequest request)
        {
            var player = PluginHost.Instance?.EntityProvider?.GetMyself();
            if (player == null)
                return false;

            var action = (ActionId)request.ActionId;
            if (request.ActionType == ActionType.GeneralAction)
            {
                if ((GeneralActionId)request.ActionId != GeneralActionId.冲刺)
                    return false;
                action = ActionId.冲刺;
            }

            var effects = new List<ActionEffect>();
            switch (action)
            {
                case ActionId.冲刺:
                    // 状态参数 30 表示冲刺的加速百分比。
                    AddSelfStatus(player, effects, StatusId.冲刺, 10, param: 30);
                    break;
                case ActionId.亲疏自行:
                    AddSourceStatus(player, effects, StatusId.亲疏自行, 6, param: 100, effectParam: 67);
                    break;
                case ActionId.沉稳咏唱:
                    AddSelfStatus(player, effects, StatusId.沉稳咏唱, 6);
                    break;
                case ActionId.即刻咏唱:
                    AddSelfStatus(player, effects, StatusId.即刻咏唱, 10);
                    break;
                case ActionId.挑衅:
                    UseEnmityAction(player, request, provoke: true);
                    return true;
                case ActionId.退避:
                    UseEnmityAction(player, request, provoke: false);
                    return true;
                default:
                    return false;
            }

            ResolvePlayerAction(player, request, player, effects, actionId: (uint)action);
            return true;
        }

        private static void UseEnmityAction(Character player, ActionRequest request, bool provoke)
        {
            if (PluginHost.Instance.PlayerActions.EncounterEntities?.TryUseEnmityAction(player, request, provoke) != true)
                player.RejectActionResult(request, 582);
        }
    }
}
