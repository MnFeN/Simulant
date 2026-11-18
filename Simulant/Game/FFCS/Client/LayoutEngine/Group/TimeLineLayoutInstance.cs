using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine.Group
{
    // Client::LayoutEngine::Group::TimeLineLayoutInstance
    //   Client::LayoutEngine::ILayoutInstance
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Group/TimeLineLayoutInstance.cs
    internal struct TimeLineLayoutInstance : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal ILayoutInstance LayoutInstance => Ptr.As<ILayoutInstance>();

        // FFCS 声明为 SchedulerInstanceObject*。本机读取时按 TimelinePlaybackReference 解引用。
        internal MemoryField<IntPtr> SchedulerObject => Ptr.Field<IntPtr>(0x30);
    }
}
