using System;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    public struct VfxContainer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<ContainerInterface>
        private ContainerInterface ContainerInterface => Ptr.As<ContainerInterface>();
        #region ContainerInterface
        public Character OwnerObject => ContainerInterface.OwnerObject;

        #endregion

        [SigPattern("E8 * * * * 48 8B 94 24 ?? ?? ?? ?? 66 41 89 2E")]
        public static IntPtr SetVfxDataFuncPtr { get; set; }

        /// <summary>替换附着特效槽，传空指针会原生停止并解除旧实例；状态循环特效使用槽 2、3。</summary>
        public void SetVfxData(int index, IntPtr vfxData)
        {
            if ((uint)index >= 14)
                throw new ArgumentOutOfRangeException(nameof(index));

            SetVfxDataFuncPtr.Call(Ptr, index, vfxData);
        }
    }
}
