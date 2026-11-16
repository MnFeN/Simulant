using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Core
{
    /// <summary>每轮模拟固定的快捷键映射；不改写或拦截游戏键位。</summary>
    internal sealed class HotbarBinding
    {
        internal readonly byte Key;
        internal readonly byte Modifiers;
        internal readonly uint Action;
        private bool _down;

        internal HotbarBinding(byte key, byte modifiers, uint action)
        {
            Key = key;
            Modifiers = modifiers;
            Action = action;
        }

        internal bool Pressed(byte[] keyboard, bool active)
        {
            var down = (keyboard[4 + Key * 4] & 1) != 0;
            var pressed = down && !_down && active && keyboard[0x495] == Modifiers;
            _down = down;
            return pressed;
        }

        internal static List<HotbarBinding> Read(byte[] keys, byte[] bars, int mainBar,
            Func<uint, uint> generalAction, IReadOnlyDictionary<uint, uint> iconActions)
        {
            var result = new List<HotbarBinding>();
            for (var bar = 0; bar < 10; bar++)
            {
                var source = bar == 0 ? mainBar : bar;
                if (source < 0 || source >= 10)
                    continue;
                for (var slot = 0; slot < 12; slot++)
                {
                    var offset = (source * 16 + slot) * 0xE8;
                    var id = BitConverter.ToUInt32(bars, offset + 0xB8);
                    uint action;
                    switch (bars[offset + 0xC7])
                    {
                        case 1: // Action
                            action = id;
                            break;
                        case 10: // GeneralAction，冲刺可使用通用技能编号。
                            action = generalAction(id);
                            break;
                        case 7: // Macro，使用游戏已解析的 /micon 图标，不解析或执行宏正文。
                            iconActions.TryGetValue(BitConverter.ToUInt32(bars, offset + 0xD0), out action);
                            break;
                        default:
                            continue;
                    }
                    if (!Simulation.Runtime.SimEntityManager.LocalActions.Contains(action))
                        continue;
                    for (var binding = 0; binding < 2; binding++)
                    {
                        var keyOffset = (bar * 12 + slot) * 11 + binding * 2;
                        var key = keys[keyOffset];
                        if (key > 0 && key < 159)
                            result.Add(new HotbarBinding(key, keys[keyOffset + 1], action));
                    }
                }
            }
            return result;
        }
    }
}
