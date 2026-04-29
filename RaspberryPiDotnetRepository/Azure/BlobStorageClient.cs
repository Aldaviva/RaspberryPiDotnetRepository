using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Data;
using System.Net;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Azure;

public interface BlobStorageClient {

    Task<IEnumerable<BlobItem>> listFiles(string? baseDir = null, CancellationToken ct = default);

    Task<(HttpStatusCode status, BlobDownloadResult? response)> readFile(string blobFilePath, FileCacheState? cacheState = null, CancellationToken ct = default);

    Task<BlobContentInfo?> uploadFile(string filename, string destinationBlobFilePath, string? contentType = null, CancellationToken ct = default);

    Task deleteFile(string blobFilePath, CancellationToken ct = default);

}

public class BlobStorageClientImpl(BlobContainerClient container, UploadProgressFactory uploadProgress, IOptions<Options> options, ILogger<BlobStorageClientImpl> logger)
    : BlobStorageClient, IDisposable {

    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromDays(180); // the max Azure CDN cache duration is 366 days

    private readonly SemaphoreSlim uploadSemaphore = new(options.Value.storageParallelUploads);

    private BlobClient openFile(string blobFilePath) => container.GetBlobClient(blobFilePath);

    public async Task<(HttpStatusCode status, BlobDownloadResult? response)> readFile(string blobFilePath, FileCacheState? cacheState = null, CancellationToken ct = default) {
        logger.Debug("Downloading {file}", blobFilePath);
        try {
            BlobDownloadOptions? downloadOptions = cacheState.HasValue ? new BlobDownloadOptions {
                Conditions = new BlobRequestConditions {
                    IfModifiedSince = cacheState.Value.lastModified,
                    IfNoneMatch     = cacheState.Value.etag is {} etag ? new ETag(etag) : null
                }
            } : null;

            Response<BlobDownloadResult> response = await openFile(blobFilePath).DownloadContentAsync(downloadOptions, ct);
            return (status: (HttpStatusCode) response.GetRawResponse().Status, response: response.AsNullable);
        } catch (RequestFailedException e) {
            return (status: (HttpStatusCode) e.Status, response: null);
        }
    }

    public async Task<IEnumerable<BlobItem>> listFiles(string? baseDir = null, CancellationToken ct = default) {
        await using IAsyncEnumerator<BlobItem> enumerator = container.GetBlobsAsync(new GetBlobsOptions { Prefix = baseDir }, cancellationToken: ct).GetAsyncEnumerator(ct);
        return await enumerator.ToList();
    }

    protected async Task<BlobContentInfo?> uploadFile(Stream source, string destinationBlobFilePath, string? contentType = null, CancellationToken ct = default) {
        destinationBlobFilePath = Path.Dos2UnixSlashes(destinationBlobFilePath);
        await uploadSemaphore.WaitAsync(ct);
        try {
            if (!options.Value.dryRun) {
                logger.Info("Uploading to {dest}", destinationBlobFilePath);
                using DisposableProgress<long> progressHandler = uploadProgress.registerFile(destinationBlobFilePath, source.Length);
                return (await openFile(destinationBlobFilePath).UploadAsync(source, new BlobHttpHeaders {
                        ContentType  = contentType,
                        CacheControl = $"public, max-age={CACHE_DURATION.TotalSeconds:F0}"
                    },
                    progressHandler: progressHandler,
                    transferOptions: new StorageTransferOptions { MaximumConcurrency = options.Value.storageParallelUploads },
                    cancellationToken: ct)).AsNullable;
            } else {
                logger.Info("Would have uploaded to {dest} if not in dry run", destinationBlobFilePath);
                return null;
            }
        } catch (Exception e) when (e is not OutOfMemoryException) {
            logger.Error(e, "Failed to upload to {dest}", destinationBlobFilePath);
            throw;
        } finally {
            uploadSemaphore.Release();
        }
    }

    public async Task<BlobContentInfo?> uploadFile(string filename, string destinationBlobFilePath, string? contentType = null, CancellationToken ct = default) {
        await using FileStream fileStream = File.OpenRead(filename);
        return await uploadFile(fileStream, destinationBlobFilePath, contentType, ct);
    }

    public async Task deleteFile(string blobFilePath, CancellationToken ct = default) {
        if (!options.Value.dryRun) {
            logger.Debug("Deleting {file}", blobFilePath);
            await container.DeleteBlobIfExistsAsync(blobFilePath, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        } else {
            logger.Debug("Would have deleted {file} if not in dry run", blobFilePath);
        }
    }

    public void Dispose() {
        uploadSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }

}