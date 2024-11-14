using System.Text;

namespace RaspberryPiDotnetRepository.Data.ControlMetadata;

public record DependencyPackage: Dependency {

    public DependencyPackage(string name): this(name, null, null) { }

    public DependencyPackage(string name, Inequality versionInequality, string version): this(name, new Inequality?(versionInequality), version) { }

    private DependencyPackage(string name, Inequality? versionInequality, string? version) {
        if (name.Contains(' ')) {
            throw new ArgumentOutOfRangeException(nameof(name), name, "Package name cannot contain spaces. To specify a version requirement, use a contructor overload");
        }

        this.name              = name;
        this.versionInequality = versionInequality;
        this.version           = version;
    }

    public string name { get; }
    public Inequality? versionInequality { get; }
    public string? version { get; }

    public string serialize() {
        StringBuilder stringBuilder = new(name);
        if (version != null && versionInequality != null) {
            stringBuilder.Append(" (")
                .Append(versionInequality.Value.serialize())
                .Append(' ')
                .Append(version)
                .Append(')');
        }

        return stringBuilder.ToString();
    }

}