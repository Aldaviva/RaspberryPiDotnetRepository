using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Azure;
using RaspberryPiDotnetRepository.Data;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Debian.Repository;

public interface ManifestManager {

    string manifestFilename { get; }
    string manifestFilePath { get; }
    string manifestCacheStateFilePath { get; }

    Task<RepositoryManifest?> downloadManifest(CancellationToken ct = default);
    (string manifest, string? cacheState) serializeManifest(RepositoryManifest manifest);

}

public class ManifestManagerImpl(BlobStorageClient blobStorage, IOptions<Options> options, ILogger<ManifestManagerImpl> logger): ManifestManager {

    private static readonly JsonSerializerOptions JSON_OPTIONS = new() {
        Converters    = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public string manifestFilename { get; } = "manifest.json";
    public string manifestFilePath => Path.Combine(options.Value.repositoryBaseDir, manifestFilename);
    public string manifestCacheStateFilePath => Path.Combine(options.Value.repositoryBaseDir, manifestFilename + ".cache");

    public async Task<RepositoryManifest?> downloadManifest(CancellationToken ct = default) {
        FileCacheState? fileCacheState = null;
        try {
            await using FileStream manifestCacheStateFile = File.OpenRead(manifestCacheStateFilePath);
            fileCacheState = await JsonSerializer.DeserializeAsync<FileCacheState>(manifestCacheStateFile, JSON_OPTIONS, ct);
        } catch (FileNotFoundException) {}

        (HttpStatusCode status, BlobDownloadResult? response) blobDownloadResult = await blobStorage.readFile(manifestFilename, fileCacheState, ct);
        RepositoryManifest?                                   repositoryManifest = null;
        switch (blobDownloadResult) {
            case { status: HttpStatusCode.OK, response.Content: {} body }: {
                await using Stream blobManifestStream = body.ToStream();

                try {
                    repositoryManifest = await JsonSerializer.DeserializeAsync<RepositoryManifest>(blobManifestStream, JSON_OPTIONS, ct);
                } catch (JsonException e) {
                    logger.Warn(e, "Package manifest JSON file {filename} was corrupted, ignoring it and regenerating all packages", manifestFilename);
                    return null;
                }

                repositoryManifest?.cacheState = new FileCacheState(blobDownloadResult.response.Details.LastModified, blobDownloadResult.response.Details.ETag.ToString());
                break;
            }
            case { status: HttpStatusCode.NotModified, response: null }: {
                logger.Debug("Reading up-to-date cache of repository manifest on disk instead of downloading it from Blob Storage again");
                await using FileStream manifestFileStream = File.OpenRead(manifestFilePath);
                repositoryManifest             = await JsonSerializer.DeserializeAsync<RepositoryManifest>(manifestFileStream, JSON_OPTIONS, ct);
                repositoryManifest?.cacheState = fileCacheState;
                break;
            }
            case { status: HttpStatusCode.NotFound }:
                logger.Debug("Package manifest JSON file {filename} was not found in Blob Storage, creating it from scratch", manifestFilename);
                break;
        }

        foreach (DebianPackage package in repositoryManifest?.packages ?? []) {
            package.isUpToDateInBlobStorage = true;
        }

        return repositoryManifest;
    }

    public (string manifest, string? cacheState) serializeManifest(RepositoryManifest manifest) => (
        manifest: JsonSerializer.Serialize(manifest, JSON_OPTIONS),
        cacheState: manifest.cacheState is not null ? JsonSerializer.Serialize(manifest.cacheState, JSON_OPTIONS) : null);

}