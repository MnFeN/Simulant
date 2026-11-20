using Simulant.Utils.Maths;
using System.Numerics;

namespace Simulant.Core.Environment
{
    public enum StudioDrawLayer
    {
        BaseUpper = 1,
        Base = 2,
        BaseLower = 3
    }

    public enum StudioFloorType
    {
        Model,
        Decal
    }

    public struct StudioSettings
    {
        internal const float DefaultSize = 50f;
        internal const float MinSize = 1f;
        internal const float MaxSize = 10000f;

        public Vector3 Position;
        public bool RelativeXY;
        public bool RelativeZ;
        public float Angle;
        public bool RelativeAngle;
        public Vector3 Size;
        public Vector4 Color;
        public StudioDrawLayer DrawLayer;
        public StudioFloorType FloorType;
        public bool DepthTest;
        public bool DepthWrite;
        public int EnvironmentLight;
        public int DirectionalLight;

        internal static StudioSettings Default => new StudioSettings
        {
            RelativeXY = true,
            RelativeZ = true,
            Size = new Vector3(DefaultSize),
            Color = Vector4.One,
            DrawLayer = StudioDrawLayer.BaseLower,
            DepthTest = true
        };

        internal bool FollowsPlayer => RelativeXY || RelativeZ || RelativeAngle;

        internal StudioSettings WithClampedSize()
        {
            var settings = this;
            settings.Size = Vector3.Clamp(Size, new Vector3(MinSize), new Vector3(MaxSize));
            return settings;
        }

        internal Vector3 ResolvePosition(Vector3 player)
            => Position + new Vector3(RelativeXY ? player.X : 0f, RelativeXY ? player.Y : 0f, RelativeZ ? player.Z : 0f);

        internal Quaternion ResolveRotation(float playerHeading)
            => Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Angles.Deg(Angle) + (RelativeAngle ? playerHeading : 0f));

        internal bool SameMaterial(StudioSettings other) => FloorType == other.FloorType &&
            DrawLayer == other.DrawLayer && DepthTest == other.DepthTest && DepthWrite == other.DepthWrite &&
            EnvironmentLight == other.EnvironmentLight && DirectionalLight == other.DirectionalLight;
    }
}
