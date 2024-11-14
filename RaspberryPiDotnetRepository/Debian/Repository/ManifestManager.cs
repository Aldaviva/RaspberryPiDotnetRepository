using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Azure;
using RaspberryPiDotnetRepository.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Debian.Repository;

public interface ManifestManager {

    string manifestFilename { get; }
    string manifestFilePath { get; }

    Task<RepositoryManifest?> downloadManifest(CancellationToken ct = default);
    string serializeManifest(RepositoryManifest manifest);

}

public class ManifestManagerImpl(BlobStorageClient blobStorage, IOptions<Options> options, ILogger<ManifestManagerImpl> logger): ManifestManager {

    private static readonly JsonSerializerOptions JSON_OPTIONS = new() {
        Converters    = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public string manifestFilename { get; } = "manifest.json";
    public string manifestFilePath => Path.Combine(options.Value.repositoryBaseDir, manifestFilename);

    public async Task<RepositoryManifest?> downloadManifest(CancellationToken ct = default) {
        await using Stream? oldManifestStream = (await blobStorage.readFile(manifestFilename, ct))?.Content.ToStream();
        if (oldManifestStream != null) {
            RepositoryManifest repositoryManifest;
            try {
                repositoryManifest = (await JsonSerializer.DeserializeAsync<RepositoryManifest>(oldManifestStream, JSON_OPTIONS, ct))!;
            } catch (JsonException e) {
                logger.LogWarning(e, "Package manifest JSON file {filename} was corrupted, ignoring it and regenerating all packages", manifestFilename);
                return null;
            }

            foreach (DebianPackage package in repositoryManifest.packages) {
                package.isUpToDateInBlobStorage = true;
            }
            return repositoryManifest;
        } else {
            return null;
        }
    }

    public string serializeManifest(RepositoryManifest manifest) {
        return JsonSerializer.Serialize(manifest, JSON_OPTIONS);
    }

}