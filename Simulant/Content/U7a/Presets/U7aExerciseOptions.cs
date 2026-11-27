using Simulant.Simulation;
using Simulant.Simulation.Options;

namespace Simulant.Content.U7a.Presets
{
    internal enum ExerciseRole
    {
        MT = 1, ST, H1, H2, D1, D2, D3, D4
    }

    internal enum ExerciseDirection
    {
        [RandomEnum(N, NW, W, SW, S, SE, E, NE)]
        Random = -1,
        N = 0, NW = 1, W = 2, SW = 3, S = 4, SE = 5, E = 6, NE = 7
    }

    internal enum ExerciseTurn
    {
        [RandomEnum(Clockwise, Counterclockwise)]
        Random,
        Clockwise,
        Counterclockwise
    }

    internal enum BaitTank
    {
        MT, ST
    }

    internal static class U7aPartyPositions
    {
        // MT、ST、H1、H2、D1、D2、D3、D4 的正八方站位。
        private static readonly ExerciseDirection[] EightWayDirections =
        {
            ExerciseDirection.N, ExerciseDirection.E, ExerciseDirection.W, ExerciseDirection.S,
            ExerciseDirection.SW, ExerciseDirection.SE, ExerciseDirection.NW, ExerciseDirection.NE
        };

        internal static ExerciseDirection EightWayDirection(int slot) => EightWayDirections[slot];
    }

    internal static class U7aExerciseOptions
    {
        internal static ComboBoxOption<ExerciseRole> Player() => new ComboBoxOption<ExerciseRole>(
            "PlayerSlot", "自身职能", ExerciseRole.MT, new Map<ExerciseRole>
            {
                [ExerciseRole.MT] = "MT", [ExerciseRole.ST] = "ST", [ExerciseRole.H1] = "H1", [ExerciseRole.H2] = "H2",
                [ExerciseRole.D1] = "D1", [ExerciseRole.D2] = "D2", [ExerciseRole.D3] = "D3", [ExerciseRole.D4] = "D4"
            });

        internal static ComboBoxOption<ExerciseDirection> Direction(string property, string label)
            => new ComboBoxOption<ExerciseDirection>(property, label, ExerciseDirection.Random, new Map<ExerciseDirection>
            {
                [ExerciseDirection.Random] = "随机", [ExerciseDirection.N] = "北 (0)", [ExerciseDirection.NW] = "西北 (1)",
                [ExerciseDirection.W] = "西 (2)", [ExerciseDirection.SW] = "西南 (3)", [ExerciseDirection.S] = "南 (4)",
                [ExerciseDirection.SE] = "东南 (5)", [ExerciseDirection.E] = "东 (6)", [ExerciseDirection.NE] = "东北 (7)"
            });

        internal static ComboBoxOption<ExerciseTurn> Turn(string label)
            => new ComboBoxOption<ExerciseTurn>("Rotation", label, ExerciseTurn.Random, new Map<ExerciseTurn>
            {
                [ExerciseTurn.Random] = "随机", [ExerciseTurn.Clockwise] = "顺时针", [ExerciseTurn.Counterclockwise] = "逆时针"
            });

        internal static CheckBoxOption FastOpeningOption() => new CheckBoxOption("FastOpening", "开场读条加速", false,
            "开场咏唱缩短为 1 秒，后续机制同步提前。");
    }
}
