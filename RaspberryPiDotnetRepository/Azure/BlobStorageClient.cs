﻿using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using ThrottleDebounce;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Azure;

public interface BlobStorageClient {

    Task<IEnumerable<BlobItem>> listFiles(string? baseDir = default, CancellationToken ct = default);

    Task<BlobDownloadResult?> readFile(string blobFilePath, CancellationToken ct = default);

    Task<BlobContentInfo?> uploadFile(Stream source,   string destinationBlobFilePath, string? contentType = default, CancellationToken ct = default);
    Task<BlobContentInfo?> uploadFile(string filename, string destinationBlobFilePath, string? contentType = default, CancellationToken ct = default);

    Task deleteFile(string blobFilePath, CancellationToken ct = default);

}

public class BlobStorageClientImpl(BlobContainerClient container, IOptions<Options> options, ILogger<BlobStorageClientImpl> logger): BlobStorageClient, IDisposable {

    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromDays(90);

    private readonly SemaphoreSlim uploadSemaphore = new(1);

    private BlobClient openFile(string blobFilePath) {
        return container.GetBlobClient(blobFilePath);
    }

    public async Task<BlobDownloadResult?> readFile(string blobFilePath, CancellationToken ct = default) {
        logger.LogDebug("Downloading {file}", blobFilePath);
        try {
            return (await openFile(blobFilePath).DownloadContentAsync(ct)).AsNullable();
        } catch (RequestFailedException e) {
            if (e.ErrorCode == "BlobNotFound") {
                return null;
            } else {
                throw;
            }
        }
    }

    public async Task<IEnumerable<BlobItem>> listFiles(string? baseDir = default, CancellationToken ct = default) {
        await using IAsyncEnumerator<BlobItem> enumerator = container.GetBlobsAsync(prefix: baseDir, cancellationToken: ct).GetAsyncEnumerator(ct);
        return await enumerator.ToList();
    }

    public async Task<BlobContentInfo?> uploadFile(Stream source, string destinationBlobFilePath, string? contentType = default, CancellationToken ct = default) {
        destinationBlobFilePath = Paths.Dos2UnixSlashes(destinationBlobFilePath);
        await uploadSemaphore.WaitAsync(ct);
        try {
            logger.LogInformation("Uploading to {dest}", destinationBlobFilePath);
            using UploadProgressHandler uploadProgressHandler = new(destinationBlobFilePath, source.Length, logger);
            return (await openFile(destinationBlobFilePath).UploadAsync(source, new BlobHttpHeaders {
                    ContentType  = contentType,
                    CacheControl = $"public, max-age={CACHE_DURATION.TotalSeconds:F0}"
                },
                progressHandler: uploadProgressHandler,
                transferOptions: new StorageTransferOptions { MaximumConcurrency = (int) Math.Max(1, options.Value.storageParallelUploads) },
                cancellationToken: ct)).AsNullable();
        } catch (Exception e) when (e is not OutOfMemoryException) {
            logger.LogError(e, "Failed to upload to {dest}", destinationBlobFilePath);
            throw;
        } finally {
            uploadSemaphore.Release();
        }
    }

    public async Task<BlobContentInfo?> uploadFile(string filename, string destinationBlobFilePath, string? contentType = default, CancellationToken ct = default) {
        await using FileStream fileStream = File.OpenRead(filename);
        return await uploadFile(fileStream, destinationBlobFilePath, contentType, ct);
    }

    public async Task deleteFile(string blobFilePath, CancellationToken ct = default) {
        logger.LogDebug("Deleting {file}", blobFilePath);
        await container.DeleteBlobIfExistsAsync(blobFilePath, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }

    public void Dispose() {
        uploadSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    private class UploadProgressHandler: IProgress<long>, IDisposable {

        private static readonly string ZERO_PERCENT        = 0.0.ToString("P0", CultureInfo.CurrentCulture);
        private static readonly string ONE_HUNDRED_PERCENT = 1.0.ToString("P0", CultureInfo.CurrentCulture);

        private readonly string                         destinationPath;
        private readonly long                           totalSize;
        private readonly ILogger<BlobStorageClientImpl> logger;
        private readonly RateLimitedAction<string>      logProgressThrottled;

        private string? previousPercentage;

        public UploadProgressHandler(string destinationPath, long totalSize, ILogger<BlobStorageClientImpl> logger) {
            this.destinationPath = destinationPath;
            this.totalSize       = totalSize;
            this.logger          = logger;
            logProgressThrottled = Throttler.Throttle((string percentage) => logProgress(percentage), TimeSpan.FromSeconds(3));
        }

        public void Report(long uploadedBytes) {
            string percentage = ((double) uploadedBytes / totalSize).ToString("P0", CultureInfo.CurrentCulture);
            if (percentage != ZERO_PERCENT && percentage != ONE_HUNDRED_PERCENT && percentage != previousPercentage) {
                logProgressThrottled.Invoke(percentage);
            }
            previousPercentage = percentage;
        }

        private void logProgress(string percentage) {
            logger.LogDebug("Uploading to {dest}: {percentage,3}", destinationPath, percentage);
        }

        public void Dispose() {
            logProgressThrottled.Dispose();
        }

    }

}