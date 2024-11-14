namespace RaspberryPiDotnetRepository.Data;

public record DotnetRelease(Version sdkVersion, Version runtimeVersion, bool isSupportedLongTerm) {

    public IDictionary<CpuArchitecture, string> downloadedSdkArchiveFilePaths { get; } = new Dictionary<CpuArchitecture, string>();
    public bool isLatestMinorVersion { get; set; } = false;
    public bool isLatestOfSupportTerm { get; set; } = false;

}