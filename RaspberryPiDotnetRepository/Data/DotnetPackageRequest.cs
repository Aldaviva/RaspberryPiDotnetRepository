namespace RaspberryPiDotnetRepository.Data;

public interface PackageRequest {

    public DotnetRuntime runtime { get; }
    public DebianRelease debian { get; }

}

public record DotnetPackageRequest(
    DotnetRelease   dotnetRelease,
    DotnetRuntime   runtime,
    CpuArchitecture architecture,
    DebianRelease   debian,
    string          sdkArchivePath
): PackageRequest;

public record MetaPackageRequest(
    DotnetRuntime runtime,
    DebianRelease debian,
    bool          mustBeSupportedLongTerm,
    string        concreteMinorVersion
): PackageRequest;