using System;

namespace Lumina.Data.Parsing.Tex
{
    // From DotSquish
    [Flags]
    public enum SquishOptions
    {
        None = 0,
        DXT1 = (1 << 0),
        DXT3 = (1 << 1),
        DXT5 = (1 << 2),
        ColourIterativeClusterFit = (1 << 3),
        ColourClusterFit = (1 << 4),
        ColourRangeFit = (1 << 5),
        ColourMetricPerceptual = (1 << 6),
        ColourMetricUniform = (1 << 7),
        WeightColourByAlpha = (1 << 8),
    }

    public static class SquishOptionsExtensions
    {
        public static SquishOptions GetMethod(this SquishOptions self)
        {
            return (self & (SquishOptions.DXT1 | SquishOptions.DXT3 | SquishOptions.DXT5));
        }

        public static SquishOptions GetFit(this SquishOptions self)
        {
            return (self & (SquishOptions.ColourIterativeClusterFit | SquishOptions.ColourClusterFit | SquishOptions.ColourRangeFit));
        }

        public static SquishOptions GetMetric(this SquishOptions self)
        {
            return (self & (SquishOptions.ColourMetricPerceptual | SquishOptions.ColourMetricUniform));
        }

        public static SquishOptions GetExtra(this SquishOptions self)
        {
            return (self & (SquishOptions.WeightColourByAlpha));
        }
    }
}
