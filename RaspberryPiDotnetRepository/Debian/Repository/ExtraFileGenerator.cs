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

    Task<string> generateAddRepoScript();

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
        statistics.onFileWritten(gpgPublicKeyDestination);
        logger.LogDebug("Wrote GPG public key file Debian repository");
        return FILENAME;
    }

    public async Task<string> generateAddRepoScript() {
        const string FILENAME = "addrepo.sh";
        string       filePath = Path.Combine(options.Value.repositoryBaseDir, FILENAME);
        const string CONTENTS = """
                                #!/bin/sh

                                echo Adding repository PGP key
                                sudo wget -q https://raspbian.aldaviva.com/aldaviva.gpg.key -O /etc/apt/trusted.gpg.d/aldaviva.gpg

                                echo Adding repository
                                echo "deb https://raspbian.aldaviva.com/ $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/aldaviva.list > /dev/null

                                echo Finding available packages
                                sudo apt update

                                echo Ready to install .NET packages, for example:
                                echo "  sudo apt install dotnet-runtime-latest"
                                echo "  sudo apt install aspnetcore-runtime-latest-lts"
                                echo "  sudo apt install dotnet-sdk-8.0"
                                echo For more information, see https://github.com/Aldaviva/RaspberryPiDotnetRepository
                                """;

        await File.WriteAllTextAsync(filePath, CONTENTS, Encoding.UTF8);
        statistics.onFileWritten(filePath);
        logger.LogDebug("Wrote client-side repository installation script");
        return FILENAME;
    }

}