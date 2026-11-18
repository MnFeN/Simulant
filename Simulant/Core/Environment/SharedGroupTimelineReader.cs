using SimuMagic;
using Simulant.Game;
using Simulant.Game.FFCS.Client.LayoutEngine;
using Simulant.Game.FFCS.Client.LayoutEngine.Group;
using Simulant.Game.FFCS.STD;
using System;
using System.Collections.Generic;

namespace Simulant.Core.Environment
{
    /// <summary>从场景 SharedGroup 读取 MapEffect 正在播放和最近启动的时间轴。</summary>
    internal sealed class SharedGroupTimelineReader
    {
        private readonly ExternalProcessMemory _memory;
        private readonly IntPtr _activeGroups;
        private readonly IntPtr _globalGroups;
        private readonly Dictionary<IntPtr, StdMap.PointerNode> _nodes = new Dictionary<IntPtr, StdMap.PointerNode>();

        internal SharedGroupTimelineReader(ExternalProcessMemory memory, LayoutManager activeLayout, LayoutManager globalLayout)
        {
            _memory = memory;
            _activeGroups = ReadSharedGroupMap(activeLayout);
            _globalGroups = ReadSharedGroupMap(globalLayout);
        }

        private IntPtr ReadSharedGroupMap(LayoutManager layout)
        {
            if (layout.IsNull())
                return IntPtr.Zero;

            var byType = _memory.Read<StdMap>(layout.InstancesByType.Ptr);
            var groups = FindValue(byType.Head, (byte)InstanceType.SharedGroup, true);
            return groups == IntPtr.Zero ? IntPtr.Zero : _memory.Read<StdMap>(groups).Head;
        }

        internal (ushort Playing, ushort? LastStarted)? ReadPlayback(uint layoutId)
        {
            // LayoutWorld::FindInstance searches both layouts by (LayoutId << 32) | SubId.
            var key = (ulong)layoutId << 32;
            var groupAddress = FindValue(_activeGroups, key);
            if (groupAddress == IntPtr.Zero)
                groupAddress = FindValue(_globalGroups, key);
            if (groupAddress == IntPtr.Zero)
                return null;

            var group = groupAddress.As<SharedGroupLayoutInstance>();
            if (_memory.Read<StdVector>(group.Instances.Instances.Ptr).IsEmpty)
                return null;

            if (!group.TimelineIndices.TryGet(_memory, out var indices))
                return null;

            ushort? lastStarted = null;
            if ((_memory.Read<uint>(group.PrefabFlags2.Ptr) & SharedGroupLayoutInstance.LastStartedTimelineFlag) != 0)
            {
                var lastSlot = (int)(_memory.Read<uint>(group.LastStartedTimelineState.Ptr) & 0x0F);
                if (indices[lastSlot] != 0)
                    lastStarted = (ushort)(1 << lastSlot);
            }

            var instances = _memory.Read<StdVector>(group.TimeLineContainer.Instances.Ptr);
            if (instances.IsEmpty)
                return (0, lastStarted);

            var size = instances.Last.ToInt64() - instances.First.ToInt64();
            if (instances.First.ToInt64() <= 0 || size < 0 || size % IntPtr.Size != 0 || size > 4096 * IntPtr.Size)
                return null;

            var children = _memory.ReadBytes(instances.First, (int)size);
            if (children?.Length != size)
                return null;

            var parent = group.LayoutInstance;
            var parentId = _memory.Read<uint>(parent.SubId.Ptr);
            var nesting = ILayoutInstance.GetNestingLevel(_memory.Read<byte>(parent.Flags1.Ptr));
            var shift = (3 - nesting) * 8;
            var stateByChild = new Dictionary<uint, ushort>();
            for (var bit = 0; bit < indices.Length; bit++)
            {
                var id = indices[bit];
                if (id == 0)
                    continue;

                var childId = parentId | ((uint)id << shift);
                stateByChild.TryGetValue(childId, out var state);
                stateByChild[childId] = (ushort)(state | (1 << bit));
            }

            ushort active = 0;
            for (var offset = 0; offset < children.Length; offset += IntPtr.Size)
            {
                var childAddress = new IntPtr(BitConverter.ToInt64(children, offset));
                if (childAddress == IntPtr.Zero)
                    continue;

                var child = childAddress.As<TimeLineLayoutInstance>();
                var subId = _memory.Read<uint>(child.LayoutInstance.SubId.Ptr);
                if (!stateByChild.TryGetValue(subId, out var state))
                    continue;

                var reference = _memory.Read<IntPtr>(child.SchedulerObject.Ptr);
                if (reference == IntPtr.Zero)
                    continue;

                var playback = _memory.Read<TimelinePlaybackReference>(reference).Instance;
                if (playback != IntPtr.Zero && _memory.Read<TimelinePlaybackState>(playback).IsPlaying)
                    active |= state;
            }
            return (active, lastStarted);
        }

        private IntPtr FindValue(IntPtr head, ulong key, bool byteKey = false)
        {
            if (head == IntPtr.Zero)
                return IntPtr.Zero;

            var next = ReadNode(head).Parent;
            for (var depth = 0; depth < 64 && next != head && next != IntPtr.Zero; depth++)
            {
                var node = ReadNode(next);
                if (node.IsNil != 0)
                    break;

                var nodeKey = byteKey ? node.ByteKey : node.UInt64Key;
                if (nodeKey == key)
                    return node.Value;

                next = key < nodeKey ? node.Left : node.Right;
            }
            return IntPtr.Zero;
        }

        private StdMap.PointerNode ReadNode(IntPtr address)
        {
            if (!_nodes.TryGetValue(address, out var node))
            {
                node = _memory.Read<StdMap.PointerNode>(address);
                _nodes.Add(address, node);
            }
            return node;
        }
    }
}
