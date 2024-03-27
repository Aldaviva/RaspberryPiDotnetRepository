namespace RaspberryPiDotnetRepository.Data;

public enum DotnetPackageType {

    CLI,
    RUNTIME,
    ASPNETCORE_RUNTIME,
    SDK

}

public static class RuntimeTypeMethods {

    public static string getPackageName(this DotnetPackageType packageType) => packageType switch {
        DotnetPackageType.CLI                => "dotnet-cli",
        DotnetPackageType.RUNTIME            => "dotnet-runtime",
        DotnetPackageType.ASPNETCORE_RUNTIME => "aspnetcore-runtime",
        DotnetPackageType.SDK                => "dotnet-sdk"
    };

    public static string getFriendlyName(this DotnetPackageType packageType) => packageType switch {
        DotnetPackageType.CLI                => "/usr/bin/dotnet",
        DotnetPackageType.RUNTIME            => ".NET Runtime",
        DotnetPackageType.ASPNETCORE_RUNTIME => "ASP.NET Core Runtime",
        DotnetPackageType.SDK                => ".NET SDK"
    };

}