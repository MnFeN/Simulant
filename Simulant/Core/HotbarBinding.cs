using System;
using System.Collections.Generic;
using System.Linq;
using Simulant.Game.FFCS.Client.System.Input;
using Simulant.Game.FFCS.Client.UI.Misc;

namespace Simulant.Core
{
    /// <summary>每轮模拟固定的快捷键映射；不改写或拦截游戏键位。</summary>
    internal sealed class HotbarBinding
    {
        internal const uint LimitBreakGeneralAction = 3;
        internal readonly byte Key;
        internal readonly KeyModifierFlag Modifiers;
        internal readonly uint Action;
        internal readonly bool IsLimitBreak;
        private bool _down;

        internal HotbarBinding(byte key, KeyModifierFlag modifiers, uint action, bool isLimitBreak = false)
        {
            Key = key;
            Modifiers = modifiers;
            Action = action;
            IsLimitBreak = isLimitBreak;
        }

        internal bool Pressed(KeyStateFlags state, KeyModifierFlag modifiers, bool active)
        {
            var down = (state & KeyStateFlags.Down) != 0;
            var pressed = down && !_down && active && modifiers == Modifiers;
            _down = down;
            return pressed;
        }

        internal static List<HotbarBinding> Read(Keybind[] keybinds, RaptureHotbarModule.HotbarSlot[] slots, int mainBar,
            Func<uint, uint> generalAction, IReadOnlyDictionary<uint, uint> iconActions, uint limitBreakIcon)
        {
            var result = new List<HotbarBinding>();
            const int keysPerHotbar = 12;
            for (var bar = 0; bar < RaptureHotbarModule.StandardHotbarCount; bar++)
            {
                var source = bar == 0 ? mainBar : bar;
                if (source < 0 || source >= RaptureHotbarModule.StandardHotbarCount)
                    continue;
                for (var slot = 0; slot < keysPerHotbar; slot++)
                {
                    var hotbarSlot = slots[source * RaptureHotbarModule.SlotsPerHotbar + slot];
                    uint action;
                    var isLimitBreak = false;
                    switch (hotbarSlot.CommandType)
                    {
                        case RaptureHotbarModule.HotbarSlotType.Action:
                            action = hotbarSlot.CommandId;
                            break;
                        case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                            // 极限技的技能编号由释放时的量表和职业决定。
                            isLimitBreak = hotbarSlot.CommandId == LimitBreakGeneralAction;
                            action = isLimitBreak ? 0 : generalAction(hotbarSlot.CommandId);
                            break;
                        case RaptureHotbarModule.HotbarSlotType.Macro:
                            // 宏使用游戏已解析的 /micon 图标。
                            isLimitBreak = limitBreakIcon != 0 && hotbarSlot.IconId == limitBreakIcon;
                            iconActions.TryGetValue(hotbarSlot.IconId, out action);
                            break;
                        default:
                            continue;
                    }
                    if (!isLimitBreak && !Simulation.Runtime.SimEntityManager.LocalActions.Contains(action))
                        continue;
                    var keybind = keybinds[bar * keysPerHotbar + slot];
                    AddBinding(keybind.Primary);
                    AddBinding(keybind.Secondary);

                    void AddBinding(KeySetting setting)
                    {
                        if (setting.Key > 0 && setting.Key < KeyboardInputData.KeyCount)
                            result.Add(new HotbarBinding(setting.Key, setting.KeyModifier, action, isLimitBreak));
                    }
                }
            }
            return result;
        }
    }
}
