using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Simulant.Game
{
    /// <summary>收集 <see cref="SigPatternAttribute"/> 声明的签名，并将解析出的地址写入对应的静态属性。</summary>
    internal static class SigAddressScanner
    {
        private static readonly Lazy<PropertyInfo[]> SigPatternProperties = new Lazy<PropertyInfo[]>(DiscoverSigPatternProperties);

        /// <summary>清除缓存地址，解析标有 <see cref="SigPatternAttribute"/> 的属性，并报告失败项。</summary>
        public static Dictionary<string, string> Scan()
        {
            var ptrProps = GetSigPatternProperties().ToList();
            ClearPtrs(ptrProps);

            var scanner = GameInterop.Connection?.Scanner;

            if (!GameInterop.IsReady || scanner == null)
                throw new Exception("[AddressStore] 游戏地址扫描器未就绪。");

            Dictionary<string, string> result = new Dictionary<string, string>();

            ScanGeneralPtrs(ptrProps, scanner, result);

            foreach (var ptrProp in ptrProps)
            {
                var key = GetPropKey(ptrProp);

                if (result.ContainsKey(key))
                    continue;

                result[key] = GetValue(ptrProp) == IntPtr.Zero
                    ? "无可用内存签名，且未设置特殊扫描方法"
                    : null;
            }

            return result;
        }

        public static unsafe IntPtr GetValue(PropertyInfo ptrProp)
        {
            if (ptrProp.PropertyType == typeof(IntPtr))
            {
                var value = ptrProp.GetValue(null);
                return value != null ? (IntPtr)value : IntPtr.Zero;
            }

            if (ptrProp.PropertyType.IsPointer)
            {
                var value = ptrProp.GetValue(null);
                return value != null ? (IntPtr)Pointer.Unbox(value) : IntPtr.Zero;
            }

            throw new InvalidOperationException("属性不是 IntPtr 或指针类型：" + GetPropKey(ptrProp));
        }

        public static unsafe void SetValue(PropertyInfo ptrProp, IntPtr ptr)
        {
            if (ptrProp.PropertyType == typeof(IntPtr))
            {
                ptrProp.SetValue(null, ptr);
                return;
            }

            if (ptrProp.PropertyType.IsPointer)
            {
                ptrProp.SetValue(null, Pointer.Box(ptr.ToPointer(), ptrProp.PropertyType));
                return;
            }

            throw new InvalidOperationException("属性不是 IntPtr 或指针类型：" + GetPropKey(ptrProp));
        }

        public static IEnumerable<PropertyInfo> GetSigPatternProperties() => SigPatternProperties.Value;

        /// <summary>枚举地址属性声明的全部备选签名，供建立游戏连接时批量扫描。</summary>
        internal static IEnumerable<string> GetSignatures()
            => GetSigPatternProperties().SelectMany(GetSignatures);

        private static string[] GetSignatures(PropertyInfo property)
            => property.GetCustomAttribute<SigPatternAttribute>().Signatures ?? Array.Empty<string>();

        private static PropertyInfo[] DiscoverSigPatternProperties()
        {
            return typeof(SigAddressScanner).Assembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("Simulant.Game", StringComparison.Ordinal))
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(p =>
                    p.GetCustomAttribute<SigPatternAttribute>() != null &&
                    (p.PropertyType == typeof(IntPtr) || p.PropertyType.IsPointer))
                .ToArray();
        }

        public static string GetPropKey(PropertyInfo ptrProp)
        {
            return ptrProp.DeclaringType.FullName + "." + ptrProp.Name;
        }

        private static void ScanGeneralPtrs(List<PropertyInfo> ptrProps, GameImageScanner scanner, Dictionary<string, string> result)
        {
            foreach (var ptrProp in ptrProps)
            {
                if (GetValue(ptrProp) != IntPtr.Zero)
                    continue;

                var sigs = GetSignatures(ptrProp);

                if (sigs.Length == 0)
                    continue;

                var key = GetPropKey(ptrProp);
                var ptr = scanner.Find(sigs);

                if (ptr == IntPtr.Zero)
                {
                    result[key] = "未能找到与签名对应的内存地址";
                }
                else
                {
                    SetValue(ptrProp, ptr);
                    result[key] = null;
                }
            }
        }

        private static void ClearPtrs(List<PropertyInfo> ptrProps)
        {
            foreach (var ptrProp in ptrProps)
            {
                SetValue(ptrProp, IntPtr.Zero);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class SigPatternAttribute : Attribute
    {
        public string[] Signatures { get; }

        public SigPatternAttribute(params string[] signatures)
        {
            Signatures = signatures;
        }
    }
}
