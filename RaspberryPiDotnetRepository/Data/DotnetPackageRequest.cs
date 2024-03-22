namespace RaspberryPiDotnetRepository.Data;

public record DotnetPackageRequest(
    DotnetRelease   dotnetRelease,
    DotnetRuntime   runtime,
    CpuArchitecture architecture,
    DebianRelease   debian,
    string          sdkArchivePath
);