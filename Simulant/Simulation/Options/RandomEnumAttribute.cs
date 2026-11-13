using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Simulant.Simulation.Options
{
    /// <summary> 带有此属性的枚举值会在开始模拟时自动随机化为指定的枚举值选项之一。 </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class RandomEnumAttribute : Attribute
    {
        private readonly object[] _choices;
        public object[] Choices => (object[])_choices.Clone();

        public Type EnumType { get; private set; }

        public RandomEnumAttribute(params object[] enumChoices)
        {
            if (enumChoices == null || enumChoices.Length == 0)
                throw new ArgumentException("RandomEnum 选项不能为空。", nameof(enumChoices));

            Type enumType = enumChoices[0]?.GetType();
            if (enumType?.IsEnum != true)
                throw new ArgumentException($"RandomEnum 选项类型 {enumType?.Name ?? "(null)"} 必须是 enum 枚举值。", nameof(enumChoices));

            for (int i = 1; i < enumChoices.Length; i++)
            {
                var choice = enumChoices[i] ?? throw new ArgumentException("RandomEnum 选项不能包含 null。", nameof(enumChoices));
                var type = choice.GetType();
                if (type != enumType)
                {
                    throw new ArgumentException($"RandomEnum 选项包含不同的枚举类型：{type.FullName}, {enumType.FullName}。", nameof(enumChoices));
                }
            }

            EnumType = enumType;
            _choices = enumChoices;
        }

    }

    public static class RandomEnumAttributeExtension
    {
        private static readonly Random _rnd = new Random();
        private static readonly object _lock = new object();

        /// <summary>
        /// 按 <see cref="RandomEnumAttribute"/> 指定的选项递归随机选择，直到得到不带该属性的枚举值。<br />
        /// 对于普通枚举值则直接返回原始值。
        /// </summary>
        public static T TryResolveRandom<T>(this T @enum) // 这里为了非枚举泛型类的 SimOption<T> 调用而不加 where
        {
            if (!typeof(T).IsEnum)
                throw new InvalidOperationException($"ResolveRandom 只能用于 enum 枚举类型，但传入了 {typeof(T).FullName}。");

            return ResolveRandom(@enum, new HashSet<T>());
        }

        private static T ResolveRandom<T>(T @enum, HashSet<T> visited)
        {
            var choices = @enum.GetRandomChoices();

            if (choices.Count == 0) // 没有 RandomEnumAttribute，直接返回原值
                return @enum;

            if (!visited.Add(@enum))
                throw new InvalidOperationException($"RandomEnum 存在循环引用：{typeof(T).FullName}.{@enum}。");

            T next;
            lock (_lock)
            {
                next = choices[_rnd.Next(choices.Count)];
            }
            return ResolveRandom(next, visited);
        }

        private static List<T> GetRandomChoices<T>(this T @enum)
        {
            var enumType = typeof(T);
            var field = enumType.GetField(@enum.ToString());

            // 没有找到对应字段，说明传入了未定义的枚举值
            if (field == null)
                return new List<T>();

            var attr = field.GetCustomAttribute<RandomEnumAttribute>();

            // 没有 RandomEnumAttribute，直接返回原值
            if (attr == null)
                return new List<T>();

            if (attr.EnumType != enumType)
            {
                throw new InvalidOperationException(
                    $"RandomEnum 类型不匹配：字段类型为 {enumType.FullName}，但候选项类型为 {attr.EnumType.FullName}。");
            }

            return new List<T>(attr.Choices.Cast<T>());
        }
    }
}
