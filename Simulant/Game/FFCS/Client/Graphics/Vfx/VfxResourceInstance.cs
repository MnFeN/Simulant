using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Graphics.Vfx
{
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Vfx/VfxResourceInstance.cs
    internal struct VfxResourceInstance : IMemoryObject
    {
        // FFCS 未声明此方法。参考 Pictomancy 的 UpdateVfxColor，并核对了客户端实际写入的 RGBA。
        [SigPattern("48 8B 51 08 F3 0F 10 44 24 28 F3 0F 11 89 A0 00 00 00 F3 0F 11 91 A4 00 00 00 F3 0F 11 99 A8 00 00 00 F3 0F 11 81 AC 00 00 00",
            "E8 * * * * 8B 4B F3")]
        internal static IntPtr SetColorFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }
        public VfxResourceObject VfxResourceObject => Ptr.ReadPtr(0x08).As<VfxResourceObject>();

        internal void SetColor(Vector4 color)
        {
            GameFunctions.CheckStatus(Ptr, SetColorFuncPtr);
            SetColorFuncPtr.Call(Ptr, color.X, color.Y, color.Z, color.W);
        }
    }
}
