using Microsoft.Extensions.Options;
using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ThrottleDebounce;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Azure;

public interface UploadProgressFactory: IDisposable {

    DisposableProgress<long> registerFile(string filename, long totalSizeBytes);

}

public interface DisposableProgress<in T>: IProgress<T>, IDisposable;

public class MultiUploadProgress: UploadProgressFactory {

    private static readonly CultureInfo CULTURE          = CultureInfo.CurrentCulture;
    private static readonly TimeSpan    MIN_LOG_INTERVAL = TimeSpan.FromSeconds(3);

    private readonly ILogger<MultiUploadProgress>                        logger;
    private readonly IDictionary<string, FileUploadProgress>             activeUploads;
    private readonly object                                              activeUploadsLock = new();
    private readonly RateLimitedAction                                   printProgressThrottled;
    private readonly ArrayPool<KeyValuePair<string, FileUploadProgress>> snapshots;

    private string? mostRecentMessage;

    public MultiUploadProgress(ILogger<MultiUploadProgress> logger, IOptions<Options> options) {
        this.logger = logger;

        int storageParallelUploads = options.Value.storageParallelUploads;
        activeUploads          = new Dictionary<string, FileUploadProgress>(storageParallelUploads * 2);
        snapshots              = ArrayPool<KeyValuePair<string, FileUploadProgress>>.Create(storageParallelUploads * 2, storageParallelUploads);
        printProgressThrottled = Throttler.Throttle(printProgress, MIN_LOG_INTERVAL, false);
    }

    public DisposableProgress<long> registerFile(string filename, long totalSizeBytes) {
        FileUploadProgress fileUploadProgress = new(filename, totalSizeBytes);
        fileUploadProgress.PropertyChanged += onUploadProgress;
        lock (activeUploadsLock) {
            activeUploads.Add(filename, fileUploadProgress);
        }
        return new ProgressHandler(fileUploadProgress);
    }

    private void onUploadProgress(object? sender, PropertyChangedEventArgs e) {
        FileUploadProgress changedFile = (FileUploadProgress) sender!;
        if (changedFile.uploadedBytes >= changedFile.totalBytes) {
            changedFile.PropertyChanged -= onUploadProgress;
            lock (activeUploadsLock) {
                activeUploads.Remove(changedFile.name);
            }
        }
        printProgressThrottled.Invoke();
    }

    private void printProgress() {
        int                                        fileCount;
        KeyValuePair<string, FileUploadProgress>[] snapshot;
        lock (activeUploadsLock) {
            fileCount = activeUploads.Count;
            snapshot  = snapshots.Rent(fileCount);
            activeUploads.CopyTo(snapshot, 0);
        }

        string? message = fileCount > 0 ? string.Join(", ", snapshot
            .Take(fileCount)
            .Select(pair => pair.Value)
            .Where(progress => progress.uploadedBytes < progress.totalBytes)
            .Order()
            .Select(progress => string.Format(CULTURE, "{0} {1,3:P0}", Path.GetFileName(progress.name),
                Math.Round((double) progress.uploadedBytes / progress.totalBytes, 2, MidpointRounding.ToZero)))) : null;

        snapshots.Return(snapshot);

        if (message?.Length > 0 && message != mostRecentMessage) {
            logger.LogDebug("{progress}", message);
            mostRecentMessage = message;
        }
    }

    private class ProgressHandler: DisposableProgress<long> {

        private readonly FileUploadProgress file;

        internal ProgressHandler(FileUploadProgress file) => this.file = file;

        public void Report(long value) => file.uploadedBytes = value;

        public void Dispose() => file.uploadedBytes = file.totalBytes;

    }

    private record FileUploadProgress(string name, long totalBytes): INotifyPropertyChanged, IComparable<FileUploadProgress> {

        private static uint registrations;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly uint registrationOrder = Interlocked.Increment(ref registrations);

        private long _uploadedBytes;

        public long uploadedBytes {
            get => _uploadedBytes;
            set {
                if (value == _uploadedBytes) return;
                _uploadedBytes = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int CompareTo(FileUploadProgress? other) => ReferenceEquals(this, other) ? 0 : other is null ? 1 : registrationOrder.CompareTo(other.registrationOrder);

    }

    public void Dispose() {
        printProgressThrottled.Dispose();
        lock (activeUploads) {
            foreach (KeyValuePair<string, FileUploadProgress> entry in activeUploads) {
                entry.Value.PropertyChanged -= onUploadProgress;
            }
            activeUploads.Clear();
        }
        GC.SuppressFinalize(this);
    }

}