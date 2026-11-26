using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.System.Input
{
    // FFXIVClientStructs/FFXIV/Client/System/Input/InputData.cs
    public struct InputData : IMemoryObject
    {
        private const int KeyboardInputsOffset = 0x500;
        private const int CurrentKeyModifierOffset = 0x995;

        public IntPtr Ptr { get; set; }

        public CursorInputData UIFilteredCursorInputs => Ptr.As<CursorInputData>(0x4A0);
        public CursorInputData CursorInputs => Ptr.As<CursorInputData>(0x4D0);
        internal MemoryField<int> NumKeybinds => Ptr.Field<int>(0x9B4);
        internal IntPtr Keybinds => Ptr.ReadPtr(0x9B8);

        internal KeyboardState ReadKeyboard() => Ptr.Read<KeyboardState>(KeyboardInputsOffset);

        internal Keybind[] ReadKeybinds(InputId first, InputId last)
        {
            if ((int)last >= NumKeybinds.Get())
                throw new InvalidOperationException("游戏快捷键列表尚未就绪。");

            return Keybinds.ArrayField<Keybind>((int)first * Keybind.Size, (int)last - (int)first + 1).Get();
        }

        // 同一次读取取出 KeyboardInputs 和紧随其后的 CurrentKeyModifier。
        [StructLayout(LayoutKind.Explicit, Size = CurrentKeyModifierOffset - KeyboardInputsOffset + 1)]
        internal struct KeyboardState
        {
            [FieldOffset(0)] public KeyboardInputData KeyboardInputs;
            [FieldOffset(CurrentKeyModifierOffset - KeyboardInputsOffset)] public KeyModifierFlag CurrentKeyModifier;
        }
    }

    internal enum InputId
    {
        HOTBAR_1_1 = 57,
        HOTBAR_10_B = 176,
    }
}
