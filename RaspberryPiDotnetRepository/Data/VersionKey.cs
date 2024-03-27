namespace RaspberryPiDotnetRepository.Data;

public record VersionKey(ISet<string> dotnetVersions, ISet<int> debianVersions) {

    public virtual bool Equals(VersionKey? other) {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return dotnetVersions.SetEquals(other.dotnetVersions) && debianVersions.SetEquals(other.debianVersions);
    }

    public override int GetHashCode() {
        return HashCode.Combine(dotnetVersions, debianVersions);
    }

}