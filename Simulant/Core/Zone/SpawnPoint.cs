using System;
using System.Numerics;

namespace Simulant.Core.Zone
{
    internal enum SpawnPointSource
    {
        EntranceCircle,
        PublicContent,
        PublicContentExit,
        Aetheryte,
        Warp,
        Level,
        EntranceRange,
        PlayerRange,
        GreenScreen
    }

    internal sealed class SpawnPoint
    {
        internal SpawnPoint(Vector3 position, SpawnPointSource source, EntranceCircle circle = null)
        {
            Position = position;
            Source = source;
            Circle = circle;
        }

        internal Vector3 Position { get; }
        internal SpawnPointSource Source { get; }
        internal EntranceCircle Circle { get; }

        public override string ToString()
        {
            string source;
            switch (Source)
            {
                case SpawnPointSource.EntranceCircle:
                    source = $"入场圈 {Circle.LayoutId:X}";
                    break;
                case SpawnPointSource.PublicContent:
                    source = "副本入口";
                    break;
                case SpawnPointSource.PublicContentExit:
                    source = "副本出口";
                    break;
                case SpawnPointSource.Aetheryte:
                    source = "传送水晶";
                    break;
                case SpawnPointSource.Warp:
                    source = "传送点";
                    break;
                case SpawnPointSource.Level:
                    source = "指定落点";
                    break;
                case SpawnPointSource.EntranceRange:
                    source = "入口范围";
                    break;
                case SpawnPointSource.GreenScreen:
                    source = "绿幕场地";
                    break;
                default:
                    source = "出生范围";
                    break;
            }

            return FormattableString.Invariant($"{source} ({Position.X:0}, {Position.Y:0}, {Position.Z:0})");
        }
    }

    internal sealed class EntranceCircle
    {
        internal EntranceCircle(uint layoutId, Vector3 position)
        {
            LayoutId = layoutId;
            Position = position;
        }

        internal uint LayoutId { get; }
        internal Vector3 Position { get; }
    }
}
