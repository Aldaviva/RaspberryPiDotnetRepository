namespace RaspberryPiDotnetRepository.Data;

public interface PackageRequest {

    public DotnetPackageType packageType { get; }
    public DebianRelease debian { get; }

}

public record DotnetPackageRequest(
    DotnetRelease     dotnetRelease,
    DotnetPackageType packageType,
    CpuArchitecture   architecture,
    DebianRelease     debian,
    string            sdkArchivePath
): PackageRequest;

public record MetaPackageRequest(
    DotnetPackageType packageType,
    DebianRelease     debian,
    bool              mustBeSupportedLongTerm,
    string            concreteMinorVersion
): PackageRequest;