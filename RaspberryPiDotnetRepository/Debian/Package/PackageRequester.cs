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

    private static readonly CpuArchitecture[] CPU_ARCHITECTURES = Enum.GetValues<CpuArchitecture>();

    private static readonly RuntimeType[] DOTNET_RUNTIMES = Enum.GetValues<RuntimeType>()
        // .Where(type => type is not RuntimeType.SDK)
        .ToArray();

    public IEnumerable<PackageRequest> listPackagesToRequest(IEnumerable<DotnetRelease> upstreamReleases) {
        upstreamReleases = upstreamReleases.ToList();
        IList<PackageRequest> packageRequests = [];
        foreach (CpuArchitecture cpuArchitecture in CPU_ARCHITECTURES) {
            foreach (DotnetRelease dotnetRelease in upstreamReleases) {
                foreach (RuntimeType dotnetRuntime in DOTNET_RUNTIMES) {
                    packageRequests.Add(new DotnetPackageRequest(dotnetRelease, dotnetRuntime, cpuArchitecture, dotnetRelease.downloadedSdkArchiveFilePaths[cpuArchitecture]));

                    if (dotnetRuntime != RuntimeType.CLI) {
                        if (dotnetRelease.isSupportedLongTerm) {
                            packageRequests.Add(new MetaPackageRequest(dotnetRuntime, cpuArchitecture, true, dotnetRelease.sdkVersion.AsMinor()));
                        }
                        packageRequests.Add(new MetaPackageRequest(dotnetRuntime, cpuArchitecture, false, dotnetRelease.sdkVersion.AsMinor()));
                    }
                }
            }
        }

        return packageRequests;
    }

}