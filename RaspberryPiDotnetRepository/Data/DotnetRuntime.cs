namespace RaspberryPiDotnetRepository.Data;

public enum DotnetRuntime {

    CLI,
    RUNTIME,
    ASPNETCORE_RUNTIME,
    SDK

}

public static class RuntimeTypeMethods {

    public static string getPackageName(this DotnetRuntime dotnetRuntime) => dotnetRuntime switch {
        DotnetRuntime.CLI                => "dotnet-cli",
        DotnetRuntime.RUNTIME            => "dotnet-runtime",
        DotnetRuntime.ASPNETCORE_RUNTIME => "aspnetcore-runtime",
        DotnetRuntime.SDK                => "dotnet-sdk"
    };

    public static string getFriendlyName(this DotnetRuntime dotnetRuntime) => dotnetRuntime switch {
        DotnetRuntime.CLI                => "/usr/bin/dotnet",
        DotnetRuntime.RUNTIME            => ".NET Runtime",
        DotnetRuntime.ASPNETCORE_RUNTIME => "ASP.NET Core Runtime",
        DotnetRuntime.SDK                => ".NET SDK"
    };

}