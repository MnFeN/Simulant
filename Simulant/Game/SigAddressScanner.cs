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

        /// <summary>解析地址属性的备选签名，保留未能定位的属性供初始化日志报告。</summary>
        internal static Dictionary<PropertyInfo, int?> ResolveRvas(GameImageScanner scanner)
            => GetSigPatternProperties().ToDictionary(property => property,
                property => scanner.FindRva(GetSignatures(property)));

        /// <summary>按当前进程的模块基址绑定已预扫出的地址。</summary>
        internal static void Bind(GameImageScan image, IntPtr imageBase)
        {
            foreach (var address in image.Addresses)
                SetValue(address.Key, address.Value.HasValue ? imageBase + address.Value.Value : IntPtr.Zero);
        }

        internal static void Clear()
        {
            foreach (var property in GetSigPatternProperties())
                SetValue(property, IntPtr.Zero);
        }

        private static unsafe void SetValue(PropertyInfo ptrProp, IntPtr ptr)
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

        private static IEnumerable<PropertyInfo> GetSigPatternProperties() => SigPatternProperties.Value;

        /// <summary>枚举地址属性声明的全部备选签名，供预扫客户端时一次匹配。</summary>
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
