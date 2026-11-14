using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using Simulant.Utils.Maths;

namespace Simulant.Utils
{
    public static class DataStringHelper
    {
        public static string ToDataString(this object prop)
        {
            if (prop == null)
                return "";

            switch (prop)
            {
                case string s:
                    return s;
                case bool b:
                    return b ? "1" : "0";
                case Enum e:
                    return e.ToString();
                case Vector2 v2:
                    return $"{v2.X.ToStringInvariant()}, {v2.Y.ToStringInvariant()}";
                case Vector3 v3:
                    return $"{v3.X.ToStringInvariant()}, {v3.Y.ToStringInvariant()}, {v3.Z.ToStringInvariant()}";
                case float f:
                    try
                    {
                        return f.ToStringInvariant();
                    }
                    catch (Exception)
                    {
                        return f.ToString();
                    }

                case double d:
                    try
                    {
                        return d.ToStringInvariant();
                    }
                    catch (Exception)
                    {
                        return d.ToString();
                    }

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                case IEnumerable data:
                    return string.Join(", ", data.Cast<object>().Select(x => x.ToDataString()));
                default:
                    return prop.ToString();
            }
        }

        public static T ParseData<T>(this string input)
        {
            return (T)input.ParseData(typeof(T));
        }

        public static T ParseDataOrDefault<T>(this string input, T defaultValue)
        {
            return TryParseData(input, typeof(T), out var result) ? (T)result : defaultValue;
        }

        public static object ParseData(this string input, Type targetType)
        {
            if (TryParseData(input, targetType, out var value))
                return value;

            throw new ArgumentException($"无法将字符串 '{input}' 转换为 {targetType.Name} 类型。");
        }

        public static bool TryParseData(string input, Type targetType, out object result)
        {
            result = null;

            try
            {
                // Nullable<T>
                Type underlyingType = Nullable.GetUnderlyingType(targetType);

                if (underlyingType != null)
                {
                    if (string.IsNullOrEmpty(input))
                    {
                        result = null;
                        return true;
                    }

                    targetType = underlyingType;
                }

                if (targetType == typeof(string))
                {
                    result = input;
                    return true;
                }

                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(input, out bool b))
                    {
                        result = b;
                        return true;
                    }

                    // 数字 → bool（非零为 true）
                    result = !MathParser.IsZero(MathParser.Parse(input));
                    return true;
                }

                if (targetType.IsEnum)
                {
                    result = Enum.Parse(targetType, input, true);
                    return true;
                }

                if (targetType == typeof(IntPtr))
                {
                    result = (IntPtr)(long)MathParser.Parse(input);
                    return true;
                }

                if (targetType == typeof(UIntPtr))
                {
                    result = (UIntPtr)(long)MathParser.Parse(input);
                    return true;
                }

                if (targetType == typeof(Guid))
                {
                    if (Guid.TryParse(input, out Guid g))
                    {
                        result = g;
                        return true;
                    }

                    return false;
                }

                if (targetType == typeof(HexOrDecId))
                {
                    result = new HexOrDecId(input);
                    return true;
                }

                if (targetType.IsNumericType())
                {
                    double d = MathParser.Parse(input);
                    result = Convert.ChangeType(d, targetType, CultureInfo.InvariantCulture);
                    return true;
                }

                // 尝试常规转换
                result = Convert.ChangeType(input, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsNumericType(this Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);

            if (underlyingType != null)
                type = underlyingType;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        public static T RawInvoke<T>(this Delegate _delegate, params string[] rawArgs)
            => (T)_delegate.RawInvoke(rawArgs);

        public static object RawInvoke(this Delegate _delegate, params string[] rawArgs)
        {
            ParameterInfo[] paramsInfo = _delegate.Method.GetParameters();

            rawArgs = rawArgs ?? new string[0];

            if (rawArgs.Length > paramsInfo.Length)
                throw new ArgumentException($"参数数量过多：期望最多 {paramsInfo.Length} 个参数，但提供了 {rawArgs.Length} 个");

            object[] parameters = new object[paramsInfo.Length];

            for (int i = 0; i < paramsInfo.Length; i++)
            {
                if (i < rawArgs.Length)
                {
                    Type paramType = paramsInfo[i].ParameterType;

                    try
                    {
                        parameters[i] = rawArgs[i].ParseData(paramType);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"参数 {i} ({paramsInfo[i].Name}) 无法转换为类型 {paramType}", ex);
                    }
                }
                else
                {
                    if (paramsInfo[i].HasDefaultValue)
                        parameters[i] = paramsInfo[i].DefaultValue;
                    else
                        throw new ArgumentException($"参数 {i} ({paramsInfo[i].Name}) 缺少值且无默认值");
                }
            }

            // 调用内置的 DynamicInvoke 方法执行委托
            object result = _delegate.DynamicInvoke(parameters);
            return _delegate.Method.ReturnType == typeof(void) ? "" : result;
        }

        /// <summary>
        /// 作为 uint 的形式上的 “子类”，用于配合 ToDataString、ParseArgs 自动解析用户输入的多种格式的 id。
        /// </summary>
        public struct HexOrDecId
        {
            public uint Value;
            public const uint Default = 0xE0000000;

            private static readonly Regex HexRegex = new Regex(@"^[0-9A-Fa-f]+$", RegexOptions.Compiled);
            public HexOrDecId(uint value) => Value = value;
            public HexOrDecId(string input)
            {
                input = input.Trim();

                if (string.IsNullOrWhiteSpace(input))
                    Value = Default;
                else if (HexRegex.IsMatch(input))
                    Value = Convert.ToUInt32(input, 16);
                else
                    Value = (uint)MathParser.Parse(input);
            }

            public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

            public static implicit operator uint(HexOrDecId id) => id.Value;
            public static implicit operator HexOrDecId(uint v) => new HexOrDecId(v);
        }
    }
}
