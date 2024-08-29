using DataSizeUnits;
using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Data;
using RaspberryPiDotnetRepository.Debian.Package;
using RaspberryPiDotnetRepository.DotnetUpstream;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Debian.Repository;

public interface Indexer {

    Task<IEnumerable<IGrouping<DebianRelease, PackageIndexFile>>> generatePackageIndex(IEnumerable<DebianPackage> packages, UpstreamReleasesSecondaryInfo upstreamInfo);

    Task<ReleaseIndexFile> generateReleaseIndex(DebianRelease debianRelease, IEnumerable<PackageIndexFile> packageIndexFiles);

}

public class IndexerImpl(StatisticsService statistics, IPGP pgp, IOptions<Options> options, ILogger<IndexerImpl> logger): Indexer {

    private const CompressionLevel PACKAGE_INDEX_COMPRESSION = CompressionLevel.Optimal;

    public async Task<IEnumerable<IGrouping<DebianRelease, PackageIndexFile>>> generatePackageIndex(IEnumerable<DebianPackage> packages, UpstreamReleasesSecondaryInfo upstreamInfo) {
        (DebianRelease debianVersion, IEnumerable<PackageIndexFile> packageIndexFiles)[] allSuites = await Task.WhenAll(groupPackagesIntoIndices(packages)
            .Select(async suitePackages =>
                (suitePackages.Key.debianVersion,
                 packageIndexFiles: await generateIndexOfPackagesInDebianReleaseAndArchitecture(suitePackages.Key.debianVersion, suitePackages.Key.architecture,
                     suitePackages.Value, upstreamInfo))));

        return allSuites
            .SelectMany(suites => suites.packageIndexFiles.Select(packageIndexFile => (suites.debianVersion, packageIndexFile)))
            .GroupBy(suitePackages => suitePackages.debianVersion, suitePackages => suitePackages.packageIndexFile);
    }

    private static IDictionary<(DebianRelease debianVersion, CpuArchitecture architecture), IList<DebianPackage>> groupPackagesIntoIndices(IEnumerable<DebianPackage> packages) {
        CpuArchitecture[] allArchitectures  = Enum.GetValues<CpuArchitecture>();
        DebianRelease[]   allDebianReleases = Enum.GetValues<DebianRelease>();

        Dictionary<(DebianRelease debianVersion, CpuArchitecture architecture), IList<DebianPackage>> groups = [];
        foreach (DebianPackage package in packages) {
            CpuArchitecture[] indexArchitectures = package.architecture.HasValue ? [package.architecture.Value] : allArchitectures;
            foreach (DebianRelease debianRelease in allDebianReleases) {
                foreach (CpuArchitecture indexArchitecture in indexArchitectures) {
                    groups.GetOrAdd((debianRelease, indexArchitecture), [], out _).Add(package);
                }
            }
        }

        return groups;
    }

    private async Task<IEnumerable<PackageIndexFile>>
        generateIndexOfPackagesInDebianReleaseAndArchitecture(DebianRelease                 debianRelease, CpuArchitecture cpuArchitecture, IEnumerable<DebianPackage> debPackages,
                                                              UpstreamReleasesSecondaryInfo upstreamInfo) {
        bool areAllPackagesInIndexUpToDateInBlobStorage = true;
        string packageFileContents = string.Join("\n\n", debPackages.Select(package => {
            areAllPackagesInIndexUpToDateInBlobStorage &= package.isUpToDateInBlobStorage;
            return $"""
                    {package.getControl(upstreamInfo).serialize().Trim()}
                    Filename: {package.filePathRelativeToRepo}
                    Size: {package.downloadSize.ConvertToUnit(Unit.Byte).Quantity:F0}
                    SHA256: {package.fileHashSha256}
                    """.ReplaceLineEndings("\n");
        }));

        string packageFileRelativeToSuite = Path.Combine("main", $"binary-{cpuArchitecture.toDebian()}", "Packages");
        string packageFileAbsolutePath    = Path.Combine(options.Value.repositoryBaseDir, "dists", debianRelease.getCodename(), packageFileRelativeToSuite);
        Directory.CreateDirectory(Path.GetDirectoryName(packageFileAbsolutePath)!);
        await using StreamWriter packageIndexStreamWriter = new(packageFileAbsolutePath, false, Encoding.UTF8);
        await packageIndexStreamWriter.WriteAsync(packageFileContents);
        logger.LogDebug("Wrote uncompressed Packages index for Debian {debian} {arch}", debianRelease.getCodename(), cpuArchitecture.toDebian());
        statistics.onFileWritten(packageFileAbsolutePath);

        await using FileStream   compressedPackageIndexFileStream   = File.Create(Path.ChangeExtension(packageFileAbsolutePath, "gz"));
        await using GZipStream   gzipStream                         = new(compressedPackageIndexFileStream, PACKAGE_INDEX_COMPRESSION);
        await using StreamWriter compressedPackageIndexStreamWriter = new(gzipStream, Encoding.UTF8);
        await compressedPackageIndexStreamWriter.WriteAsync(packageFileContents);
        logger.LogDebug("Wrote compressed Packages.gz index for Debian {debian} {arch}", debianRelease.getCodename(), cpuArchitecture.toDebian());
        statistics.onFileWritten(packageFileAbsolutePath);

        logger.LogInformation("Generated Packages index files for Debian {debian} {arch}", debianRelease.getCodename(), cpuArchitecture.toDebian());

        return [
            new PackageIndexFile(debianRelease, cpuArchitecture, true, areAllPackagesInIndexUpToDateInBlobStorage),
            new PackageIndexFile(debianRelease, cpuArchitecture, false, areAllPackagesInIndexUpToDateInBlobStorage)
        ];
    }

    public async Task<ReleaseIndexFile> generateReleaseIndex(DebianRelease debianRelease, IEnumerable<PackageIndexFile> packageIndexFiles) {
        string repositoryBaseDir                            = options.Value.repositoryBaseDir;
        bool   areAllReleaseIndexFilesUpToDateInBlobStorage = true;

        string indexSha256Hashes = string.Join('\n', await Task.WhenAll(packageIndexFiles.Select(async packageIndexFile => {
            await using FileStream fileStream = File.OpenRead(Path.Combine(repositoryBaseDir, packageIndexFile.filePathRelativeToRepo));

            string sha256Hash = Convert.ToHexString(await SHA256.HashDataAsync(fileStream)).ToLowerInvariant();
            long   fileSize   = fileStream.Length;
            areAllReleaseIndexFilesUpToDateInBlobStorage &= packageIndexFile.isUpToDateInBlobStorage;
            return $" {sha256Hash} {fileSize:D} {Paths.Dos2UnixSlashes(packageIndexFile.filePathRelativeToSuite)}";
        })));

        string releaseFileCleartext =
            $"""
                 Origin: Ben Hutchison
                 Label: .NET for Raspberry Pi OS
                 Codename: {debianRelease.getCodename()}
                 Suite: {debianRelease.getSuiteName()}
                 Architectures: {string.Join(' ', Enum.GetValues<CpuArchitecture>().Select(c => c.toDebian()))}
                 Components: main
                 Description: ARM packages of .NET Runtimes and SDKs for Raspberry Pi OS
                 Date: {DateTime.UtcNow:R}
                 SHA256:
                 {indexSha256Hashes}
                 """.ReplaceLineEndings("\n");

        ReleaseIndexFile releaseIndexFile = new(debianRelease, areAllReleaseIndexFilesUpToDateInBlobStorage);

        string filePath = Path.Combine(repositoryBaseDir, releaseIndexFile.releaseFilePathRelativeToRepo);
        await File.WriteAllTextAsync(filePath, releaseFileCleartext, Encoding.UTF8); // Bom.Squad has already defused this UTF-8 BOM, or else apt will get its limbs blown off
        logger.LogDebug("Wrote unsigned Release meta-index of package indices for Debian {debian}", debianRelease.getCodename());
        statistics.onFileWritten(filePath);

        // gpg --sign --detach-sign --armor
        filePath = Path.Combine(repositoryBaseDir, releaseIndexFile.releaseGpgFilePathRelativeToRepo);
        await File.WriteAllTextAsync(filePath, await pgp.DetachedSignAsync(releaseFileCleartext), Encoding.UTF8);
        logger.LogDebug("Wrote Release.gpg signature of Release meta-index of package indices for Debian {debian}", debianRelease.getCodename());
        statistics.onFileWritten(filePath);

        // gpg --sign --clearsign --armor
        filePath = Path.Combine(repositoryBaseDir, releaseIndexFile.inreleaseFilePathRelativeToRepo);
        await File.WriteAllTextAsync(filePath, await pgp.ClearSignAsync(releaseFileCleartext), Encoding.UTF8);
        logger.LogDebug("Wrote signed InRelease meta-index of package indices for Debian {debian}", debianRelease.getCodename());
        statistics.onFileWritten(filePath);

        logger.LogInformation("Generated Release index files for Debian {debian}", debianRelease.getCodename());

        return releaseIndexFile;
    }

}