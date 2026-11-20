using Simulant.Game.FFCS.Client.System.File;
using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;
using System.IO;

namespace Simulant.Game
{
    internal static class UnpackedResourceInterop
    {
        // ModelResourceHandle.Read 对不走包内读取且没有对应外部模型记录的文件返回 -15。
        private const byte ModelReadNeedsLocalFile = unchecked((byte)-15);

        internal static ResourceHandle Load(GameConnection connection, string virtualPath, string filePath)
        {
            GameFunctions.CheckStatus(ResourceHandle.GetAsyncFuncPtr, ResourceHandle.DecRefFuncPtr);
            GameFunctions.CheckStatus(FileInterface.CtorFuncPtr, FileInterface.OpenFileFuncPtr, FileInterface.CloseFuncPtr);
            GameFunctions.CheckStatus(ResourceHandle.ReadUnpackedFuncPtr, ResourceHandle.FinishLoadFuncPtr, ResourceHandle.CompleteSyncLoadFuncPtr);
            var isModel = virtualPath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase);
            if (isModel)
            {
                GameFunctions.CheckStatus(ModelResourceHandle.ReadLocalFileFuncPtr);
            }

            var memory = connection.Memory;
            var expectedSize = checked((uint)new FileInfo(filePath).Length);
            var parameters = new ResourceHandle.GetResourceParameters { Priority = 5, ListenerIndex = 0xFF, SkipSqPackRead = 1 };
            var handle = ResourceHandle.GetAsync(virtualPath, RemoteInput.Struct(parameters));
            if (handle.IsNull())
            {
                throw new InvalidOperationException("未能注册场景资源：" + virtualPath);
            }

            try
            {
                if (IsReady(handle, expectedSize))
                {
                    handle.ClearSkipSqPackRead();
                    return handle;
                }

                if (!handle.SkipsSqPackRead || handle.ReadState.Get() != 1 || handle.LoadState.Get() != 1)
                {
                    throw new InvalidOperationException("场景资源未处于可读入的等待状态：" + virtualPath);
                }

                const int descriptorOffset = (FileInterface.Size + 15) & ~15;
                var storage = memory.AllocateMemory(descriptorOffset + FileDescriptor.Size);
                if (storage == IntPtr.Zero)
                {
                    throw new InvalidOperationException("未能分配原生文件对象。");
                }

                try
                {
                    var file = storage.As<FileInterface>().Ctor();
                    if (file.Ptr != storage)
                    {
                        throw new InvalidOperationException("原生文件对象初始化失败。");
                    }

                    var descriptor = storage + descriptorOffset;
                    memory.Write(descriptor, FileDescriptor.ForUnpackedFile(file.Ptr, handle.Ptr, filePath));
                    byte readResult;
                    try
                    {
                        var result = file.OpenFile(filePath, OpenFileMode.Read);
                        if (result != 1 || !file.IsFileOpen)
                        {
                            throw new IOException("客户端无法打开场景测试文件：" + filePath);
                        }

                        readResult = handle.ReadUnpacked(descriptor, false);
                        if (isModel && readResult == ModelReadNeedsLocalFile)
                        {
                            readResult = handle.Ptr.As<ModelResourceHandle>().ReadLocalFile(descriptor);
                        }
                    }
                    finally
                    {
                        if (file.IsFileOpen)
                        {
                            file.Close();
                        }
                    }

                    // 国服散文件读取在关闭文件后把读取结果传给 FinishLoad。
                    var finished = handle.FinishLoad(descriptor, readResult, 0);
                    handle.CompleteSyncLoad(false);

                    if (!finished || !IsReady(handle, expectedSize))
                    {
                        var result = unchecked((sbyte)handle.ReadResult.Get());
                        throw new InvalidOperationException($"客户端未完成场景资源加载：{virtualPath}（读取结果 {unchecked((sbyte)readResult)}、读入 {handle.ReadState.Get()}、加载 {handle.LoadState.Get()}、结果 {result}）");
                    }

                    // 同步请求碰到仍带此标志的资源会强制从 SqPack 再读一次。
                    handle.ClearSkipSqPackRead();
                }
                finally
                {
                    memory.FreeMemory(storage);
                }

                return handle;
            }
            catch
            {
                handle.DecRef();
                throw;
            }
        }

        private static bool IsReady(ResourceHandle handle, uint expectedSize)
            => handle.ReadState.Get() == 2 && handle.ReadResult.Get() == 1 && handle.FileSize.Get() == expectedSize;
    }
}
