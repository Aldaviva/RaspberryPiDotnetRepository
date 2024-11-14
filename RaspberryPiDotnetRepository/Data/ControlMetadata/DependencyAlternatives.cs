namespace RaspberryPiDotnetRepository.Data.ControlMetadata;

public record DependencyAlternatives(IEnumerable<DependencyPackage> alternatives): Dependency {

    public DependencyAlternatives(params DependencyPackage[] alternatives): this(alternatives.AsEnumerable()) { }

    public string serialize() => string.Join(" | ", alternatives.serializeAll());

}