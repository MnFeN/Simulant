using System;
using System.Text;

namespace Simulant.Game.FFCS.Component.GUI
{
    // FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitManager.cs
    public struct AtkUnitManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("E8 * * * * 48 8B F8 41 B0 01")]
        public static IntPtr GetAddonByNameFuncPtr { get; set; }

        public IntPtr GetAddonByName(string name, int index = 1)
        {
            Ptr.ThrowIfZero(nameof(GetAddonByName), 0, "查找界面组件");
            return GetAddonByNameFuncPtr.Call<IntPtr>(Ptr, RemoteInput.CString(name, Encoding.ASCII), index);
        }
    }
}
