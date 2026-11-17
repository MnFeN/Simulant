using Simulant.ACT;
using System;
using System.Numerics;
using System.Text;

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

        public IntPtr Ptr { get; set; }

        public static VfxObject CreateStatic(string path)
        {
            GameFunctions.CheckStatus(CreateStaticFuncPtr, RunFuncPtr, FadeOutFuncPtr);
            const string pool = "Client.System.Scheduler.Instance.VfxObject";
            return NamazuInterop.Plugin.Memory.WithAllocatedString2(path, pool, Encoding.UTF8,
                (pathPtr, poolPtr) => CreateStaticFuncPtr.Call<IntPtr>(pathPtr, poolPtr)).As<VfxObject>();
        }

        public static VfxObject CreateActor(string path, IntPtr source, IntPtr target)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase) || path.IndexOf('\0') >= 0)
                throw new ArgumentException("实体特效需要有效的 .avfx 资源路径。", nameof(path));
            GameFunctions.CheckStatus(CreateActorFuncPtr, source, target);
            return NamazuInterop.Plugin.Memory.WithAllocatedString(path, Encoding.UTF8,
                pathPtr => CreateActorFuncPtr.Call<IntPtr>(pathPtr, source, target, -1f, (byte)0, 0, (byte)0))
                .As<VfxObject>();
        }

        public void Configure(Vector3 position, float heading, Vector3 scale, Vector4? color)
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(0, 0, heading);
            Ptr.Write(new Vector3(position.X, position.Z, position.Y), 0x50);
            Ptr.Write(new Vector4(rotation.X, rotation.Z, rotation.Y, rotation.W), 0x60);
            Ptr.Write(new Vector3(scale.X, scale.Z, scale.Y), 0x70);
            if (color.HasValue)
                Ptr.Write(color.Value, 0x260);
        }

        public void Run() => RunFuncPtr.Call<IntPtr>(Ptr, 0f, -1);
        public void FadeOut() => FadeOutFuncPtr.Call<IntPtr>(Ptr, 10f);
    }
}
