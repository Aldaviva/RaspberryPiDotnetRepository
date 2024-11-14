using System.Globalization;
using ThrottleDebounce;

namespace RaspberryPiDotnetRepository.Azure;

public class UploadProgress: IProgress<long>, IDisposable {

    private const string PERCENTAGE_FORMAT = "P0";

    private static readonly CultureInfo CULTURE             = CultureInfo.CurrentCulture;
    private static readonly string      ZERO_PERCENT        = 0.0.ToString(PERCENTAGE_FORMAT, CULTURE);
    private static readonly string      ONE_HUNDRED_PERCENT = 1.0.ToString(PERCENTAGE_FORMAT, CULTURE);

    private readonly string                    destinationPath;
    private readonly long                      totalSize;
    private readonly ILogger<UploadProgress>   logger;
    private readonly RateLimitedAction<string> logProgressThrottled;

    private string? previousPercentage;

    public UploadProgress(string destinationPath, long totalSize, ILogger<UploadProgress> logger) {
        this.destinationPath = destinationPath;
        this.totalSize       = totalSize;
        this.logger          = logger;
        logProgressThrottled = Throttler.Throttle((Action<string>) logProgress, TimeSpan.FromSeconds(3), leading: false);
    }

    public void Report(long uploadedBytes) {
        string percentage = ((double) uploadedBytes / totalSize).ToString(PERCENTAGE_FORMAT, CULTURE);
        if (percentage != ZERO_PERCENT && percentage != ONE_HUNDRED_PERCENT && percentage != previousPercentage) {
            logProgressThrottled.Invoke(percentage);
            previousPercentage = percentage;
        }
    }

    private void logProgress(string percentage) {
        logger.LogDebug("Uploading to {dest}: {percentage,3}", destinationPath, percentage);
    }

    public void Dispose() {
        logProgressThrottled.Dispose();
        GC.SuppressFinalize(this);
    }

}