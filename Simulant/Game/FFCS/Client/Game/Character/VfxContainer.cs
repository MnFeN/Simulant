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
    }
}
