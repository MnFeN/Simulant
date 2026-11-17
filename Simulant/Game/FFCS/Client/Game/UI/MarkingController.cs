using Simulant.ACT;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    internal struct MarkingController : IMemoryObject
    {
        public const uint NoTarget = 0xE0000000;

        [SigPattern("48 8D 0D * * * * 4C 8B 85")]
        public static IntPtr InstancePtr { get; set; }

        [SigPattern("E8 * * * * 4C 8B C5 8B D7 48 8B CB E8")]
        public static IntPtr SetLocalMarkerFuncPtr { get; set; }

        public static MarkingController Instance => InstancePtr.As<MarkingController>();
        public IntPtr Ptr { get; set; }

        public void SetLocalMarker(MarkerType marker, uint entityId)
        {
            GameFunctions.CheckStatus(Ptr, SetLocalMarkerFuncPtr);
            SetLocalMarkerFuncPtr.Call(Ptr, (uint)marker, entityId, 0u);
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x20)]
        private struct FieldMarker
        {
            [FieldOffset(0x00)] public Vector3 Position;
            [FieldOffset(0x10)] public int X;
            [FieldOffset(0x14)] public int Y;
            [FieldOffset(0x18)] public int Z;
            [FieldOffset(0x1C)] public byte Active;
        }

        public void SetFieldMarkers(Func<int, Vector3?> getPosition)
        {
            GameFunctions.CheckStatus(Ptr);
            var memory = NamazuInterop.Plugin.Memory;
            var start = Ptr + 0x1E0;
            var size = 8 * Marshal.SizeOf<FieldMarker>();
            var bytes = memory.ReadBytes(start, size);
            if (bytes == null || bytes.Length != size)
                throw new InvalidOperationException("未能完整读取场地标点。");

            var markers = MemoryMarshal.Cast<byte, FieldMarker>(bytes.AsSpan());
            for (var slot = 0; slot < markers.Length; slot++)
            {
                var position = getPosition?.Invoke(slot);
                var p = position.GetValueOrDefault();
                ref var marker = ref markers[slot];
                marker.Position = new Vector3(p.X, p.Z, p.Y);
                marker.X = (int)(p.X * 1000);
                marker.Y = (int)(p.Z * 1000);
                marker.Z = (int)(p.Y * 1000);
                marker.Active = position.HasValue ? (byte)1 : (byte)0;
            }

            if (memory.WriteBytes(start, bytes) != bytes.Length)
                throw new InvalidOperationException("未能完整写入场地标点。");
        }
    }
}
