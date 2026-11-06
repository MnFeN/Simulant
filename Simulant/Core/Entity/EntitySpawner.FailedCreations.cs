using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Core.Entity
{
    public sealed partial class EntitySpawner
    {
        private sealed class LocalCharacterCreation
        {
            internal long WorldGeneration;
            internal GreyMagicExternalProcessMemory Memory;
            internal IntPtr ProcessHandle;
            internal IntPtr ManagerAddress;
            internal IntPtr ObjectManagerAddress;
            internal ushort LocalIndex;
            internal ushort ObjectSlot;
            internal IntPtr NativeAddress;
            internal EntityObservation? Observation;
            internal Character Entity;
            internal Exception IdentityError;
            internal bool OwnershipConflict;
        }

        private const ushort LocalCharacterSlotStart = 200;
        private readonly List<LocalCharacterCreation> _failedLocalCreations = new List<LocalCharacterCreation>();
        private long _localCreationGeneration = 1;
        private bool _retryingFailedCreations;

        /// <summary>宿主共享的未交付失败分配；不包括已经归属任何会话的成功实体。</summary>
        internal bool HasPendingCreationCleanup
        {
            get { lock (_gate) return _failedLocalCreations.Count != 0; }
        }

        /// <summary>只重试未交付的失败创建，不回收其他会话已拥有的实体。</summary>
        internal void RetryFailedCreations()
        {
            lock (_gate)
            {
                var failures = RetryFailedCreationsCore();
                if (failures != null)
                    throw new AggregateException("部分失败创建尚未回收，记录已保留。", failures);
            }
        }

        private LocalCharacterCreation CaptureLocalCreationContext(ClientObjectManager manager)
        {
            var memory = NamazuInterop.Plugin?.Memory
                ?? throw new InvalidOperationException("本地角色创建无法访问当前游戏进程。");
            var creation = new LocalCharacterCreation
            {
                WorldGeneration = _localCreationGeneration,
                Memory = memory,
                ProcessHandle = memory.ProcessHandle,
                ManagerAddress = manager.Ptr,
                ObjectManagerAddress = GameObjectManager.InstancePtr
            };
            EnsureLocalCreationContext(creation);
            return creation;
        }

        private void EnsureLocalCreationContext(LocalCharacterCreation creation)
        {
            var memory = NamazuInterop.Plugin?.Memory;
            if (creation.WorldGeneration != _localCreationGeneration ||
                memory == null || !ReferenceEquals(memory.RawMemory, creation.Memory.RawMemory) ||
                !creation.Memory.IsProcessOpen || creation.ProcessHandle == IntPtr.Zero ||
                creation.Memory.ProcessHandle != creation.ProcessHandle || memory.ProcessHandle != creation.ProcessHandle ||
                creation.ManagerAddress == IntPtr.Zero || ClientObjectManager.InstancePtr != creation.ManagerAddress ||
                creation.ObjectManagerAddress == IntPtr.Zero || GameObjectManager.InstancePtr != creation.ObjectManagerAddress)
                throw new InvalidOperationException("本地失败分配所属的世界、内存实例或对象管理器已变化，不能调用旧地址回收。");
        }

        private EntityObservation ReadLocalCreationIdentity(LocalCharacterCreation creation)
        {
            EnsureLocalCreationContext(creation);
            var manager = creation.ManagerAddress.As<ClientObjectManager>();
            var objects = creation.ObjectManagerAddress.As<GameObjectManager>().Objects.IndexSorted;
            var localPointerAddress = manager.BattleCharas[creation.LocalIndex].Ptr;
            var objectPointerAddress = objects.Ptr + creation.ObjectSlot * IntPtr.Size;
            var pointer = creation.Memory.Read<IntPtr>(localPointerAddress);
            if (pointer != IntPtr.Zero && creation.NativeAddress != IntPtr.Zero && pointer != creation.NativeAddress)
                creation.OwnershipConflict = true;
            var indexedPointer = creation.Memory.Read<IntPtr>(objectPointerAddress);
            // 空表可能尚未完成登记，只有明确观察到其他非空指针才判定归属冲突。
            if ((pointer != IntPtr.Zero && indexedPointer != IntPtr.Zero && pointer != indexedPointer) ||
                (creation.NativeAddress != IntPtr.Zero && indexedPointer != IntPtr.Zero &&
                    indexedPointer != creation.NativeAddress))
                creation.OwnershipConflict = true;
            if (pointer == IntPtr.Zero || indexedPointer != pointer ||
                (creation.NativeAddress != IntPtr.Zero && pointer != creation.NativeAddress))
                throw new InvalidOperationException($"本地失败分配 #{creation.LocalIndex} 的两个对象表与原指针不一致。");

            var native = pointer.As<GameObject>();
            if (creation.Memory.Read<ushort>(native.ObjectIndex.Ptr) != creation.ObjectSlot)
            {
                creation.OwnershipConflict = true;
                throw new InvalidOperationException($"本地失败分配 #{creation.LocalIndex} 的对象槽位已变化。");
            }
            var observed = new EntityObservation(pointer, creation.Memory.Read<uint>(native.EntityId.Ptr),
                creation.Memory.Read<ObjectKind>(native.ObjectKind.Ptr));
            EnsureLocalCreationContext(creation);
            var currentLocalPointer = creation.Memory.Read<IntPtr>(localPointerAddress);
            if (currentLocalPointer != IntPtr.Zero && currentLocalPointer != pointer)
                creation.OwnershipConflict = true;
            var currentIndexedPointer = creation.Memory.Read<IntPtr>(objectPointerAddress);
            if (currentIndexedPointer != IntPtr.Zero && currentIndexedPointer != pointer)
                creation.OwnershipConflict = true;
            if (currentLocalPointer != pointer || currentIndexedPointer != pointer)
                throw new InvalidOperationException($"读取本地失败分配 #{creation.LocalIndex} 身份时对象表发生了变化。");
            return observed;
        }

        private void CaptureFailedLocalCreationIdentity(LocalCharacterCreation creation)
        {
            try
            {
                // 仅在本次创建失败的调用栈内补采身份；后续重试不得认领新出现的同槽对象。
                var observed = ReadLocalCreationIdentity(creation);
                creation.NativeAddress = observed.Address;
                creation.Observation = observed;
            }
            catch (Exception error) { creation.IdentityError = error; }
        }

        private List<Exception> RetryFailedCreationsCore()
        {
            // 日志可能同步重入清理；当前这一轮继续持有失败记录。
            if (_retryingFailedCreations || _failedLocalCreations.Count == 0) return null;
            _retryingFailedCreations = true;
            List<Exception> failures = null;
            try
            {
                foreach (var creation in _failedLocalCreations.ToArray())
                {
                    try
                    {
                        if (creation.OwnershipConflict)
                            throw new InvalidOperationException($"本地失败分配 #{creation.LocalIndex} 已观察到槽位归属冲突，不能重新认领；记录保留至世界重置。");
                        if (!creation.Observation.HasValue || creation.NativeAddress == IntPtr.Zero)
                            throw new InvalidOperationException($"本地失败分配 #{creation.LocalIndex} 的原始身份未确认，不能安全回收；记录保留至世界重置。",
                                creation.IdentityError);
                        if (!ReadLocalCreationIdentity(creation).Equals(creation.Observation.Value) ||
                            (creation.Entity != null && !_registry.MatchesCurrent(creation.Entity)))
                        {
                            creation.OwnershipConflict = true;
                            throw new InvalidOperationException($"本地失败分配 #{creation.LocalIndex} 的实体身份已变化，不能按槽位删除。");
                        }
                        EnsureLocalCreationContext(creation);
                        creation.ManagerAddress.As<ClientObjectManager>().DeleteObjectByIndex(creation.LocalIndex, 0);
                        _failedLocalCreations.Remove(creation);
                    }
                    catch (Exception error)
                    {
                        (failures ?? (failures = new List<Exception>())).Add(error);
                        LogCleanupError("重试回收失败创建时出错：", error);
                    }
                }
            }
            finally { _retryingFailedCreations = false; }
            return failures;
        }
    }
}
