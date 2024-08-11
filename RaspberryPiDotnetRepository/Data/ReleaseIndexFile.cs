namespace RaspberryPiDotnetRepository.Data;

/// <summary>
/// This represents <c>InRelease</c>, <c>Release</c>, or <c>Release.gpg</c> files in a Debian package repository
/// </summary>
public record ReleaseIndexFile(DebianRelease debianVersion, bool isUpToDateInBlobStorage = false) {

    private readonly string parentDirectory = Path.Combine("dists", debianVersion.getCodename());

    public string releaseFilePathRelativeToRepo => Path.Combine(parentDirectory, "Release");
    public string releaseGpgFilePathRelativeToRepo => Path.Combine(parentDirectory, "Release.gpg");
    public string inreleaseFilePathRelativeToRepo => Path.Combine(parentDirectory, "InRelease");

}