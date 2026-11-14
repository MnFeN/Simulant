using Simulant.Utils.Maths;
using System;
using System.Globalization;

namespace Simulant.Tests
{
    internal static class MathParserTests
    {
        public static void RangeMembershipAndBooleanFunctions()
        {
            Check("between(3,3,5)", 1);
            Check("between(5,3,5)", 1);
            Check("between(6,3,5)", 0);
            Check("in(4,1,4,7)", 1);
            Check("in(5,1,4,7)", 0);
            Check("if(and(1,2),or(0,3),9)", 1);
            Check("if(0,2,3)", 3);
        }

        public static void ApproximationModuloAndInterpolation()
        {
            Check("10 ≈ 10.5", 1);
            Check("10 !≈ 10.5001", 1);
            Check("10 !≈ 10.5", 0);
            Check("mod(-1,8)", 7);
            Check("mod(7,8,-4)", -1);
            Check("rem(7,4)", -1);
            Check("lerp(2,10,0.25)", 4);
            Check("lerpangle(170°, -170°, 0.5)", -Math.PI);
            Check("lerpanglecw(0, pi05, 0.5)", -3 * Math.PI / 4);
            Check("lerpangleccw(0, pi05, 0.5)", Math.PI / 4);
            Check("lerpdir(7,1,8,0.5)", 0);
            Check("lerpdircw(7,1,8,0.5)", 4);
            Check("lerpdirccw(7,1,8,0.5)", 0);
            Check("roundir(pi05,8) = RadToDir(pi05,8)", 1);
            Check("roundvec(1,0,8) = VecToDir(1,0,8)", 1);
        }

        public static void ConstantsFastPathsAndOperatorPrecedence()
        {
            var original = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check("0xFF", 255);
                Check("0xFFFFFFFFFFFFFFFF", -1);
                Check("2.5", 2.5);
                Check("2.5 + 3", 5.5);
                Check("1 ⏎ +\n2", 3);
                Check("pi2 + pi05 + pi025 + pi0125", Math.PI * 2.875);
                Check("pitorad * piofrad", 1);
                Check("-2^2", -4);
                Check("2^3^2", 512);
                Check("8/2*2+1", 9);
                Check("1?2:0?3:4", 2);
                Check("unknown??5", 5);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        public static void InvalidExpressionsAreNotPartiallyAccepted()
        {
            foreach (var expression in new[]
            {
                "1,2",
                "1+",
                "(1+2",
                "1?2",
                "1??"
            }

            )
            {
                try
                {
                    MathParser.Parse(expression);
                }
                catch (ArithmeticException)
                {
                    continue;
                }

                throw new InvalidOperationException("不完整表达式不应返回部分结果：" + expression);
            }
        }

        public static void EorzeanTimeDoesNotRequireTriggernometry()
        {
            var delay = MathParser.Parse("nextETms(06:00)");

            if (delay < 0 || delay > 1440 * MathParser.ETmin2sec * 1000)
                throw new InvalidOperationException("等待时间必须位于一个艾欧泽亚日以内。");

            try
            {
                MathParser.NextETms(new[]
                {
                    "25:00"
                });
            }
            catch (FormatException)
            {
                return;
            }

            throw new InvalidOperationException("超出一天范围的时间应当报错。");
        }

        private static void Check(string expression, double expected)
        {
            var actual = MathParser.Parse(expression);

            if (double.IsNaN(actual) || Math.Abs(actual - expected) > 0.000001)
                throw new InvalidOperationException($"表达式 {expression}：预期 {expected}，实际 {actual}。");
        }
    }
}
