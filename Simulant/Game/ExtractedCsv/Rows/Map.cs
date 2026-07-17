using System;
using System.Collections.Generic;

namespace Simulant.Game.ExtractedCsv.Rows
{
    public class Map : TypedCsvRow
    {
        // "x6d3/02"
        public override string Name => Get("Id");
        // 400
        public ushort SizeFactor => Get<ushort>("SizeFactor");
        // -100
        public short OffsetX => Get<short>("Offset{X}");
        // -100
        public short OffsetY => Get<short>("Offset{Y}");
        // region: Unlost World
        public ushort RegionPlaceNameId => Get<ushort>("PlaceName{Region}"); // PlaceName
        // place: Alexandria
        public ushort PlaceNameId => Get<ushort>("PlaceName"); // PlaceName
        // sub: Containment Area III
        public ushort SubPlaceNameId => Get<ushort>("PlaceName{Sub}"); // PlaceName
    }

}