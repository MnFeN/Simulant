using Simulant.Utils;
using System;
using System.Globalization;
using System.Numerics;

namespace Simulant.Tests
{
    internal static class NumericCultureTests
    {
        public static void DataNumbersAndCoordinateArgumentsIgnoreRegionalSettings()
        {
            InCommaCultures(() =>
            {
                Assert((-2.5f).ToDataString() == "-2.5" && 3.7d.ToDataString() == "3.7"
                    && 0.125m.ToDataString() == "0.125", "数据小数必须使用点号。");
                Assert(ulong.MaxValue.ToDataString().ParseULong() == ulong.MaxValue,
                    "整数转文本不能丢失精度。");
                var point = new Vector3(-2.5f, 100.125f, 0.75f);
                var coordinates = point.ToDataString();
                Assert(coordinates == "-2.5, 100.125, 0.75", "坐标分隔逗号不能与小数混淆。");
                var parsed = coordinates.ParseArgs<float, float, float>();
                Assert(new Vector3(parsed.Item1, parsed.Item2, parsed.Item3) == point,
                    "坐标文本必须能还原原始坐标。");
                Assert(Math.Abs("-2.5 * 5 * √2".ParseData<double>() + 2.5 * 5 * Math.Sqrt(2)) < 0.00000001,
                    "辅助线表达式必须使用一致的数值语法。");
                Assert(!DataStringHelper.TryParseData("3,7", typeof(double), out _)
                    && !"3,7".TryParseDouble(out _), "小数逗号不能被误当作千位分隔符接受。");
                ArgHelper.CheckArgCount("1, 3-5, >=7", 4, "区域设置验证");
            });
        }

        internal static void InCommaCultures(Action action)
        {
            var original = CultureInfo.CurrentCulture;

            try
            {
                foreach (var name in new[]
                {
                    "fr-FR", "de-DE"
                })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    action();
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
