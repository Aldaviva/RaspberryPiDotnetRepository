using DataSizeUnits;
using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Unfucked.HTTP;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.DotnetUpstream;

public interface SdkDownloader {

    Task<UpstreamReleasesState> downloadSdks(Version minMinorVersion, CancellationToken ct = default);

    void deleteSdksExcept(IEnumerable<DotnetRelease> sdksToKeep);

}

public class SdkDownloaderImpl(HttpClient httpClient, IOptions<Options> options, ILogger<SdkDownloaderImpl> logger): SdkDownloader {

    // Found on https://github.com/dotnet/core#release-information
    // #34: Use the new Azure Traffic Manager domain name instead of the Azure Blob Storage domain for performance, as recommended by https://devblogs.microsoft.com/dotnet/critical-dotnet-install-links-are-changing/#call-to-action
    private static readonly Uri DOTNET_RELEASE_INDEX = new("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json");

    private readonly CpuArchitecture[] allCpuArchitectures = Enum.GetValues<CpuArchitecture>();

    /// <param name="minMinorVersion">The oldest upstream minor version we want to package. Should be 6.0.</param>
    /// <param name="ct"></param>
    private async Task<IEnumerable<JsonNode>> listReleasesToPackage(Version minMinorVersion, CancellationToken ct = default) =>
        (await httpClient.Target(DOTNET_RELEASE_INDEX).Get<JsonNode>(ct))
        ["releases-index"]!
        .AsArray()
        .Compact()
        .Where(release => release["support-phase"]!.GetValue<string>() is "active" or "maintenance" or "eol"
            && Version.Parse(release["channel-version"]!.GetValue<string>()).AsMinor() >= minMinorVersion.AsMinor());

    public async Task<UpstreamReleasesState> downloadSdks(Version minMinorVersion, CancellationToken ct = default) {
        Directory.CreateDirectory(options.Value.tempDir);
        List<DotnetRelease> dotnetReleases = (await Task.WhenAll((await listReleasesToPackage(minMinorVersion, ct))
            .Select(async upstreamRelease => {
                Uri      patchVersionUrl     = new(upstreamRelease["releases.json"]!.GetValue<string>());
                bool     isSupportedLongTerm = upstreamRelease["release-type"]!.GetValue<string>() == "lts";
                JsonNode patchVersions       = await httpClient.Target(patchVersionUrl).Get<JsonNode>(ct);

                // this will always find at least one because we're excluding preview support phases from releasesToPackage above
                JsonNode latestSdk = patchVersions["releases"]!.AsArray().First(isStableVersion)!["sdk"]!;
                DotnetRelease dotnetRelease = new(
                    Version.Parse(latestSdk["version"]!.GetValue<string>()),
                    Version.Parse(latestSdk["runtime-version"]!.GetValue<string>()),
                    isSupportedLongTerm);

                await Task.WhenAll(allCpuArchitectures.Select(async cpuArchitecture => {
                    string   rid                   = "linux-" + cpuArchitecture.toRuntimeIdentifierSuffix();
                    JsonNode archSpecificFiles     = latestSdk["files"]!.AsArray().First(file => file?["rid"]?.GetValue<string>() == rid)!;
                    Uri      sdkDownloadUrl        = new(archSpecificFiles["url"]!.GetValue<string>());
                    byte[]   expectedSdkSha512Hash = Convert.FromHexString(archSpecificFiles["hash"]!.GetValue<string>());

                    Stopwatch downloadTimer         = new();
                    string    downloadedSdkFilename = Path.Combine(options.Value.tempDir, Path.GetFileName(sdkDownloadUrl.LocalPath));
                    dotnetRelease.downloadedSdkArchiveFilePaths[cpuArchitecture] = downloadedSdkFilename;
                    await using FileStream fileDownloadStream = File.Open(downloadedSdkFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                    if (fileDownloadStream.Length == 0) {
                        logger.LogInformation("Downloading .NET SDK {version} {arch}...", dotnetRelease.sdkVersion.ToString(3), cpuArchitecture.toRuntimeIdentifierSuffix());
                        downloadTimer.Start();
                        await using Stream downloadStream = await httpClient.Target(sdkDownloadUrl).Get<Stream>(ct);
                        await downloadStream.CopyToAsync(fileDownloadStream, ct);
                        await fileDownloadStream.FlushAsync(ct);
                        downloadTimer.Stop();

                        DataSize fileSizeOnDisk         = new(fileDownloadStream.Length);
                        DataSize downloadSpeedPerSecond = fileSizeOnDisk / downloadTimer.Elapsed.TotalSeconds;
                        logger.LogInformation(@"Downloaded .NET SDK {version} {arch} in {time:m\m\ ss\s} ({size} at {speed}/s)", dotnetRelease.sdkVersion.ToString(3),
                            cpuArchitecture.toRuntimeIdentifierSuffix(), downloadTimer.Elapsed, fileSizeOnDisk.ToString(1, true), downloadSpeedPerSecond.ToString(1, true));
                    }

                    fileDownloadStream.Position = 0;
                    byte[] actualSdkSha512Hash = await SHA512.HashDataAsync(fileDownloadStream, ct);
                    if (actualSdkSha512Hash.SequenceEqual(expectedSdkSha512Hash)) {
                        logger.LogInformation("Successfully verified .NET SDK {version} {arch} file hash.", dotnetRelease.sdkVersion.ToString(3), cpuArchitecture.toRuntimeIdentifierSuffix());
                    } else {
                        logger.LogError("""
                                        Failed to verify .NET SDK {version} {arch}!
                                        Expected SHA-512 hash of {url}: {expected}
                                        Actual SHA-512 hash of {filename}: {actual}
                                        """, dotnetRelease.sdkVersion.ToString(3), cpuArchitecture.toRuntimeIdentifierSuffix(), sdkDownloadUrl, Convert.ToHexString(expectedSdkSha512Hash),
                            downloadedSdkFilename, Convert.ToHexString(actualSdkSha512Hash));
                        throw new ApplicationException($"Verification failed after downloading {sdkDownloadUrl}");
                    }
                }));

                return dotnetRelease;
            }))).Compact().OrderByDescending(release => release.sdkVersion).ToList();

        dotnetReleases.First().isLatestMinorVersion                                        = true;
        dotnetReleases.First(release => release.isSupportedLongTerm).isLatestOfSupportTerm = true;
        if (dotnetReleases.FirstOrDefault(release => !release.isSupportedLongTerm) is { } latestShortTerm) {
            latestShortTerm.isLatestOfSupportTerm = true;
        }

        return new UpstreamReleasesState(dotnetReleases, new UpstreamReleasesSecondaryInfo(
            knownReleaseMinorRuntimeVersions: dotnetReleases.Select(release => release.runtimeVersion.AsMinor()).ToList().AsReadOnly(),
            knownReleaseSdkVersions: dotnetReleases.Select(release => release.sdkVersion).ToList().AsReadOnly(),
            leastProvidedReleaseMinorVersion: dotnetReleases.Last().sdkVersion.AsMinor()));
    }

    public void deleteSdksExcept(IEnumerable<DotnetRelease> sdksToKeep) {
        IEnumerable<string> allSdks      = Directory.EnumerateFiles(options.Value.tempDir, "*.tar.gz", SearchOption.TopDirectoryOnly);
        IEnumerable<string> sdksToDelete = allSdks.Except(sdksToKeep.SelectMany(sdkToKeep => sdkToKeep.downloadedSdkArchiveFilePaths.Values));

        foreach (string sdkToDelete in sdksToDelete) {
            if (!options.Value.dryRun) {
                File.Delete(sdkToDelete);
                logger.LogDebug("Deleted outdated SDK file {file}", sdkToDelete);
            } else {
                logger.LogDebug("Would have deleted outdated SDK file {file} if not in dry-run mode", sdkToDelete);
            }
        }
    }

    private static bool isStableVersion(JsonNode? release) {
        string patchVersionNumber = release!["release-version"]!.GetValue<string>();
        return !patchVersionNumber.Contains("-preview.") && !patchVersionNumber.Contains("-rc.");
    }

}