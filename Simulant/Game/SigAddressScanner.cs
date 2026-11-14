using Simulant.ACT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Simulant.Game
{
    internal static class SigAddressScanner
    {
        public static Dictionary<string, string> Scan()
        {
            var ptrProps = GetSigPatternProperties().ToList();
            ClearPtrs(ptrProps);

            var scanner = NamazuInterop.Plugin?.SigScanner;

            if (!NamazuInterop.IsReady || scanner == null)
                throw new Exception("[AddressStore] Namazu scanner 未就绪。");

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

        public static IEnumerable<PropertyInfo> GetSigPatternProperties()
        {
            return typeof(SigAddressScanner).Assembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("Simulant.Game", StringComparison.Ordinal))
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(p =>
                    p.GetCustomAttribute<SigPatternAttribute>() != null &&
                    (p.PropertyType == typeof(IntPtr) || p.PropertyType.IsPointer));
        }

        public static string GetPropKey(PropertyInfo ptrProp)
        {
            return ptrProp.DeclaringType.FullName + "." + ptrProp.Name;
        }

        private static void ScanGeneralPtrs(List<PropertyInfo> ptrProps, NamazuScanner scanner, Dictionary<string, string> result)
        {
            foreach (var ptrProp in ptrProps)
            {
                if (GetValue(ptrProp) != IntPtr.Zero)
                    continue;

                var sigs = ptrProp.GetCustomAttribute<SigPatternAttribute>().Signatures;

                if (sigs == null || sigs.Length == 0)
                    continue;

                var key = GetPropKey(ptrProp);
                var ptr = scanner.TryScanMultiple(sigs, key);

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
