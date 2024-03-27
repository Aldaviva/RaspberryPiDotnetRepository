namespace RaspberryPiDotnetRepository.Data;

// These must be sorted in ascending order to make version comparisons work, like BOOKWORM > BULLSEYE
public enum DebianRelease {

    /// <summary>
    /// Debian 10 Buster
    /// </summary>
    BUSTER = 10,

    /// <summary>
    /// Debian 11 Bullseye
    /// </summary>
    BULLSEYE,

    /// <summary>
    /// Debian 12 Bookworm
    /// </summary>
    BOOKWORM

    // When adding a new Debian release, make sure to also add the version of libicu it provides below, in DebianVersionsMethods.getLibicuDependencyName

}

public static class DebianVersionsMethods {

    public static int getMajorVersion(this DebianRelease release) => (int) release;

    public static string getCodename(this DebianRelease release) => Enum.GetName(release)!.ToLowerInvariant();

    public static string getLibIcuDependencyName(this DebianRelease release) => "libicu" + release switch {
        DebianRelease.BUSTER   => "63",
        DebianRelease.BULLSEYE => "67",
        DebianRelease.BOOKWORM => "72"
    };

}