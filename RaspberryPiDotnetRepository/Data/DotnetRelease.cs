namespace RaspberryPiDotnetRepository.Data;

public record DotnetRelease(string minorVersion, string patchVersion) {

    public IDictionary<CpuArchitecture, string> downloadedSdkArchiveFilePaths { get; } = new Dictionary<CpuArchitecture, string>();

}