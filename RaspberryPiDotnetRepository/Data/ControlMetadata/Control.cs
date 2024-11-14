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
            // Subsequent lines in each of these strings will be prefixed with a leading space in the package control file because that indentation is how multi-line descriptions work.
            //
            // Except for the summary first line of every description string, each line that starts with "." will be replaced to start with U+2024 ONE DOT LEADER ("․") instead of U+002E FULL STOP (".", a normal period).
            // This is because lines that start with periods have special meaning in Debian packages. Specifically, a period on a line of its own is interpreted as a blank line to differentiate it from a new package record, but a line that starts with a period and has more text after it (like .NET) is illegal and should not be used. Aptitude renders such lines as blank lines.
            //
            // https://www.debian.org/doc/debian-policy/ch-controlfields.html#description
            output.Append(value.Trim().ReplaceLineEndings("\n").Replace("\n.", "\n\u2024").Replace("\n\n", "\n.\n").Replace("\n", "\n "));
            output.Append('\n');
        }

        return output.ToString();
    }

}