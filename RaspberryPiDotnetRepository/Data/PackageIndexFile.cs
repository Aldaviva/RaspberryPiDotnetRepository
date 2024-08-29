namespace RaspberryPiDotnetRepository.Data;

/// <summary>
/// This represents <c>Packages.gz</c> or <c>Packages</c> files in a Debian package repository
/// </summary>
public record PackageIndexFile(DebianRelease debianVersion, CpuArchitecture architecture, bool isCompressed, bool isUpToDateInBlobStorage = false) {

    public string filePathRelativeToSuite => Path.Combine("main", $"binary-{architecture.toDebian()}", $"Packages{(isCompressed ? ".gz" : "")}");
    public string filePathRelativeToRepo => Path.Combine("dists", debianVersion.getCodename(), filePathRelativeToSuite);

}