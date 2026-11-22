using System;

namespace Simulant.Game.FFCS.Common.Component.BGCollision
{
    // Common::Component::BGCollision::BGCollisionModule
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/BGCollisionModule.cs
    /// <summary>Entry point for collision related operations.</summary>
    internal struct BGCollisionModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal SceneManager SceneManager => Ptr.ReadPtr(0x10).As<SceneManager>();
    }
}
