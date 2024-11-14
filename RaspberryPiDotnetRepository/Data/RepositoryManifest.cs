namespace RaspberryPiDotnetRepository.Data;

public record RepositoryManifest(ICollection<DebianPackage> packages, ISet<DebianRelease> debianReleases, string versionSuffix, ISet<Version> dotnetSdkVersions) {

    public virtual bool Equals(RepositoryManifest? other) => other is not null && (ReferenceEquals(this, other)
        || (packages.EqualsUnordered(other.packages) &&
            debianReleases.SetEquals(other.debianReleases) &&
            dotnetSdkVersions.SetEquals(other.dotnetSdkVersions) &&
            versionSuffix == other.versionSuffix));

    public override int GetHashCode() => HashCode.Combine(packages, debianReleases, dotnetSdkVersions, versionSuffix);

    public bool isUpToDate(IEnumerable<Version> newDotnetSdkVersions) =>
        dotnetSdkVersions.SetEquals(newDotnetSdkVersions) &&
        debianReleases.SetEquals(Enum.GetValues<DebianRelease>()) &&
        versionSuffix == DebianPackage.VERSION_SUFFIX;

}