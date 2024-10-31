using RaspberryPiDotnetRepository.Data;

namespace RaspberryPiDotnetRepository.DotnetUpstream;

public record UpstreamReleasesState(IList<DotnetRelease> releases, UpstreamReleasesSecondaryInfo secondaryInfo);

/// <param name="knownReleaseMinorRuntimeVersions">something like [6.0, 7.0, 8.0]</param>
/// <param name="knownReleaseSdkVersions"></param>
/// <param name="leastProvidedReleaseMinorVersion">usually 6.0</param>
public record UpstreamReleasesSecondaryInfo(IEnumerable<Version> knownReleaseMinorRuntimeVersions, IEnumerable<Version> knownReleaseSdkVersions, Version leastProvidedReleaseMinorVersion);