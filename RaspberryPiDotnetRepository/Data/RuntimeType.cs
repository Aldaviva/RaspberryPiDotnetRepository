namespace RaspberryPiDotnetRepository.Data;

public enum RuntimeType {

    CLI,
    RUNTIME,
    ASPNETCORE_RUNTIME,
    SDK

}

public static class RuntimeTypeMethods {

    public static string getPackageName(this RuntimeType packageType) => packageType switch {
        RuntimeType.CLI                => "dotnet-cli",
        RuntimeType.RUNTIME            => "dotnet-runtime",
        RuntimeType.ASPNETCORE_RUNTIME => "aspnetcore-runtime",
        RuntimeType.SDK                => "dotnet-sdk"
    };

    public static string getFriendlyName(this RuntimeType packageType) => packageType switch {
        RuntimeType.CLI                => ".NET CLI",
        RuntimeType.RUNTIME            => ".NET Runtime",
        RuntimeType.ASPNETCORE_RUNTIME => "ASP.NET Core Runtime",
        RuntimeType.SDK                => ".NET SDK"
    };

}