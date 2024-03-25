namespace RaspberryPiDotnetRepository.Data;

public record DebianPackage(
    string           nameWithMinorVersion,
    string           patchVersion,
    DebianRelease    debianVersion,
    CpuArchitecture? architecture,
    DotnetRuntime    dotnetRuntime,
    string           controlMetadata,
    string           absoluteFilename
);