using RaspberryPiDotnetRepository.Data;

namespace RaspberryPiDotnetRepository.DotnetUpstream;

public record UpstreamReleasesState(IList<DotnetRelease> releases, UpstreamReleasesSecondaryInfo secondaryInfo);

public record UpstreamReleasesSecondaryInfo(IEnumerable<Version> knownReleaseMinorRuntimeVersions, IEnumerable<Version> knownReleaseSdkVersions, Version leastProvidedCurrentReleaseMinorVersion);