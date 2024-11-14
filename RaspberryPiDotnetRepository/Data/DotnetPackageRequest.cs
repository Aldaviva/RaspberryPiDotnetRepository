namespace RaspberryPiDotnetRepository.Data;

public interface PackageRequest {

    public RuntimeType packageType { get; }

}

public record DotnetPackageRequest(
    DotnetRelease dotnetRelease,
    RuntimeType packageType,
    CpuArchitecture architecture,
    string sdkArchivePath
): PackageRequest {

    public override string ToString() {
        return $"{packageType.getFriendlyName()} {dotnetRelease.runtimeVersion} {architecture}";
    }

}

public record MetaPackageRequest(
    RuntimeType packageType,
    CpuArchitecture architecture,
    bool mustBeSupportedLongTerm,
    Version concreteMinorVersion
): PackageRequest {

    public override string ToString() {
        return $"{packageType.getFriendlyName()} {concreteMinorVersion} {architecture} latest {(mustBeSupportedLongTerm ? "LTS" : "")}";
    }

}