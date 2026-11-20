using System;
using System.Numerics;

namespace Simulant.Core.Zone
{
    internal struct GreenScreenAppearance
    {
        internal Vector3 FloorColor;
        internal float Emission;
        internal float Diffuse;
        internal float Specular;
        internal float Glare;
        internal Vector4 PointLight;
        internal Vector4 WorldLight;

        internal static GreenScreenAppearance Default => new GreenScreenAppearance
        {
            FloorColor = Vector3.UnitY,
            Emission = 0.25f,
            PointLight = new Vector4(Vector3.One, 2f),
            WorldLight = new Vector4(Vector3.One, 0.25f)
        };

        internal void WriteMaterialConstants(Action<uint, float[]> write)
        {
            var emission = FloorColor * Emission;
            var diffuse = FloorColor * Diffuse;
            var emissiveRgb = new[] { emission.X, emission.Y, emission.Z };
            var diffuseRgb = new[] { diffuse.X, diffuse.Y, diffuse.Z };
            var specularRgb = new[] { Specular, Specular, Specular };
            write(0x38A64362, emissiveRgb); // g_EmissiveColor
            write(0xAA676D0F, emissiveRgb); // g_MultiEmissiveColor
            write(0x2C2A34DD, diffuseRgb); // g_DiffuseColor
            write(0x3F8AC211, diffuseRgb); // g_MultiDiffuseColor
            write(0x141722D5, specularRgb); // g_SpecularColor
            write(0x86D60CB8, specularRgb); // g_MultiSpecularColor
            write(0xBFE9D12D, new[] { Glare }); // g_EmissiveGlarePower
        }
    }
}
