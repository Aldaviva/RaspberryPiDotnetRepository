namespace RaspberryPiDotnetRepository.Data;

public enum DebianRelease {

    /// <summary>
    /// Debian 10
    /// </summary>
    BUSTER,

    /// <summary>
    /// Debian 11
    /// </summary>
    BULLSEYE,

    /// <summary>
    /// Debian 12
    /// </summary>
    BOOKWORM

}

public static class DebianVersionsMethods {

    public static int getMajorVersion(this DebianRelease release) => release switch {
        DebianRelease.BUSTER   => 10,
        DebianRelease.BULLSEYE => 11,
        DebianRelease.BOOKWORM => 12
    };

    public static string getCodename(this DebianRelease release) => Enum.GetName(release)!.ToLowerInvariant();

}