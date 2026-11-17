using Simulant.Game.FFCS.Client.Game.UI;
using System;
using System.Numerics;

namespace Simulant.Core.Environment
{
    internal static class GameMarkers
    {
        internal static void Mark(MarkerType marker, uint entityId)
            => MarkingController.Instance.SetLocalMarker(marker, entityId);

        internal static void ClearMarker(MarkerType marker) => Mark(marker, MarkingController.NoTarget);

        public static void PlaceWaymarks(WaymarkLayout layout)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            WriteWaymarks(layout);
        }

        public static void ClearWaymarks() => WriteWaymarks(null);

        private static void WriteWaymarks(WaymarkLayout layout)
        {
            MarkingController.Instance.UpdateFieldMarkers(markers =>
            {
                for (var slot = 0; slot < markers.Length; slot++)
                {
                    var position = layout?.Position(slot);
                    var p = position.GetValueOrDefault();
                    ref var marker = ref markers[slot];
                    marker.Position = new Vector3(p.X, p.Z, p.Y);
                    marker.X = (int)(p.X * 1000);
                    marker.Y = (int)(p.Z * 1000);
                    marker.Z = (int)(p.Y * 1000);
                    marker.Active = position.HasValue ? (byte)1 : (byte)0;
                }
            });
        }
    }
}
