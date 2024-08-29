using DataSizeUnits;
using System.Diagnostics;

namespace RaspberryPiDotnetRepository.Debian.Package;

public interface StatisticsService {

    DataSize dataWritten { get; }
    int filesWritten { get; }

    void startTimer();
    TimeSpan stopTimer();

    void onFileWritten(DataSize fileSize);
    void onFileWritten(string   filePath);

}

public class StatisticsServiceImpl: StatisticsService {

    private readonly Stopwatch stopwatch = new();

    public DataSize dataWritten { get; private set; } = new();
    public int filesWritten { get; private set; }

    public void startTimer() {
        stopwatch.Start();
    }

    public void onFileWritten(DataSize fileSize) {
        filesWritten++;
        dataWritten += fileSize;
    }

    public void onFileWritten(string filePath) {
        onFileWritten(new FileInfo(filePath).Length);
    }

    public TimeSpan stopTimer() {
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

}