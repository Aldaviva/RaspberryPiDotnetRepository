using DataSizeUnits;
using RaspberryPiDotnetRepository.Debian.Repository;
using System.Text;

namespace RaspberryPiDotnetRepository.Data.ControlMetadata;

public record Control(
    string          name,
    string          version,
    PersonWithEmail maintainer,
    DataSize        installedSize,
    string          descriptionSummary,
    string          descriptionBody
): DebianSerializable {

    public Section? section { get; init; }
    public Priority? priority { get; init; }
    public Uri? homepage { get; init; }
    public CpuArchitecture? architecture { get; init; }
    public IEnumerable<Dependency> dependencies { get; init; } = [];
    public IEnumerable<Dependency> recommendations { get; init; } = [];
    public IEnumerable<Dependency> suggestions { get; init; } = [];
    public IEnumerable<Dependency> enhanced { get; init; } = [];
    public IEnumerable<Dependency> preDependencies { get; init; } = [];
    public IEnumerable<Dependency> provided { get; init; } = [];

    /// <summary>
    /// Extra fields are added in <c>Packages.gz</c> index files by <see cref="IndexerImpl.generateIndexOfPackagesInDebianReleaseAndArchitecture"/>
    /// </summary>
    public string serialize() => serialize(new SortedDictionary<string, string?> {
        { "Package", name },
        { "Version", version },
        { "Architecture", architecture?.toDebian() ?? "all" },
        { "Maintainer", maintainer.serialize() },
        { "Installed-Size", Math.Round(installedSize.ConvertToUnit(Unit.Kilobyte).Quantity).ToString("F0") },
        { "Depends", string.Join(", ", dependencies.serializeAll()) },
        { "Recommends", string.Join(", ", recommendations.serializeAll()) },
        { "Suggests", string.Join(", ", suggestions.serializeAll()) },
        { "Enhances", string.Join(", ", enhanced.serializeAll()) },
        { "Pre-Depends", string.Join(", ", preDependencies.serializeAll()) },
        { "Provides", string.Join(", ", provided.serializeAll()) },
        { "Section", section?.serialize() },
        { "Priority", priority?.serialize() },
        { "Homepage", homepage?.ToString() },
        { "Description", $"{descriptionSummary}\n{descriptionBody}" }
    });

    private static string serialize(IDictionary<string, string?> metadata) {
        StringBuilder output = new();
        foreach ((string key, string value) in metadata.Compact().Where(pair => !string.IsNullOrWhiteSpace(pair.Value))) {
            output.Append(key).Append(':').Append(' ');
            output.Append(value.Trim().ReplaceLineEndings("\n").Replace("\n.", "\n\u2024").Replace("\n\n", "\n.\n").Replace("\n", "\n "));
            output.Append('\n');
        }

        return output.ToString();
    }

}