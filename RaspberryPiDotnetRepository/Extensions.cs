using Azure;
using RaspberryPiDotnetRepository.Data;

// ReSharper disable InconsistentNaming - extension methods
// ReSharper disable ReturnTypeCanBeEnumerable.Global

namespace RaspberryPiDotnetRepository;

public static class Extensions {

    public static T? AsNullable<T>(this NullableResponse<T> response) where T: class => response.HasValue ? response.Value : null;

    // Make sure you always provide the optional constructor parameters even if they're 0, because otherwise ToString(int) throws an exception if you ask for 3 and only provided 2 because Version is too stupid to fill in trailing zeros
    public static Version AsMajor(this Version version) => version is { Minor: 0, Build: 0, Revision: 0 } ? version : new Version(version.Major, 0, 0);
    public static Version AsMinor(this Version version) => version is { Build: 0, Revision: 0 } ? version : new Version(version.Major, version.Minor, 0);

    public static Options options(this IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Options>>().Value;

    /// <summary>
    /// This is slow O(n²) and assumes there are no duplicates in either collection.
    /// </summary>
    public static bool EqualsUnordered<T>(this ICollection<T> source, ICollection<T> other, IEqualityComparer<T>? equalityComparer = null) {
        equalityComparer ??= EqualityComparer<T>.Default;
        return source.Count == other.Count && source.All(sourceItem => other.Contains(sourceItem, equalityComparer));
    }

}