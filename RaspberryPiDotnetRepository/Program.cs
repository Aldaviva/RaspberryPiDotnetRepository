using Bom.Squad;
using DataSizeUnits;
using McMaster.Extensions.CommandLineUtils;
using Org.BouncyCastle.Bcpg;
using PgpCore;
using RaspberryPiDotnetRepository.Data;
using RaspberryPiDotnetRepository.Unfucked.PGPCore;
using RaspberryPiDotnetRepository.Unfucked.SharpCompress.Writers.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static RaspberryPiDotnetRepository.DebPackageBuilder;
using FileInfo = System.IO.FileInfo;
using PGP = RaspberryPiDotnetRepository.Unfucked.PGPCore.PGP;

namespace RaspberryPiDotnetRepository;

internal class Program {

    private const string           DEBIAN_PACKAGE_VERSION_SUFFIX     = "-0";
    private const float            LEAST_PROVIDED_HISTORICAL_RELEASE = 6.0f;
    private const CompressionLevel GZ_COMPRESSION_LEVEL              = CompressionLevel.Optimal;

    private const SharpCompress.Compressors.Deflate.CompressionLevel TAR_GZ_COMPRESSION_LEVEL = SharpCompress.Compressors.Deflate.CompressionLevel.Default;

    private static readonly Encoding UTF8 = new UTF8Encoding(false, true);

    // Found on https://github.com/dotnet/core#release-information
    private static readonly Uri DOTNET_RELEASE_INDEX = new("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json");

    private static readonly HttpClient HTTP_CLIENT = new(new SocketsHttpHandler { MaxConnectionsPerServer = Environment.ProcessorCount, AllowAutoRedirect = true })
        { Timeout = TimeSpan.FromSeconds(30) };

    private static IReadOnlyList<float> knownReleaseMinorVersions               = null!;
    private static string               leastProvidedCurrentReleaseMinorVersion = null!;
    private static string               latestLtsMinorVersion                   = null!;

    [Option("--repo-dir <PATH>", "Absolute path of 'raspbian' directory", CommandOptionType.SingleValue)]
    public string repositoryBaseDir { get; } = @".\raspbian\";

    [Option("--temp-dir <PATH>", "Absolute path in which to temporarily download .NET SDK archives", CommandOptionType.SingleValue)]
    public string tempDir { get; } = @".\temp\";

    [Option("--gpg-pub-key <PATH>", "Absolute path of GPG public key", CommandOptionType.SingleValue)]
    public string gpgPublicKeyPath { get; } = @".\dotnet-raspbian.gpg.pub.asc";

    [Option("--gpg-priv-key <PATH>", "Absolute path of GPG private key", CommandOptionType.SingleValue)]
    public string gpgPrivateKeyPath { get; } = @".\dotnet-raspbian.gpg.priv";

    [Option("--keep-temp-downloads", "Don't delete unneeded SDK .tar.gz files after generating packages", CommandOptionType.NoValue)]
    public bool keepTempDownloads { get; } = false;

    [Option("--force", "Regenerate repository even if there are no new .NET or Raspbian versions", CommandOptionType.NoValue)]
    public bool forceRegenerate { get; } = false;

    private IPGP pgp = null!;

    private string packageDir => Path.Combine(repositoryBaseDir, "packages");
    private string badgeDir => Path.Combine(repositoryBaseDir, "badges");

    public static async Task Main(string[] args) => await CommandLineApplication.ExecuteAsync<Program>(args);

    // ReSharper disable once UnusedMember.Local - called by CommandLineApplication.ExecuteAsync<T>()
    private async Task OnExecuteAsync() {
        Stopwatch overallTimer = Stopwatch.StartNew();
        BomSquad.DefuseUtf8Bom();

        pgp = new PGP(new EncryptionKeys(await File.ReadAllTextAsync(gpgPrivateKeyPath, UTF8), string.Empty)) { HashAlgorithmTag = HashAlgorithmTag.Sha256 };

        foreach (string directory in new[] { tempDir, packageDir, badgeDir }) {
            Directory.CreateDirectory(directory);
        }

        DebianRelease[]     debianReleases   = Enum.GetValues<DebianRelease>();
        CpuArchitecture[]   cpuArchitectures = Enum.GetValues<CpuArchitecture>();
        DotnetPackageType[] dotnetRuntimes   = Enum.GetValues<DotnetPackageType>();

        JsonNode dotnetMinorVersionsManifest = (await HTTP_CLIENT.GetFromJsonAsync<JsonNode>(DOTNET_RELEASE_INDEX))!;

        JsonArray releasesIndex = dotnetMinorVersionsManifest["releases-index"]!.AsArray();
        knownReleaseMinorVersions = releasesIndex.Where(release => release!["support-phase"]!.GetValue<string>() is "active" or "maintenance" or "eol")
            .Select(release => float.Parse(release!["channel-version"]!.GetValue<string>()))
            .Where(minorVersion => minorVersion >= LEAST_PROVIDED_HISTORICAL_RELEASE)
            .ToList().AsReadOnly();

        IList<JsonNode> supportedStableReleases = releasesIndex.Where(release => release!["support-phase"]!.GetValue<string>() is "active" or "maintenance").Compact().ToList();

        Stream repoVersionStream;
        string repoVersionFilename = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "version.json");
        VersionKey upstreamVersionKey = new(supportedStableReleases.Select(release => release["latest-release"]!.GetValue<string>()).ToImmutableSortedSet(),
            Enum.GetValuesAsUnderlyingType<DebianRelease>().Cast<int>().ToImmutableSortedSet());
        if (!forceRegenerate) {
            try {
                Console.WriteLine($"Comparing current repo version with latest upstream version to determine if the repository needs to be regenerated now ({repoVersionFilename})");
                await using (repoVersionStream = File.OpenRead(repoVersionFilename)) {
                    VersionKey? repoVersionKey = JsonSerializer.Deserialize<VersionKey>(repoVersionStream);
                    if (upstreamVersionKey.Equals(repoVersionKey)) {
                        Console.WriteLine("No .NET or Raspbian updates since last repository regeneration, exiting leaving repository unchanged.");
                        return;
                    }
                }
            } catch (FileNotFoundException) {
                // if file does not exist, continue generating repo
            }
        }

        File.Copy(gpgPublicKeyPath, Path.Combine(repositoryBaseDir, "aldaviva.gpg.key"), true);

        IList<DotnetRelease> dotnetReleases = (await Task.WhenAll(supportedStableReleases
            .Select(async minorVersion => {
                string         minorVersionNumber  = minorVersion["channel-version"]!.GetValue<string>();
                Uri            patchVersionUrl     = new(minorVersion["releases.json"]!.GetValue<string>());
                bool           isSupportedLongTerm = minorVersion["release-type"]!.GetValue<string>() == "lts";
                JsonNode       patchVersions       = (await HTTP_CLIENT.GetFromJsonAsync<JsonNode>(patchVersionUrl))!;
                DotnetRelease? dotnetRelease       = null;

                if (patchVersions["releases"]!.AsArray().FirstOrDefault(isStableVersion) is { } latestPatchVersion1) {
                    string patchVersionNumber = latestPatchVersion1["release-version"]!.GetValue<string>();
                    dotnetRelease = new DotnetRelease(minorVersionNumber, patchVersionNumber, isSupportedLongTerm);

                    await Task.WhenAll(cpuArchitectures.Select(async cpuArchitecture => {
                        string   rid                   = "linux-" + cpuArchitecture.toRuntimeIdentifierSuffix();
                        JsonNode architectureObject    = latestPatchVersion1["sdk"]!["files"]!.AsArray().First(file => file?["rid"]?.GetValue<string>() == rid)!;
                        Uri      sdkDownloadUrl        = new(architectureObject["url"]!.GetValue<string>());
                        byte[]   expectedSdkSha512Hash = Convert.FromHexString(architectureObject["hash"]!.GetValue<string>());

                        Stopwatch downloadTimer         = new();
                        string    downloadedSdkFilename = Path.Combine(tempDir, Path.GetFileName(sdkDownloadUrl.LocalPath));
                        dotnetRelease.downloadedSdkArchiveFilePaths[cpuArchitecture] = downloadedSdkFilename;
                        await using FileStream fileDownloadStream = File.Open(downloadedSdkFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                        if (fileDownloadStream.Length == 0) {
                            Console.WriteLine($"Downloading .NET SDK {patchVersionNumber} for {cpuArchitecture}...");
                            downloadTimer.Start();
                            await using Stream downloadStream = await HTTP_CLIENT.GetStreamAsync(sdkDownloadUrl);
                            await downloadStream.CopyToAsync(fileDownloadStream);
                            await fileDownloadStream.FlushAsync();
                            downloadTimer.Stop();

                            DataSize fileSizeOnDisk         = new(fileDownloadStream.Length);
                            DataSize downloadSpeedPerSecond = fileSizeOnDisk / downloadTimer.Elapsed.TotalSeconds;
                            Console.WriteLine($@"Downloaded .NET SDK {patchVersionNumber} for {cpuArchitecture} in {downloadTimer.Elapsed:m\m\ ss\s}"
                                + $" ({fileSizeOnDisk.ToString(1, true)} at {downloadSpeedPerSecond.ToString(1, true)}/s)");
                        }

                        fileDownloadStream.Position = 0;
                        Console.WriteLine($"Verifying .NET SDK {patchVersionNumber} for {cpuArchitecture} file hash...");
                        byte[] actualSdkSha512Hash = await SHA512.HashDataAsync(fileDownloadStream);
                        if (actualSdkSha512Hash.SequenceEqual(expectedSdkSha512Hash)) {
                            Console.WriteLine($"Successfully verified .NET SDK {patchVersionNumber} for {cpuArchitecture} file hash.");
                        } else {
                            Console.WriteLine($"Failed to verify .NET SDK {patchVersionNumber} for {cpuArchitecture}!");
                            Console.WriteLine($"Expected SHA-512 hash of {sdkDownloadUrl}: {Convert.ToHexString(expectedSdkSha512Hash)}");
                            Console.WriteLine($"Actual SHA-512 hash of {downloadedSdkFilename}: {Convert.ToHexString(actualSdkSha512Hash)}");
                            throw new ApplicationException($"Verification failed after downloading {sdkDownloadUrl}");
                        }
                    }));
                }

                return dotnetRelease;
            }))).Compact().OrderByDescending(release => release.minorVersionNumeric).ToList();

        leastProvidedCurrentReleaseMinorVersion = dotnetReleases.Last().minorVersion;

        DotnetRelease latestMinorRelease = dotnetReleases.First();
        latestMinorRelease.isLatestMinorVersion = true;

        DotnetRelease latestLongTerm = dotnetReleases.First(release => release.isSupportedLongTerm);
        latestLongTerm.isLatestOfSupportTerm = true;
        latestLtsMinorVersion                = latestLongTerm.minorVersion;

        if (dotnetReleases.FirstOrDefault(release => !release.isSupportedLongTerm) is { } latestShortTerm) {
            latestShortTerm.isLatestOfSupportTerm = true;
        }

        IList<PackageRequest> packageRequests = [];
        foreach (DebianRelease debianRelease in debianReleases) {
            foreach (CpuArchitecture cpuArchitecture in cpuArchitectures) {
                foreach (DotnetRelease dotnetRelease in dotnetReleases) {
                    foreach (DotnetPackageType dotnetRuntime in dotnetRuntimes) {
                        packageRequests.Add(new DotnetPackageRequest(dotnetRelease, dotnetRuntime, cpuArchitecture, debianRelease, dotnetRelease.downloadedSdkArchiveFilePaths[cpuArchitecture]));
                    }
                }
            }

            foreach (DotnetPackageType dotnetRuntime in dotnetRuntimes.Except([DotnetPackageType.CLI])) {
                packageRequests.Add(new MetaPackageRequest(dotnetRuntime, debianRelease, true, latestLongTerm.minorVersion));
                packageRequests.Add(new MetaPackageRequest(dotnetRuntime, debianRelease, false, latestMinorRelease.minorVersion));
            }
        }

        Console.WriteLine("Generating packages");
        DebianPackage[] generatedPackages = await Task.WhenAll(packageRequests.AsParallel().Select(generateDebPackage));
        Console.WriteLine("Generated packages");

        Console.WriteLine("Generating package index files");
        IEnumerable<IGrouping<DebianRelease, string>> packageIndexFiles = (await Task.WhenAll(groupPackagesIntoIndices(generatedPackages)
                .Select(async suitePackages =>
                    (suitePackages.Key.debianVersion, packageIndexFiles: await generatePackageIndexFiles(suitePackages.Key.debianVersion, suitePackages.Key.architecture, suitePackages.Value)))))
            .SelectMany(suitePackages => suitePackages.packageIndexFiles.Select(packageIndexFile => (suitePackages.debianVersion, packageIndexFile)))
            .GroupBy(suitePackages => suitePackages.debianVersion, suitePackages => suitePackages.packageIndexFile);

        Console.WriteLine("Generated package index files");

        Console.WriteLine("Generating release index files");
        await Task.WhenAll(packageIndexFiles.Select(releaseFiles => generateReleaseIndexFiles(releaseFiles.Key, releaseFiles)));
        Console.WriteLine("Generated release index files");

        await generateBadges(latestMinorRelease, debianReleases);

        await using (repoVersionStream = File.Create(repoVersionFilename)) {
            await JsonSerializer.SerializeAsync(repoVersionStream, upstreamVersionKey);
            Console.WriteLine("Generated version key file");
        }

        if (!keepTempDownloads) {
            foreach (DotnetRelease release in dotnetReleases) {
                foreach ((CpuArchitecture _, string? sdkArchivePath) in release.downloadedSdkArchiveFilePaths) {
                    File.Delete(sdkArchivePath);
                    Console.WriteLine($"Deleted {sdkArchivePath}");
                }
            }
        }

        overallTimer.Stop();
        Console.WriteLine($"Finished in {overallTimer.Elapsed.TotalSeconds:N0} seconds at Default/Optimal compression.");
        return;

        static IDictionary<(DebianRelease debianVersion, CpuArchitecture architecture), IList<DebianPackage>> groupPackagesIntoIndices(IEnumerable<DebianPackage> packages) {
            Dictionary<(DebianRelease debianVersion, CpuArchitecture architecture), IList<DebianPackage>> groups = [];
            foreach (DebianPackage package in packages) {
                CpuArchitecture[] indexArchitectures = package.architecture.HasValue ? [package.architecture.Value] : Enum.GetValues<CpuArchitecture>();
                foreach (CpuArchitecture indexArchitecture in indexArchitectures) {
                    if (!groups.TryGetValue((package.debianVersion, indexArchitecture), out IList<DebianPackage>? packagesInIndex)) {
                        packagesInIndex = [];
                        groups.Add((package.debianVersion, indexArchitecture), packagesInIndex);
                    }

                    packagesInIndex.Add(package);
                }
            }

            return groups;
        }
    }

    private async Task generateBadges(DotnetRelease latestMinorRelease, IEnumerable<DebianRelease> debianReleases) {
        var dotnetBadge = new { latestVersion = latestMinorRelease.patchVersion };

        await using FileStream dotnetBadgeFile = File.Create(Path.Combine(badgeDir, "dotnet.json"));
        await JsonSerializer.SerializeAsync(dotnetBadgeFile, dotnetBadge);

        DebianRelease latestDebianVersion = debianReleases.Max();
        var           debianBadge         = new { latestVersion = $"{latestDebianVersion.getCodename()} ({latestDebianVersion.getMajorVersion():D})" };

        await using FileStream debianBadgeFile = File.Create(Path.Combine(badgeDir, "raspbian.json"));
        await JsonSerializer.SerializeAsync(debianBadgeFile, debianBadge);
    }

    private async Task generateReleaseIndexFiles(DebianRelease debianRelease, IEnumerable<string> packageIndexFilesRelativeToSuite) {
        Console.WriteLine($"Generating Release index file for Debian {debianRelease}");
        string suiteDirectory = Path.Combine(repositoryBaseDir, "dists", debianRelease.getCodename());

        string indexSha256Hashes = string.Join('\n', await Task.WhenAll(packageIndexFilesRelativeToSuite.Select(async filePath => {
            await using FileStream fileStream = File.OpenRead(Path.Combine(suiteDirectory, filePath));

            string sha256Hash = Convert.ToHexString(await SHA256.HashDataAsync(fileStream)).ToLowerInvariant();
            long   fileSize   = fileStream.Length;
            return $" {sha256Hash} {fileSize:D} {dos2UnixPathSeparators(filePath)}";
        })));

        string releaseFileCleartext =
            $"""
                 Origin: Ben Hutchison
                 Label: .NET for Raspberry Pi OS
                 Codename: {debianRelease.getCodename()}
                 Architectures: {string.Join(' ', Enum.GetValues<CpuArchitecture>().Select(c => c.toDebian()))}
                 Components: main
                 Description: ARM packages of .NET Runtimes and SDKs for Raspberry Pi OS
                 Date: {DateTime.UtcNow:R}
                 SHA256:
                 {indexSha256Hashes}
                 """.ReplaceLineEndings("\n");

        await File.WriteAllTextAsync(Path.Combine(suiteDirectory, "Release"), releaseFileCleartext, UTF8);
        Console.WriteLine($"Wrote unsigned Release meta-index of package indices for Raspbian {debianRelease.getCodename()}");

        // gpg --sign --detach-sign --armor
        await File.WriteAllTextAsync(Path.Combine(suiteDirectory, "Release.gpg"), await pgp.DetachedSignAsync(releaseFileCleartext), UTF8);
        Console.WriteLine($"Wrote Release.gpg signature of Release meta-index of package indices for Raspbian {debianRelease.getCodename()}");

        // gpg --sign --clearsign --armor
        await File.WriteAllTextAsync(Path.Combine(suiteDirectory, "InRelease"), await pgp.ClearSignAsync(releaseFileCleartext), UTF8);
        Console.WriteLine($"Wrote signed InRelease meta-index of package indices for Raspbian {debianRelease.getCodename()}");

        Console.WriteLine($"Generated Release index file for Debian {debianRelease}");
    }

    private async Task<IEnumerable<string>> generatePackageIndexFiles(DebianRelease debianRelease, CpuArchitecture cpuArchitecture, IEnumerable<DebianPackage> debPackages) {
        Console.WriteLine($"Generating Packages index file for Debian {debianRelease} {cpuArchitecture}");
        string packageFileContents = string.Join("\n\n", await Task.WhenAll(debPackages.Select(async package => {
            await using FileStream debPackageStream = File.OpenRead(package.absoluteFilename);

            string fileSha256Hash = Convert.ToHexString(await SHA256.HashDataAsync(debPackageStream)).ToLowerInvariant();
            return $"""
                    {package.controlMetadata.Trim()}
                    Filename: packages/{debianRelease.getCodename()}/{Path.GetFileName(package.absoluteFilename)}
                    Size: {new FileInfo(package.absoluteFilename).Length:D}
                    SHA256: {fileSha256Hash}
                    """.ReplaceLineEndings("\n");
        })));

        string packageFileRelativeToSuite = Path.Combine("main", $"binary-{cpuArchitecture.toDebian()}", "Packages");
        string packageFileAbsolutePath    = Path.Combine(repositoryBaseDir, "dists", debianRelease.getCodename(), packageFileRelativeToSuite);
        Directory.CreateDirectory(Path.GetDirectoryName(packageFileAbsolutePath)!);
        await using StreamWriter packageIndexStreamWriter = new(packageFileAbsolutePath, false, UTF8);
        await packageIndexStreamWriter.WriteAsync(packageFileContents);
        Console.WriteLine($"Wrote uncompressed Packages index for Raspbian {debianRelease.getCodename()} {cpuArchitecture.toDebian()}");

        string compressedPackageFileRelativeToSuite = Path.ChangeExtension(packageFileRelativeToSuite, "gz");
        string compressedPackageFileAbsolutePath    = Path.ChangeExtension(packageFileAbsolutePath, "gz");

        await using FileStream   compressedPackageIndexFileStream   = File.Create(compressedPackageFileAbsolutePath);
        await using GZipStream   gzipStream                         = new(compressedPackageIndexFileStream, GZ_COMPRESSION_LEVEL);
        await using StreamWriter compressedPackageIndexStreamWriter = new(gzipStream, UTF8);
        await compressedPackageIndexStreamWriter.WriteAsync(packageFileContents);
        Console.WriteLine($"Wrote compressed Packages.gz index for Raspbian {debianRelease.getCodename()} {cpuArchitecture.toDebian()}");

        Console.WriteLine($"Generated Packages index file for Debian {debianRelease} {cpuArchitecture}");
        return [packageFileRelativeToSuite, compressedPackageFileRelativeToSuite];
    }

    private async Task<DebianPackage> generateDebPackage(PackageRequest packageToGenerate) => packageToGenerate switch {
        DotnetPackageRequest r => await generateDebPackage(r),
        MetaPackageRequest r => await generateDebPackage(r),
        _ => throw new ArgumentOutOfRangeException(nameof(packageToGenerate), packageToGenerate, $"Update {nameof(generateDebPackage)} to handle {packageToGenerate.GetType().Name}")
    };

    private async Task<DebianPackage> generateDebPackage(DotnetPackageRequest packageToGenerate) {
        Console.WriteLine($"Generating package for Debian {packageToGenerate.debian} {packageToGenerate.architecture} {packageToGenerate.packageType} {packageToGenerate.dotnetRelease.minorVersion}");
        string   packageName                 = packageToGenerate.packageType.getPackageName();
        bool     isCliPackage                = packageToGenerate.packageType == DotnetPackageType.CLI;
        string   packageNameWithMinorVersion = isCliPackage ? packageName : $"{packageName}-{packageToGenerate.dotnetRelease.minorVersion}";
        DataSize installedSize               = new();
        ISet<string> existingDirectories = new HashSet<string> {
            "./usr/share",
            "./usr/bin"
        };

        await using DebPackageBuilder debPackageBuilder = new() { gzipCompressionLevel = TAR_GZ_COMPRESSION_LEVEL };

        await using (FileStream sdkArchiveStream = File.OpenRead(packageToGenerate.sdkArchivePath))
        using (IReader downloadReader = ReaderFactory.Open(sdkArchiveStream))
        using (UnfuckedTarWriter dataArchiveWriter = debPackageBuilder.data) {
            while (downloadReader.MoveToNextEntry()) {
                IEntry entry = downloadReader.Entry;
                if (entry.IsDirectory) continue;

                string   sourcePath      = entry.Key[2..]; // remove leading "./"
                string   destinationPath = $"./usr/share/dotnet/{sourcePath}";
                int?     fileMode        = null;
                DateTime lastModified    = entry.LastModifiedTime ?? entry.CreatedTime ?? entry.ArchivedTime ?? DateTime.Now;

                switch (packageToGenerate.packageType) {
                    case DotnetPackageType.CLI:
                        if (sourcePath == "dotnet") {
                            fileMode = o(755);
                        }

                        break;
                    case DotnetPackageType.RUNTIME:
                        if (sourcePath.StartsWith("shared/Microsoft.NETCore.App/")) {
                            fileMode = Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) == "createdump" ? o(755) : o(644);
                        } else if (sourcePath is "ThirdPartyNotices.txt" or "LICENSE.txt") {
                            fileMode = o(644);
                        } else if (sourcePath.StartsWith("host/fxr")) {
                            fileMode = o(755);
                        }

                        break;
                    case DotnetPackageType.ASPNETCORE_RUNTIME:
                        if (sourcePath.StartsWith("shared/Microsoft.AspNetCore.App/")) {
                            fileMode = o(644);
                        }

                        break;
                    case DotnetPackageType.SDK:
                        if (sourcePath.StartsWith("packs/") || sourcePath.StartsWith("sdk") || sourcePath.StartsWith("templates/")) {
                            fileMode = Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) is "apphost" or "singlefilehost" ? o(744) : o(644);
                        }

                        break;
                }

                if (fileMode != null) {
                    IList<string> directoriesToAdd = [];
                    for (string destinationDirectory = dos2UnixPathSeparators(Path.GetDirectoryName(destinationPath)!);
                         existingDirectories.Add(destinationDirectory);
                         destinationDirectory = dos2UnixPathSeparators(Path.GetDirectoryName(destinationDirectory)!)) {

                        directoriesToAdd.Add(destinationDirectory);
                    }

                    foreach (string directoryToAdd in directoriesToAdd.Reverse()) {
                        dataArchiveWriter.WriteDirectory(directoryToAdd, null, o(755));
                        installedSize += 1024;
                    }

                    // It is very important to dispose each EntryStream, otherwise the IReader will randomly throw an IncompleteArchiveException on a later file in the archive
                    await using EntryStream downloadedInnerFileStream = downloadReader.OpenEntryStream();
                    dataArchiveWriter.WriteFile(destinationPath, downloadedInnerFileStream, lastModified, entry.Size, fileMode);
                    installedSize += entry.Size;

                    if (destinationPath == "./usr/share/dotnet/dotnet") {
                        // can't figure out how to make absolute symlinks in deb packages, so making it relative
                        dataArchiveWriter.WriteSymLink("./usr/bin/dotnet", "../share/dotnet/dotnet", lastModified);
                        installedSize += 1024;
                    }
                }
            }
        }

        IList<string> dependencies = getDependencies(packageToGenerate.packageType, packageToGenerate.dotnetRelease, packageToGenerate.debian).ToList();
        string        depends      = dependencies.Any() ? $"Depends: {string.Join(", ", dependencies)}" : string.Empty;
        string        suggests     = isCliPackage ? $"Suggests: {DotnetPackageType.RUNTIME.getPackageName()}-{leastProvidedCurrentReleaseMinorVersion}-or-greater" : string.Empty;

        IList<string> providing = !isCliPackage ? knownReleaseMinorVersions.Where(minorVersion => minorVersion <= packageToGenerate.dotnetRelease.minorVersionNumeric)
            .Select(minorVersion => $"{packageName}-{minorVersion:F1}-or-greater").ToList() : [];
        string provides = providing.Any() ? $"Provides: {string.Join(", ", providing)}" : string.Empty;

        debPackageBuilder.control =
            $"""
             Package: {packageNameWithMinorVersion}
             Version: {packageToGenerate.dotnetRelease.patchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX}
             Architecture: {packageToGenerate.architecture.toDebian()}
             Maintainer: Ben Hutchison <ben@aldaviva.com>
             Installed-Size: {Math.Round(installedSize.ConvertToUnit(Unit.Kilobyte).Quantity):F0}
             {depends}
             {suggests}
             {provides}
             Section: devel
             Priority: optional
             Homepage: https://dot.net
             Description: {getPackageDescription(packageToGenerate.packageType, packageToGenerate.dotnetRelease.minorVersion)}
             """;

        string debFileAbsolutePath = Path.Combine(packageDir, packageToGenerate.debian.getCodename(),
            $"{packageName}-{packageToGenerate.dotnetRelease.patchVersion}-{packageToGenerate.architecture.toDebian()}.deb");
        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (Stream debStream = File.Create(debFileAbsolutePath)) {
            await debPackageBuilder.build(debStream);
        }

        Console.WriteLine($"Wrote package {debFileAbsolutePath}");
        Console.WriteLine($"Generated package for Debian {packageToGenerate.debian} {packageToGenerate.architecture} {packageToGenerate.packageType} {packageToGenerate.dotnetRelease.minorVersion}");
        return new DebianPackage(packageNameWithMinorVersion, packageToGenerate.dotnetRelease.patchVersion, packageToGenerate.debian, packageToGenerate.architecture, packageToGenerate.packageType,
            debPackageBuilder.control, debFileAbsolutePath);
    }

    private async Task<DebianPackage> generateDebPackage(MetaPackageRequest packageToGenerate) {
        await using DebPackageBuilder debPackageBuilder = new();

        string packageName    = $"{packageToGenerate.packageType.getPackageName()}-latest{(packageToGenerate.mustBeSupportedLongTerm ? "-lts" : "")}";
        string packageVersion = packageToGenerate.concreteMinorVersion + DEBIAN_PACKAGE_VERSION_SUFFIX;

        debPackageBuilder.control =
            $"""
             Package: {packageName}
             Version: {packageVersion}
             Architecture: all
             Maintainer: Ben Hutchison <ben@aldaviva.com>
             Installed-Size: 0
             Depends: {packageToGenerate.packageType.getPackageName()}-{packageToGenerate.concreteMinorVersion}
             Section: devel
             Priority: optional
             Homepage: https://dot.net
             Description: {getPackageDescription(packageToGenerate.packageType, packageToGenerate.concreteMinorVersion, true, packageToGenerate.mustBeSupportedLongTerm)}
             """;

        string debFileAbsolutePath = Path.Combine(packageDir, packageToGenerate.debian.getCodename(),
            $"{packageName}-{packageVersion}.deb");
        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (Stream debStream = File.Create(debFileAbsolutePath)) {
            await debPackageBuilder.build(debStream);
        }

        Console.WriteLine($"Wrote package {debFileAbsolutePath}");
        Console.WriteLine($"Generated package for Debian {packageToGenerate.debian} all {packageToGenerate.packageType} {packageToGenerate.concreteMinorVersion}");
        return new DebianPackage(packageName, packageVersion, packageToGenerate.debian, null, packageToGenerate.packageType, debPackageBuilder.control, debFileAbsolutePath);
    }

    // Subsequent lines in each of these strings will be prefixed with a leading space in the package control file because that indentation is how multi-line descriptions work.
    //
    // Except for the summary first line of every description string, each line that starts with ".NET" actually starts with U+2024 ONE DOT LEADER ("․") instead of U+002E FULL STOP (".", a normal period).
    // This is because lines that start with periods have special meaning in Debian packages. Specifically, a period on a line of its own is interpreted as a blank line to differentiate it from a new package record, but a line that starts with a period and has more text after it (like .NET) is illegal and should not be used. Aptitude renders such lines as blank lines.
    //
    // https://www.debian.org/doc/debian-policy/ch-controlfields.html#description
    private static string getPackageDescription(DotnetPackageType packageType, string minorVersion, bool isMetaPackage = false, bool isMetaPackageSupportedLongTerm = false) => (packageType switch {
        _ when isMetaPackage && isMetaPackageSupportedLongTerm =>
            $"""
             {packageType.getFriendlyName()} (latest LTS)
             This is a dependency metapackage that installs the current latest Long Term Support (LTS) version of {packageType.getFriendlyName()}. Does not include preview or release candidate versions.

             Install this package if you want to always have the greatest LTS {packageType.getFriendlyName()} version installed. This will perform major and minor version upgrades — for example, if {packageType.getPackageName()}-6.0 were already installed, `apt upgrade` would install {packageType.getPackageName()}-{minorVersion}.

             If you instead want to always stay on the latest version, even if that means sometimes using an STS (Standard Term Support) release, then install {packageType.getPackageName()}-latest.

             If you instead want to always stay on a specific minor version, then install a numbered release, such as {packageType.getPackageName()}-{minorVersion}.

             If you are a developer who wants your application package to depend upon a certain minimum version of {packageType.getPackageName()}, it is suggested that you add a dependency on `{packageType.getPackageName()}-latest | {packageType.getPackageName()}-{minorVersion}-or-greater` (replace {minorVersion} with the minimum .NET version your app targets).
             """,
        _ when isMetaPackage && !isMetaPackageSupportedLongTerm =>
            $"""
             {packageType.getFriendlyName()} (latest LTS or STS)
             This is a dependency metapackage that installs the current latest version of {packageType.getFriendlyName()}, whether that is LTS (Long Term Support) or STS (Standard Term Support), whichever version number is greater. Does not include preview or release candidate versions.

             Install this package if you want to always have the highest stable .NET version installed, even if that version is not LTS. This will perform major and minor version upgrades — for example, if {packageType.getPackageName()}-{latestLtsMinorVersion} were already installed, `apt upgrade` would install {packageType.getPackageName()}-{Math.Floor(float.Parse(latestLtsMinorVersion)) + 1:F1} when it was released.

             If you instead want to always stay on the latest LTS release and avoid STS, then install {packageType.getPackageName()}-latest-lts.

             If you instead want to always stay on a specific minor version, then install a numbered release, such as {packageType.getPackageName()}-{minorVersion}.

             If you are a developer who wants your application package to depend upon a certain minimum version of {packageType.getPackageName()}, it is suggested that you add a dependency on `{packageType.getPackageName()}-latest | {packageType.getPackageName()}-{minorVersion}-or-greater` (replace {minorVersion} with the minimum .NET version your app targets).
             """,
        DotnetPackageType.CLI =>
            $"""
             .NET CLI tool (without runtime)
             This package installs the '/usr/bin/dotnet' command-line interface, which is part of all .NET installations.

             This package does not install any .NET runtimes or SDKs by itself, so .NET applications won't run with only this package. This is just a common dependency used by the other .NET packages.

             To actually run or build .NET applications, you must also install one of the .NET runtime or SDK packages, such as {DotnetPackageType.RUNTIME.getPackageName()}-{minorVersion}, {DotnetPackageType.ASPNETCORE_RUNTIME.getPackageName()}-{minorVersion}, or {DotnetPackageType.SDK.getPackageName()}-{minorVersion}.
             """,
        DotnetPackageType.RUNTIME =>
            """
            .NET CLI tools and runtime
            ․NET is a fast, lightweight and modular platform for creating cross platform applications that work on GNU/Linux, macOS, and Windows.

            It particularly focuses on creating console applications, web applications, and micro-services.

            ․NET contains a runtime conforming to .NET Standards, a set of framework libraries, an SDK containing compilers, and a 'dotnet' CLI application to drive everything.
            """,
        DotnetPackageType.ASPNETCORE_RUNTIME =>
            """
            ASP.NET Core runtime
            The ASP.NET Core runtime contains everything needed to run .NET web applications. It includes a high performance Virtual Machine as well as the framework libraries used by .NET applications.

            ASP.NET Core is a fast, lightweight, and modular platform for creating cross platform applications that work on GNU/Linux, macOS, and Windows.

            It particularly focuses on creating console applications, web applications, and micro-services.
            """,
        DotnetPackageType.SDK =>
            $"""
             .NET {minorVersion} Software Development Kit
             The .NET SDK is a collection of command-line applications to create, build, publish, and run .NET applications.

             ․NET is a fast, lightweight, and modular platform for creating cross platform applications that work on GNU/Linux, macOS and Windows.

             It particularly focuses on creating console applications, web applications, and micro-services.
             """,
        _ => throw new ArgumentOutOfRangeException(nameof(packageType), packageType, "Unhandled runtime")
    }).Trim().ReplaceLineEndings("\n").Replace("\n\n", "\n.\n").Replace("\n", "\n ");

    /**
     * Microsoft documentation:    https://github.com/dotnet/core/blob/main/release-notes/8.0/linux-packages.md#debian-11-bullseye
     *                             https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies
     * Microsoft container images: https://github.com/dotnet/dotnet-docker/blob/main/src/runtime-deps/8.0/bookworm-slim/arm32v7/Dockerfile
     * Ubuntu packages:            https://packages.ubuntu.com/mantic/dotnet-runtime-8.0
     */
    private static IEnumerable<string> getDependencies(DotnetPackageType packageType, DotnetRelease dotnetRelease, DebianRelease debian) => packageType switch {
        DotnetPackageType.CLI => [],
        DotnetPackageType.RUNTIME => [
            $"{DotnetPackageType.CLI.getPackageName()} (>= {dotnetRelease.patchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX})",
            "libc6",
            debian >= DebianRelease.BULLSEYE && dotnetRelease.minorVersionNumeric >= 8 ? "libgcc-s1" : "libgcc1",
            debian.getLibIcuDependencyName(),
            "libgssapi-krb5-2",
            debian >= DebianRelease.BOOKWORM ? "libssl3" : "libssl1.1",
            "libstdc++6",
            "zlib1g",
            "tzdata" // this only shows up in the .NET 8 Bookworm Docker image
        ],
        DotnetPackageType.ASPNETCORE_RUNTIME => [$"{DotnetPackageType.RUNTIME.getPackageName()}-{dotnetRelease.minorVersion} (= {dotnetRelease.patchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX})"],
        DotnetPackageType.SDK => [$"{DotnetPackageType.ASPNETCORE_RUNTIME.getPackageName()}-{dotnetRelease.minorVersion} (= {dotnetRelease.patchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX})"]
    };

    private static string dos2UnixPathSeparators(string dosPath) => dosPath.Replace('\\', '/');

    private static bool isStableVersion(JsonNode? release) {
        string patchVersionNumber = release!["release-version"]!.GetValue<string>();
        return !patchVersionNumber.Contains("-preview.") && !patchVersionNumber.Contains("-rc.");
    }

}