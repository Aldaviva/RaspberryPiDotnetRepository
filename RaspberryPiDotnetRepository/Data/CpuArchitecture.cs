namespace RaspberryPiDotnetRepository.Data;

public enum CpuArchitecture {

    ARM32,
    ARM64

}

public static class CpuArchitectureMethods {

    public static string toRuntimeIdentifierSuffix(this CpuArchitecture cpuArchitecture) => cpuArchitecture switch {
        CpuArchitecture.ARM32 => "arm",
        CpuArchitecture.ARM64 => "arm64"
    };

    public static string toDebian(this CpuArchitecture cpuArchitecture) => cpuArchitecture switch {
        CpuArchitecture.ARM32 => "armhf",
        CpuArchitecture.ARM64 => "arm64"
    };

}