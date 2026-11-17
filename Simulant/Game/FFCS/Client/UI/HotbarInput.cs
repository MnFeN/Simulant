using System;
using System.Text;

namespace Simulant.Game.FFCS.Client.UI
{
    // 精简迁移 FFXIVClientStructs 的 InputData、RaptureHotbarModule 与 AddonActionBar。
    // 来源：FFXIVClientStructs/FFXIV/Client/System/Input/InputData.cs 与 Client/UI。
    public struct UIInputData : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public byte[] ReadKeyboard() => Ptr.ReadBytes(0x496, 0x500);

        public byte[] ReadHotbarKeybinds()
        {
            if (Ptr.Read<int>(0x9B4) < 177)
                throw new InvalidOperationException("游戏快捷键列表尚未就绪。");
            // 普通快捷栏的 InputId 为 57～176，每项含两组键盘绑定。
            return Ptr.ReadPtr(0x9B8).ReadBytes(120 * 11, 57 * 11);
        }
    }

    public struct RaptureHotbarModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public byte[] ReadStandardHotbars()
        {
            if (!Ptr.Read<bool>(0x58))
                throw new InvalidOperationException("游戏快捷栏尚未就绪。");
            return Ptr.ReadBytes(10 * 16 * 0xE8, 0xA0);
        }
    }

    public struct AtkModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("E8 * * * * 48 8B F8 41 B0 01")]
        public static IntPtr GetAddonByNameFuncPtr { get; set; }

        public IntPtr GetAddonByName(string name, int index)
            => GetAddonByNameFuncPtr.Call<IntPtr>(Ptr.ReadPtr(0x2C0), RemoteInput.CString(name, Encoding.ASCII), index);
    }

    public struct AddonActionBar : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public MemoryField<byte> CurrentPage => Ptr.Field<byte>(0x2CB);
    }
}
