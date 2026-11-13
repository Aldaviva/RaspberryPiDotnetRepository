using NodaTime;
using System.Text.Json.Serialization;

namespace RaspberryPiDotnetRepository.Data;

public readonly record struct DotnetReleases {

    public Version channelVersion { get; init; }
    public string latestRelease { get; init; }
    public LocalDate latestReleaseDate { get; init; }
    public bool latestReleaseSecurity { get; init; }
    public string latestRuntime { get; init; }
    public string latestSdk { get; init; }
    public SupportPhase supportPhase { get; init; }
    public ReleaseType releaseType { get; init; }
    [JsonPropertyName("eol-date")] public LocalDate? endOfSupportDate { get; init; }
    public Uri lifecyclePolicy { get; init; }
    public IReadOnlyList<Release> releases { get; init; }

    public readonly record struct Release {

        public LocalDate releaseDate { get; init; }
        public string releaseVersion { get; init; }
        public bool security { get; init; }
        [JsonPropertyName("cve-list")] public IReadOnlyCollection<CveRecord> cveRecords { get; init; }
        public Uri releaseNotes { get; init; }

        public Runtime runtime { get; init; }
        public Sdk sdk { get; init; }
        public IReadOnlyCollection<Sdk> sdks { get; init; }
        public AspNetCoreRuntime aspNetCoreRuntime { get; init; }
        [JsonPropertyName("windowsdesktop")] public WindowsDesktopRuntime windowsDesktopRuntime { get; init; }

    }

    public enum SupportPhase {

        PREVIEW, GO_LIVE, ACTIVE, MAINTENANCE, [JsonStringEnumMemberName("eol")] END_OF_SUPPORT

    }

    public enum ReleaseType {

        LTS, STS

    }

    public readonly record struct CveRecord {

        [JsonPropertyName("cve-id")] public string id { get; init; }
        [JsonPropertyName("cve-url")] public Uri pageUrl { get; init; }

    }

    public abstract record AbstractRuntime {

        public required string version { get; init; }
        public required string versionDisplay { get; init; }

        public required IReadOnlyCollection<File> files { get; init; }

    }

    public record Runtime: AbstractRuntime {

        [JsonPropertyName("vs-version")] public string? visualStudioVersion { get; init; }

    }

    public record Sdk: Runtime {

        public required string runtimeVersion { get; init; }
        [JsonPropertyName("vs-support")] public required string visualStudioSupport { get; init; }
        public required string csharpVersion { get; init; }
        public required string fsharpVersion { get; init; }
        [JsonPropertyName("vb-version")] public required string visualBasicVersion { get; init; }

    }

    public record AspNetCoreRuntime: AbstractRuntime {

        [JsonPropertyName("vs-version")] public required string visualStudioVersion { get; init; }
        [JsonPropertyName("version-aspnetcoremodule")] public required IReadOnlyCollection<string> aspNetCoreModuleVersion { get; init; }

    }

    public record WindowsDesktopRuntime: AbstractRuntime;

    public readonly record struct File {

        public string name { get; init; }
        [JsonPropertyName("rid")] public string runtimeIdentifier { get; init; }
        public Uri url { get; init; }

        /// <summary>Lower hexadecimal SHA-512 hash</summary>
        [JsonPropertyName("hash")] public string sha512HashString { get; init; }

        /// <summary>SHA-512 hash</summary>
        [JsonIgnore] public byte[] sha512Hash => Convert.FromHexString(sha512HashString);

    }

}