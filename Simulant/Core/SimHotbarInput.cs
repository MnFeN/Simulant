using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Game.FFCS.Client.System.Input;
using Simulant.Game.FFCS.Client.UI;
using Simulant.Game.FFCS.Component.GUI;
using Simulant.Utils;
using System;
using System.Collections.Generic;

namespace Simulant.Core
{
    internal sealed class SimHotbarInput
    {
        private readonly InputData _input;
        private readonly int _processId;
        private readonly List<HotbarBinding> _bindings;
        private bool _focused;

        internal SimHotbarInput()
        {
            var ui = Framework.Instance.UIModule;
            _input = ui.InputData;
            _processId = GameInterop.Connection.Memory.Process.Id;
            var data = GameDataService.Instance;
            var icons = new Dictionary<uint, uint>();
            foreach (var action in Simulation.Runtime.SimEntityManager.LocalActions)
                icons[data.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(action).Icon] = action;
            var general = data.GetSheet<Lumina.Excel.Sheets.GeneralAction>();
            var limitBreakIcon = general.TryGetRow(HotbarBinding.LimitBreakGeneralAction, out var row) ? (uint)row.Icon : 0;
            var keybinds = _input.ReadKeybinds(InputId.HOTBAR_1_1, InputId.HOTBAR_10_B);
            var hotbar = ui.HotbarModule;
            if (!hotbar.ModuleReady)
                throw new InvalidOperationException("游戏快捷栏尚未就绪。");

            _bindings = HotbarBinding.Read(keybinds, hotbar.StandardHotbarSlots.Get(),
                ReadCurrentHotbarPage(ui.AtkModule), id => general.TryGetRow(id, out var action) ? action.Action.RowId : 0, icons, limitBreakIcon);
            Poll((_, __) => { });
        }

        private static byte ReadCurrentHotbarPage(AtkModule atk)
            => atk.AtkUnitManager.GetAddonByName("_ActionBar").As<AddonActionBar>()
                .ThrowIfNull("游戏主快捷栏尚未就绪。").HotbarId.Get();

        internal void Poll(Action<uint, bool> execute)
        {
            var focused = WindowFocus.IsProcessForeground(_processId);
            // 游戏过滤后的键盘状态在输入聊天时为空；重新切回游戏也不补发按住的键。
            var keyboard = _input.ReadKeyboard();
            foreach (var binding in _bindings)
            {
                if (binding.Pressed(keyboard.KeyboardInputs.GetKeyState(binding.Key), keyboard.CurrentKeyModifier, focused && _focused))
                    execute(binding.Action, binding.IsLimitBreak);
            }
            _focused = focused;
        }

    }
}
