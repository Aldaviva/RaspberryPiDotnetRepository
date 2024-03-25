namespace RaspberryPiDotnetRepository.Data;

public record DotnetRelease(string minorVersion, string patchVersion, bool isSupportedLongTerm) {

    public IDictionary<CpuArchitecture, string> downloadedSdkArchiveFilePaths { get; } = new Dictionary<CpuArchitecture, string>();
    public bool isLatestMinorVersion { get; set; } = false;
    public bool isLatestOfSupportTerm { get; set; } = false;

    public float minorVersionNumeric => float.Parse(minorVersion);

}