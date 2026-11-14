using System;
using System.Collections.Generic;

namespace Lumina.Extensions
{
    public static class LinqExtensions
    {
        public static T? FirstOrNull<T>(this IEnumerable<T> source)
            where T : struct
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var e = source.GetEnumerator();
            if (e.MoveNext())
            {
                return e.Current;
            }

            return null;
        }

        public static T? FirstOrNull<T>(this IEnumerable<T> source, Predicate<T> predicate)
            where T : struct
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            foreach (var element in source)
            {
                if (predicate(element))
                {
                    return element;
                }
            }

            return null;
        }

        // https://github.com/dotnet/runtime/blob/96ae7cfefb0ae0ad621edc56bf690da174d9c7ab/src/libraries/System.Linq/src/System/Linq/First.cs#L65
        public static bool TryGetFirst<T>(this IEnumerable<T> source, out T result)
            where T : struct
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            using (var e = source.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    result = e.Current;
                    return true;
                }
            }

            result = default;
            return false;
        }

        // https://github.com/dotnet/runtime/blob/96ae7cfefb0ae0ad621edc56bf690da174d9c7ab/src/libraries/System.Linq/src/System/Linq/First.cs#L105
        public static bool TryGetFirst<T>(this IEnumerable<T> source, Predicate<T> predicate, out T result)
            where T : struct
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            foreach (var element in source)
            {
                if (predicate(element))
                {
                    result = element;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
