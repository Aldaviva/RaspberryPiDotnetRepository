using System.Text;

// ReSharper disable InconsistentNaming - extension methods
// ReSharper disable ReturnTypeCanBeEnumerable.Global

namespace RaspberryPiDotnetRepository;

public static class Extensions {

    public static Stream ToStream(this string str, Encoding? encoding = null) {
        encoding ??= Encoding.UTF8;
        return new MemoryStream(encoding.GetBytes(str), false);
    }

    public static IEnumerable<T> Compact<T>(this IEnumerable<T?> source) where T: class {
        return source.Where(item => item != null)!;
    }

    public static IEnumerable<T> Compact<T>(this IEnumerable<T?> source) where T: struct {
        return source.Where(item => item != null).Cast<T>();
    }

    public static T[] Compact<T>(this T?[] source) where T: class {
        return source.Where(item => item != null).ToArray()!;
    }

    public static T[] Compact<T>(this T?[] source) where T: struct {
        return source.Where(item => item != null).Cast<T>().ToArray();
    }

    public static void Add<T>(this ICollection<T> destination, IEnumerable<T> source) {
        foreach (T item in source) {
            destination.Add(item);
        }
    }

    public static TValue getOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue newValue) {
        if (dictionary.TryGetValue(key, out TValue? oldValue)) {
            return oldValue;
        } else {
            dictionary.Add(key, newValue);
            return newValue;
        }
    }

}