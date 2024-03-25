namespace RaspberryPiDotnetRepository.Data;

// These must be sorted in ascending order to make version comparisons work, like BOOKWORM > BULLSEYE
public enum DebianRelease {

    /// <summary>
    /// Debian 10
    /// </summary>
    BUSTER = 10,

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

    public static int getMajorVersion(this DebianRelease release) => (int) release;

    public static string getCodename(this DebianRelease release) => Enum.GetName(release)!.ToLowerInvariant();

}