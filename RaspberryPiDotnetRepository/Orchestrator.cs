using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Azure;
using RaspberryPiDotnetRepository.Data;
using RaspberryPiDotnetRepository.Debian.Package;
using RaspberryPiDotnetRepository.Debian.Repository;
using RaspberryPiDotnetRepository.DotnetUpstream;
using System.Text;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository;

public class Orchestrator(
    SdkDownloader sdkDownloader,
    PackageRequester packageRequester,
    PackageGenerator packageGenerator,
    Indexer indexer,
    CdnClient cdnClient,
    ExtraFileGenerator extraFileGenerator,
    ManifestManager manifestManager,
    BlobStorageClient blobStorage,
    StatisticsService statistics,
    IHostApplicationLifetime appLifetime,
    IOptions<Options> options,
    ILogger<Orchestrator> logger
): BackgroundService {

    private static readonly Version MIN_DOTNET_MINOR_VERSION = new(6, 0);

    protected override async Task ExecuteAsync(CancellationToken ct) {
        statistics.startTimer();

        RepositoryManifest? oldManifest = await manifestManager.downloadManifest(ct);

        // Download .NET SDK .tar.gz binaries from Microsoft
        (IList<DotnetRelease> upstreamReleases, UpstreamReleasesSecondaryInfo upstreamInfo) = await sdkDownloader.downloadSdks(MIN_DOTNET_MINOR_VERSION, ct);

        // Stop early if repo is already up to date
        if (!oldManifest?.isUpToDate(upstreamInfo.knownReleaseSdkVersions) ?? true) {

            // Generate .deb package files
            IEnumerable<PackageRequest> packagesToRequest = packageRequester.listPackagesToRequest(upstreamReleases);
            DebianPackage[] generatedPackages = await Task.WhenAll(packagesToRequest.AsParallel().Select(request => packageGenerator.generateDebPackage(request, upstreamInfo, oldManifest)));

            // Generate manifest.json file
            RepositoryManifest newManifest = new(generatedPackages, Enum.GetValues<DebianRelease>().ToHashSet(),
                generatedPackages[0].versionSuffix, upstreamReleases.Select(release => release.sdkVersion).ToHashSet());
            string newManifestJson = manifestManager.serializeManifest(newManifest);
            await File.WriteAllTextAsync(manifestManager.manifestFilePath, newManifestJson, Encoding.UTF8, ct);

            // Generate Packages.gz index files
            IEnumerable<IGrouping<DebianRelease, PackageIndexFile>> packageIndexesByDebianRelease = (await indexer.generatePackageIndex(generatedPackages, upstreamInfo)).ToList();

            // Generate InRelease index files
            ReleaseIndexFile[] releaseIndexFiles = await Task.WhenAll(packageIndexesByDebianRelease.Select(releaseFiles => indexer.generateReleaseIndex(releaseFiles.Key, releaseFiles)));

            // Write readme, badges, and GPG public key
            string                      readmeFilename    = await extraFileGenerator.generateReadme();
            IEnumerable<UploadableFile> badgeFiles        = await extraFileGenerator.generateReadmeBadges(upstreamReleases.First(release => release.isLatestMinorVersion));
            string                      gpgPublicKeyFile  = extraFileGenerator.copyGpgPublicKey();
            string                      addRepoScriptFile = await extraFileGenerator.generateAddRepoScript();

            // Upload .deb packages to Azure Blob Storage
            string repoBaseDir = options.Value.repositoryBaseDir;
            await Task.WhenAll(generatedPackages.Where(p => !p.isUpToDateInBlobStorage).Select(p =>
                blobStorage.uploadFile(Path.Combine(repoBaseDir, p.filePathRelativeToRepo), p.filePathRelativeToRepo, "application/vnd.debian.binary-package", ct)));

            // Upload Packages.gz indexes to Azure Blob Storage
            Task<BlobContentInfo?[]> packageIndexUploads = Task.WhenAll(packageIndexesByDebianRelease.SelectMany(debianRelease => debianRelease).Select(packageIndexFile =>
                blobStorage.uploadFile(Path.Combine(repoBaseDir, packageIndexFile.filePathRelativeToRepo), packageIndexFile.filePathRelativeToRepo,
                    packageIndexFile.isCompressed ? "application/gzip" : "text/plain", ct)));

            // Upload InRelease indexes to Azure Blob Storage
            Task<BlobContentInfo?[]> releaseIndexUploads = Task.WhenAll(releaseIndexFiles.SelectMany(file =>
                new[] { file.inreleaseFilePathRelativeToRepo, file.releaseFilePathRelativeToRepo, file.releaseGpgFilePathRelativeToRepo }.Select(relativeFilePath =>
                    blobStorage.uploadFile(Path.Combine(repoBaseDir, relativeFilePath), relativeFilePath,
                        ".gpg".Equals(Path.GetExtension(relativeFilePath), StringComparison.OrdinalIgnoreCase) ? "application/pgp-signature" : "text/plain", ct))));

            await packageIndexUploads;
            await releaseIndexUploads;

            // Upload badge JSON and other extra files to Azure Blob Storage
            await Task.WhenAll(badgeFiles.Select(file => blobStorage.uploadFile(Path.Combine(repoBaseDir, file.filePathRelativeToRepo), file.filePathRelativeToRepo, "application/json", ct)));
            await blobStorage.uploadFile(Path.Combine(repoBaseDir, readmeFilename), readmeFilename, "text/plain", ct);
            await blobStorage.uploadFile(Path.Combine(repoBaseDir, gpgPublicKeyFile), gpgPublicKeyFile, "application/pgp-keys", ct);
            await blobStorage.uploadFile(Path.Combine(repoBaseDir, addRepoScriptFile), addRepoScriptFile, "application/x-sh", ct);

            // Upload manifest.json file to Azure Blob Storage
            await blobStorage.uploadFile(manifestManager.manifestFilePath, manifestManager.manifestFilename, "application/json", ct);

            // Clear CDN cache
            await cdnClient.purge(["/dists/*", "/badges/*", "/manifest.json"]);

            // Delete outdated .deb package files from Azure Blob Storage
            await Task.WhenAll(oldManifest?.packages.Except(newManifest.packages).Select(packageToDelete => blobStorage.deleteFile(packageToDelete.filePathRelativeToRepo, ct)) ?? []);

            // Delete old downloaded SDK .tar.gz files from previous patch versions
            sdkDownloader.deleteSdksExcept(upstreamReleases);

        } else {
            logger.LogInformation("Repository is already up to date according to the manifest file, stopping without generating or uploading any files.");
        }

        TimeSpan elapsed = statistics.stopTimer();
        logger.LogInformation(@"Finished in {elapsed:m\m\ ss\s}. Wrote {size} to {files:N0} files.", elapsed, statistics.dataWritten.ToString(1, true), statistics.filesWritten);

        appLifetime.StopApplication();
    }

}