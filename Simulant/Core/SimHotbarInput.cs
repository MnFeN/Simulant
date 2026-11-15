using Simulant.ACT;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Game.FFCS.Client.UI;
using Simulant.Utils;
using System;
using System.Collections.Generic;

namespace Simulant.Core
{
    internal sealed class SimHotbarInput
    {
        private readonly UIInputData _input;
        private readonly int _processId;
        private readonly List<HotbarBinding> _bindings;
        private bool _focused;

        internal SimHotbarInput()
        {
            var ui = Framework.Instance.UIModule;
            _input = ui.InputData;
            _processId = NamazuInterop.Plugin.Memory.Process.Id;
            var data = GameDataService.Instance;
            var icons = new Dictionary<uint, uint>();
            foreach (var action in new uint[] { 3, 7548, 7559 })
                icons[data.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(action).Icon] = action;
            var general = data.GetSheet<Lumina.Excel.Sheets.GeneralAction>();
            _bindings = HotbarBinding.Read(_input.ReadHotbarKeybinds(), ui.HotbarModule.ReadStandardHotbars(),
                ui.AtkModule.CurrentHotbar(), id => general.TryGetRow(id, out var row) ? row.Action.RowId : 0, icons);
            Poll(_ => { });
        }

        internal void Poll(Action<uint> execute)
        {
            var focused = WindowFocus.IsProcessForeground(_processId);
            // 游戏过滤后的键盘状态在输入聊天时为空；重新切回游戏也不补发按住的键。
            var keyboard = _input.ReadKeyboard();
            foreach (var binding in _bindings)
            {
                if (binding.Pressed(keyboard, focused && _focused))
                    execute(binding.Action);
            }
            _focused = focused;
        }

    }
}
