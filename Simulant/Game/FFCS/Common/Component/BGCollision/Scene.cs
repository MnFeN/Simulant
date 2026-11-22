using System;
using System.Collections.Generic;

namespace Simulant.Game.FFCS.Common.Component.BGCollision
{
    // Common::Component::BGCollision::SceneManager
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Scene.cs
    internal struct SceneManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // this is a linked list that always contains a single scene created on init; there are functions that create or destroy additional scenes, but they are not used
        internal SceneWrapper FirstScene => Ptr.ReadPtr(0x18).As<SceneWrapper>();
        internal MemoryField<int> NumScenes => Ptr.Field<int>(0x20);

        /// <summary>Iterate over loaded scenes. Currently there's always one scene when module is initialized, but game has some unused functions that create or remove more scenes.</summary>
        internal IEnumerable<SceneWrapper> EnumerateScenes()
        {
            var scene = FirstScene;
            var count = NumScenes.Get();
            for (var i = 0; i < count && !scene.IsNull(); i++, scene = scene.Next)
            {
                yield return scene;
            }
        }
    }

    // Common::Component::BGCollision::SceneWrapper
    //   Common::Component::BGCollision::Node
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Scene.cs
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Object.cs
    internal struct SceneWrapper : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal SceneWrapper Next => Ptr.ReadPtr(0x18).As<SceneWrapper>(); // NodeLink.Next
        internal Scene Scene => Ptr.ReadPtr(0x28).As<Scene>();

        internal bool Raycast(IntPtr hitInfo, ulong layerMask, IntPtr args)
            => Ptr.CallVFunc<byte>(14, hitInfo, layerMask, args) != 0;
    }

    // Common::Component::BGCollision::Scene
    //   Common::Component::BGCollision::Object
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Scene.cs
    internal struct Scene : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal Collider FirstCollider => Ptr.ReadPtr(0x10).As<Collider>();
        internal MemoryField<int> NumColliders => Ptr.Field<int>(0x18);
    }
}
