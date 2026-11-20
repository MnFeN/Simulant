using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;

namespace Simulant.Game
{
    /// <summary>将内存中的 AVFX 数据注册到游戏客户端的资源缓存。</summary>
    internal static class AvfxResourceInterop
    {
        // FFCS 未声明这两个入口；对照 KodakkuAssist 的 Sigs.VfxResourcesLoad、Sigs.VfxResSetupComplete 和 VfxManager.ResourceAdd。
        [SigPattern("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 ?? 48 ?? ?? ?? 48 ?? ?? ?? 49 ?? ?? 48 ?? ?? ?? 41 ?? ??")]
        private static IntPtr ParseResourceFuncPtr { get; set; }

        // 保留 ReadState、LoadState 的指令偏移，以核对下方用到的状态字段。
        [SigPattern("40 53 48 83 EC 20 48 8B D9 33 C9 8B C1 F0 0F C0 83 A9 00 00 00 3C 07 75 ?? F0 0F C0 8B A8 00 00 00 80 F9 03")]
        private static IntPtr SetupCompleteFuncPtr { get; set; }

        // FFCS 未声明 ApricotResourceHandle 的虚表签名，从资源句柄构造函数的 LEA 取址。
        [SigPattern("33 FF 48 8D 05 * * * * 48 89 BB B0 00 00 00 48 89 BB B8 00 00 00 48 89 03 8D 4F 20 48 89 BB C0 00 00 00 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3 48 8B C8 E8 ?? ?? ?? ?? 48 89 83 C0 00 00 00")]
        private static IntPtr ResourceVTable { get; set; }

        /// <summary>使用未注册的资源路径加载 AVFX，返回的引用由调用方释放。<br />
        /// · validateParsedEffect 可在解析后、初始化前检查调用方需要的结构。</summary>
        internal static ResourceHandle Load(string path, byte[] asset, Action<ParsedAvfx> validateParsedEffect = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("AVFX 资源路径不能为空。", nameof(path));
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            if (asset.Length == 0)
                throw new ArgumentException("AVFX 数据不能为空。", nameof(asset));

            GameFunctions.CheckStatus(ResourceHandle.GetAsyncFuncPtr, ResourceHandle.DecRefFuncPtr);
            GameFunctions.CheckStatus(ParseResourceFuncPtr, SetupCompleteFuncPtr, ResourceVTable);

            var parameters = new ResourceHandle.GetResourceParameters
            {
                Priority = 5,
                ListenerIndex = 0xFF,
                SkipSqPackRead = 1
            };
            var handle = ResourceHandle.GetAsync(path, RemoteInput.Struct(parameters));
            if (handle.IsNull())
                throw new InvalidOperationException("客户端未能注册 AVFX 资源句柄。");

            try
            {
                if (handle.Ptr.ReadPtr() != ResourceVTable || !handle.SkipsSqPackRead ||
                    handle.ReadState.Get() != 1 || handle.LoadState.Get() != 1)
                    throw new InvalidOperationException("AVFX 资源句柄未进入预期的等待状态。");

                var resource = handle.Ptr.As<ApricotResourceHandle>().VfxResourceObject;
                if (resource.IsNull() || resource.ApricotResourceHandle.Ptr != handle.Ptr)
                    throw new InvalidOperationException("AVFX 资源对象未正确初始化。");

                // 原生文件加载流程也是解析返回后即可释放 AVFX 输入缓冲。
                if (ParseResourceFuncPtr.Call<byte>(resource.Ptr, RemoteInput.FromBytes(asset), (uint)asset.Length, handle.Ptr) == 0)
                    throw new InvalidOperationException("客户端未能解析 AVFX 数据。");

                var resourceFields = resource.Ptr.As<VfxResourceObjectFields>();
                var parsed = resourceFields.ParsedEffect;
                if (parsed.IsNull())
                    throw new InvalidOperationException("客户端未能生成解析后的 AVFX 资源。");
                validateParsedEffect?.Invoke(parsed);

                handle.LoadState.Set(7);
                SetupCompleteFuncPtr.Call(handle.Ptr);
                if (!resourceFields.IsPrepared)
                    throw new InvalidOperationException("客户端未能完成 AVFX 渲染资源的初始化。");

                handle.ReadResult.Set(1);
                handle.ReadState.Set(2);
                return handle;
            }
            catch
            {
                handle.DecRef();
                throw;
            }
        }

        // 以下为 FFCS 未声明的字段和内部对象，按成员归属保留在本地。
        private struct VfxResourceObjectFields : IMemoryObject
        {
            public IntPtr Ptr { get; set; }
            internal bool IsPrepared => Ptr.Read<int>(0x0C) == 1;
            internal ParsedAvfx ParsedEffect => Ptr.ReadPtr(0x10).As<ParsedAvfx>();
        }

        internal struct ParsedAvfx : IMemoryObject
        {
            public IntPtr Ptr { get; set; }
            internal AvfxScheduler FirstScheduler => Ptr.ReadPtr(0x28).As<AvfxScheduler>();
            internal byte SchedulerCount => Ptr.Read<byte>(0xE7);
        }

        internal struct AvfxScheduler : IMemoryObject
        {
            public IntPtr Ptr { get; set; }
            internal IntPtr Triggers => Ptr.ReadPtr(0x08);
            internal byte TriggerCount => Ptr.Read<byte>(0x11);
        }
    }
}
