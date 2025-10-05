namespace RaspberryPiDotnetRepository.Data;

/*
 * https://www.debian.org/releases/
 * https://wiki.debian.org/DebianReleases
 */
public enum DebianRelease {

    /// <summary>
    /// Debian 10 Buster
    /// </summary>
    BUSTER = 10,

    /// <summary>
    /// Debian 11 Bullseye
    /// </summary>
    BULLSEYE = 11,

    /// <summary>
    /// Debian 12 Bookworm
    /// </summary>
    BOOKWORM = 12,

    /// <summary>
    /// Debian 13 Trixie
    /// </summary>
    TRIXIE = 13

    // When adding a new Debian release, make sure to also add the version of libicu it provides below in DebianVersionMethods.getLibIcuDependencyName

}

public static class DebianVersionsMethods {

    public static int getMajorVersion(this DebianRelease release) => (int) release;

    public static string getCodename(this DebianRelease release) => Enum.GetName(release)!.ToLowerInvariant();

    /// <summary>
    /// <para>Debian and Raspbian archive their releases when they are the fourth most recent release, and thus "oldoldstable" is the oldest they ever get before being frozen.</para>
    /// <para>In this repository, the number of "old" prefixes increases unbounded because it's funnier.</para>
    /// <para>For example, when Trixie is released, Buster will be archived by Debian and won't get security updates, keeping its previous "oldoldstable" suite because it's read-only. Bullseye will be renamed from "oldstable" to "oldoldstable" at that time. However, in this repository, I see no reason to delete existing packages for archived Debian versions, because people might still use them and it's no skin off my nose to keep building .NET packages for them (it's 100% automated and costs almost nothing). Therefore, this repository will continue releasing .NET packages for Buster even after Debian archives it, and I will call the suite "oldoldoldstable" at that time.</para>
    /// </summary>
    /// <param name="release">Debian version</param>
    /// <returns>Suite name of the release, which refers to its stability, age, and support state. The latest release is <c>stable</c>, the second latest is <c>oldstable</c>, third is <c>oldoldstable</c>, and the number of <c>old</c> prefixes keeps increasing unbounded.</returns>
    public static string getSuiteName(this DebianRelease release) => string.Join(null, Enumerable.Repeat("old", Enum.GetValues<DebianRelease>().Max() - release)) + "stable";

    public static string getLibIcuDependencyName(this DebianRelease release) => "libicu" + release switch {
        DebianRelease.BUSTER   => "63",
        DebianRelease.BULLSEYE => "67",
        DebianRelease.BOOKWORM => "72",
        DebianRelease.TRIXIE   => "76",
        _ => throw new ArgumentOutOfRangeException(nameof(release), release,
            $"Please update {nameof(DebianVersionsMethods)}.{nameof(getLibIcuDependencyName)}({nameof(DebianRelease)}) to handle Debian {release.getCodename()} based on https://packages.debian.org/search?suite={release.getCodename()}&searchon=names&keywords=libicu")
    };

}