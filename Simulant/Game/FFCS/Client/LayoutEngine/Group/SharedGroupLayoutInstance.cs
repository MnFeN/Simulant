using Simulant.Game.FFCS.Client.LayoutEngine.Node;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine.Group
{
    // Client::LayoutEngine::Group::SharedGroupLayoutInstance
    //   Client::LayoutEngine::ILayoutInstance
    //   Client::System::Resource::ResourceEventListener
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Group/SharedGroupLayoutInstance.cs
    internal struct SharedGroupLayoutInstance : IMemoryObject
    {
        // 从 FFCS 的 FixedSizeArray16<byte> _timelineIndices 取长度，供项目的 MemoryArray 使用。
        internal const int TimelineSlotCount = 16;

        // 项目的远程内存视图。ILayoutInstance 是上游声明的首个基类。
        public IntPtr Ptr { get; set; }

        internal ILayoutInstance LayoutInstance => Ptr.As<ILayoutInstance>();
        internal ChildNodeContainer Instances => Ptr.As<ChildNodeContainer>(0x080);
        internal TimeLineContainer TimeLineContainer => Ptr.As<TimeLineContainer>(0x0D0);
        internal MemoryField<uint> PrefabFlags2 => Ptr.Field<uint>(0x12C);
        internal MemoryArray<byte> TimelineIndices => Ptr.ArrayField<byte>(0x154, TimelineSlotCount);

        // 本机客户端 2026.11.03.0000.0000 反汇编补充。
        // RVA 0x7060B0 / 0x706920 成功查到时间轴后写入 +0x138 的低四位，并设置 PrefabFlags2 的 bit 0。
        // 构造函数（RVA 0x6FC730）清除此标记。FFCS 的 PlayingTimelineIndex（+0x16D）用于加载前排队。
        internal const uint LastStartedTimelineFlag = 1;
        internal MemoryField<uint> LastStartedTimelineState => Ptr.Field<uint>(0x138);
    }
}
