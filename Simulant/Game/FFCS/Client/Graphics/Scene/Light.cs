using System;

namespace Simulant.Game.FFCS.Client.Graphics.Scene
{
    // Client::Graphics::Scene::Light, FFCS size 0xB0.
    //   Client::Graphics::Scene::DrawObject
    //     Client::Graphics::Scene::Object
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Scene/Light.cs
    internal struct Light : IMemoryObject
    {
        // FFCS 未声明。国服 2026.11.03 的灯光 SetColor 与强度更新入口共用此函数，更新渲染范围及剔除。
        [SigPattern("40 53 48 83 EC 20 48 8B D9 48 8B 89 90 00 00 00 E8 ?? ?? ?? ?? 48 8B 03 48 8B CB 48 83 C4 20 5B 48 FF 60 30")]
        internal static IntPtr UpdateLightCullingFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }
        internal Render.Light RenderLight => Ptr.ReadPtr(0x90).As<Render.Light>();

        // FFCS 未声明。第二参数沿用原生颜色和强度更新入口传入的 0。
        internal void UpdateLightCulling() => UpdateLightCullingFuncPtr.Call(Ptr, (byte)0);
    }
}
