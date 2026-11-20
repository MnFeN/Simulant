using Simulant.Game.FFCS.Client.Graphics.Vfx;
using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Graphics.Scene
{
    internal struct VfxObject : IMemoryObject
    {
        [SigPattern("E8 * * * * 48 8B D8 48 85 C0 74 ?? 0F B6 57 ?? 48 8B C8 C0 EA 02 80 E2 01")]
        public static IntPtr CreateActorFuncPtr { get; set; }

        [SigPattern("E8 * * * * F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08")]
        public static IntPtr CreateStaticFuncPtr { get; set; }

        [SigPattern("E8 * * * * B0 02 EB 02", "E8 * * * * 0F ? ? ? ? ? ? 66 ? ? ? 74 ?",
            "E8 * * * * 8B 4B 7C 85 C9")]
        public static IntPtr RunFuncPtr { get; set; }

        [SigPattern("0F 28 CA E8 * * * * 80 A3", "83 89 ? ? ? ? ? F3 0F 11 89")]
        public static IntPtr FadeOutFuncPtr { get; set; }

        // FFCS 未声明 VfxObject 的虚表签名，从构造函数写入主虚表的 LEA 取址。
        [SigPattern("48 8B 5C 24 ?? 48 8D 05 * * * * 48 89 07 48 8D 05 ?? ?? ?? ?? 48 89 87 90 00 00 00")]
        internal static IntPtr VTablePtr { get; set; }

        public IntPtr Ptr { get; set; }

        public MemoryField<Vector3> Position => Ptr.Field<Vector3>(0x50);
        public MemoryField<Quaternion> Rotation => Ptr.Field<Quaternion>(0x60);
        public MemoryField<Vector3> Scale => Ptr.Field<Vector3>(0x70);
        public MemoryField<Vector4> Color => Ptr.Field<Vector4>(0x260);
        internal VfxResourceInstance VfxResourceInstance => Ptr.ReadPtr(0x2A0).As<VfxResourceInstance>();

        internal bool MatchesResource(ResourceHandle resource, uint resourceId)
        {
            if (Ptr.ReadPtr() != VTablePtr || Ptr.As<Object>().ParentObject.IsNull())
                return false;

            var instance = VfxResourceInstance;
            if (instance.IsNull())
                return false;

            var package = instance.VfxResourceObject;
            return !package.IsNull() && package.ApricotResourceHandle.Ptr == resource.Ptr && resource.Id.Get() == resourceId;
        }

        public static VfxObject CreateStatic(string path, string pool)
        {
            GameFunctions.CheckStatus(CreateStaticFuncPtr);
            return CreateStaticFuncPtr.Call<IntPtr>(RemoteInput.CString(path), RemoteInput.CString(pool)).As<VfxObject>();
        }

        public static VfxObject CreateActor(string path, IntPtr source, IntPtr target, float a4, byte a5, int a6, byte a7)
        {
            GameFunctions.CheckStatus(CreateActorFuncPtr, source, target);
            return CreateActorFuncPtr.Call<IntPtr>(RemoteInput.CString(path), source, target, a4, a5, a6, a7).As<VfxObject>();
        }

        public void Run(float a2, int a3) => RunFuncPtr.Call<IntPtr>(Ptr, a2, a3);
        public void FadeOut(float frames) => FadeOutFuncPtr.Call<IntPtr>(Ptr, frames);
    }
}
