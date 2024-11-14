namespace RaspberryPiDotnetRepository.Data;

public record VersionKey(ISet<string> dotnetVersions, ISet<int> debianVersions) {

    public virtual bool Equals(VersionKey? other) =>
        other is not null && (ReferenceEquals(this, other) || (dotnetVersions.SetEquals(other.dotnetVersions) && debianVersions.SetEquals(other.debianVersions)));

    public override int GetHashCode() => HashCode.Combine(dotnetVersions, debianVersions);

}