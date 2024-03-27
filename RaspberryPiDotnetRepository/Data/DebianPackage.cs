namespace RaspberryPiDotnetRepository.Data;

public record DebianPackage(
    string            nameWithMinorVersion,
    string            patchVersion,
    DebianRelease     debianVersion,
    CpuArchitecture?  architecture,
    DotnetPackageType packageType,
    string            controlMetadata,
    string            absoluteFilename
);