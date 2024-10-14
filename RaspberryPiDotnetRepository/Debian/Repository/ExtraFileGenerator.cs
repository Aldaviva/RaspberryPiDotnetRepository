using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Data;
using System.Text;
using System.Text.Json;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Debian.Repository;

public interface ExtraFileGenerator {

    Task<IEnumerable<UploadableFile>> generateReadmeBadges(DotnetRelease latestMinorRelease);

    Task<string> generateReadme();

    string copyGpgPublicKey();

}

public class ExtraFileGeneratorImpl(IOptions<Options> options, StatisticsService statistics, ILogger<ExtraFileGeneratorImpl> logger): ExtraFileGenerator {

    public async Task<IEnumerable<UploadableFile>> generateReadmeBadges(DotnetRelease latestMinorRelease) {
        const string BADGE_DIR = "badges";
        Directory.CreateDirectory(Path.Combine(options.Value.repositoryBaseDir, BADGE_DIR));
        IList<UploadableFile> files = new List<UploadableFile>(2);

        var dotnetBadge = new { latestVersion = latestMinorRelease.runtimeVersion.ToString(3) };

        UploadableFile dotnetBadgeFile = new(Path.Combine(BADGE_DIR, "dotnet.json"));
        files.Add(dotnetBadgeFile);
        await using FileStream dotnetBadgeFileStream = File.Create(Path.Combine(options.Value.repositoryBaseDir, dotnetBadgeFile.filePathRelativeToRepo));
        await JsonSerializer.SerializeAsync(dotnetBadgeFileStream, dotnetBadge);
        statistics.onFileWritten(dotnetBadgeFileStream.Name);
        logger.LogDebug("Wrote badge JSON file that shows the latest .NET version");

        DebianRelease latestDebianVersion = Enum.GetValues<DebianRelease>().Max();

        var debianBadge = new { latestVersion = $"{latestDebianVersion.getCodename()} ({latestDebianVersion.getMajorVersion():D})" };

        UploadableFile debianBadgeFile = new(Path.Combine(BADGE_DIR, "raspbian.json"));
        files.Add(debianBadgeFile);
        await using FileStream debianBadgeFileStream = File.Create(Path.Combine(options.Value.repositoryBaseDir, debianBadgeFile.filePathRelativeToRepo));
        await JsonSerializer.SerializeAsync(debianBadgeFileStream, debianBadge);
        statistics.onFileWritten(debianBadgeFileStream.Name);
        logger.LogDebug("Wrote badge JSON file that shows the latest Debian version");

        return files;
    }

    public async Task<string> generateReadme() {
        const string FILENAME   = "readme";
        string       readmePath = Path.Combine(options.Value.repositoryBaseDir, FILENAME);
        await File.WriteAllTextAsync(readmePath, "https://github.com/Aldaviva/RaspberryPiDotnetRepository", Encoding.UTF8);
        statistics.onFileWritten(readmePath);
        logger.LogDebug("Wrote readme file to Debian repository");
        return FILENAME;
    }

    public string copyGpgPublicKey() {
        const string FILENAME                = "aldaviva.gpg.key";
        string       gpgPublicKeyDestination = Path.Combine(options.Value.repositoryBaseDir, FILENAME);
        File.Copy(options.Value.gpgPublicKeyPath, gpgPublicKeyDestination, true);
        logger.LogDebug("Wrote GPG public key file Debian repository");
        return FILENAME;
    }

}