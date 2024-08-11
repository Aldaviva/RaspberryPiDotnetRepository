using RaspberryPiDotnetRepository.Data;

namespace RaspberryPiDotnetRepository.Debian.Package;

/// <summary>
/// Generate a list of Debian packages that should exist, given the upstream .NET releases.
/// This is the Cartesian product of the .NET versions, .NET runtimes and SDK, Debian version, and CPU architecture.
/// This does not actually generate the package files, this just lists the ones that should exist. Generation is handled by <see cref="PackageGenerator"/>, which uses <see cref="PackageBuilder"/>.
/// </summary>
public interface PackageRequester {

    IEnumerable<PackageRequest> listPackagesToRequest(IEnumerable<DotnetRelease> upstreamReleases);

}

public class PackageRequesterImpl: PackageRequester {

    private readonly CpuArchitecture[] cpuArchitectures = Enum.GetValues<CpuArchitecture>();

    private readonly RuntimeType[] dotnetRuntimes = Enum.GetValues<RuntimeType>()
        // .Where(type => type is not (RuntimeType.SDK or RuntimeType.ASPNETCORE_RUNTIME))
        .ToArray();

    public IEnumerable<PackageRequest> listPackagesToRequest(IEnumerable<DotnetRelease> upstreamReleases) {
        upstreamReleases = upstreamReleases.ToList();
        IList<PackageRequest> packageRequests    = [];
        DotnetRelease         latestLongTerm     = upstreamReleases.First(release => release is { isSupportedLongTerm: true, isLatestOfSupportTerm: true });
        DotnetRelease         latestMinorRelease = upstreamReleases.First(release => release.isLatestMinorVersion);

        foreach (CpuArchitecture cpuArchitecture in cpuArchitectures) {
            foreach (DotnetRelease dotnetRelease in upstreamReleases) {
                foreach (RuntimeType dotnetRuntime in dotnetRuntimes) {
                    packageRequests.Add(new DotnetPackageRequest(dotnetRelease, dotnetRuntime, cpuArchitecture, dotnetRelease.downloadedSdkArchiveFilePaths[cpuArchitecture]));
                }
            }
        }

        foreach (RuntimeType dotnetRuntime in dotnetRuntimes.Except([RuntimeType.CLI])) {
            packageRequests.Add(new MetaPackageRequest(dotnetRuntime, true, latestLongTerm.sdkVersion.AsMinor()));
            packageRequests.Add(new MetaPackageRequest(dotnetRuntime, false, latestMinorRelease.sdkVersion.AsMinor()));
        }

        return packageRequests;
    }

}