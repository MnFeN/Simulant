using System;
using Simulant.Game.FFCS.Client.Game.InstanceContent;

namespace Simulant.Game.FFCS.Client.Game.Event
{
    public struct DirectorModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public ContentDirector ActiveContentDirector => Ptr.ReadPtr(0x98).As<ContentDirector>();
    }
}
