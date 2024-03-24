using Bom.Squad;
using DataSizeUnits;
using LibObjectFile.Ar;
using Org.BouncyCastle.Bcpg;
using PgpCore;
using RaspberryPiDotnetRepository.Data;
using RaspberryPiDotnetRepository.Unfucked.PGPCore;
using RaspberryPiDotnetRepository.Unfucked.SharpCompress.Writers.Tar;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Tar;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CompressionMode = SharpCompress.Compressors.CompressionMode;
using FileInfo = System.IO.FileInfo;
using PGP = RaspberryPiDotnetRepository.Unfucked.PGPCore.PGP;

namespace RaspberryPiDotnetRepository;

internal static class Program {

    private const string REPOSITORY_BASEDIR            = @"C:\Users\Ben\Desktop\dotnet rpi\raspbian";
    private const string DEBIAN_PACKAGE_VERSION_SUFFIX = "-0";

    private const CompressionLevel                                   GZ_COMPRESSION_LEVEL     = CompressionLevel.Optimal;
    private const SharpCompress.Compressors.Deflate.CompressionLevel TAR_GZ_COMPRESSION_LEVEL = SharpCompress.Compressors.Deflate.CompressionLevel.Default;

    private static readonly Encoding UTF8       = new UTF8Encoding(false, true);
    private static readonly string   TEMPDIR    = Path.Combine(REPOSITORY_BASEDIR, @"..\temp");
    private static readonly string   PACKAGEDIR = Path.Combine(REPOSITORY_BASEDIR, "packages");
    private static readonly string   BADGEDIR   = Path.Combine(REPOSITORY_BASEDIR, "badges");

    // Found on https://github.com/dotnet/core#release-information
    private static readonly Uri DOTNET_RELEASE_INDEX = new("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json");

    private static readonly HttpClient HTTP_CLIENT = new(new SocketsHttpHandler { MaxConnectionsPerServer = Environment.ProcessorCount, AllowAutoRedirect = true })
        { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly IPGP PGP = new PGP(new EncryptionKeys(File.ReadAllText(@"C:\Users\Ben\Desktop\dotnet rpi\dotnet-raspbian.gpg.priv", UTF8), string.Empty))
        { HashAlgorithmTag = HashAlgorithmTag.Sha256 };

    private static async Task Main() {
        Stopwatch overallTimer = Stopwatch.StartNew();
        BomSquad.DefuseUtf8Bom();

        Directory.CreateDirectory(REPOSITORY_BASEDIR);
        Directory.CreateDirectory(TEMPDIR);
        Directory.CreateDirectory(PACKAGEDIR);
        Directory.CreateDirectory(BADGEDIR);

        File.Copy(@"C:\Users\Ben\Desktop\dotnet rpi\dotnet-raspbian.gpg.pub", Path.Combine(REPOSITORY_BASEDIR, "aldaviva.gpg.key"), true);
        File.Copy(@"C:\Users\Ben\Desktop\dotnet rpi\dotnet-raspbian.gpg.pub.asc", Path.Combine(REPOSITORY_BASEDIR, "aldaviva.gpg.key.asc"), true);

        DebianRelease[]   debianReleases   = Enum.GetValues<DebianRelease>();
        CpuArchitecture[] cpuArchitectures = Enum.GetValues<CpuArchitecture>();
        DotnetRuntime[]   dotnetRuntimes   = Enum.GetValues<DotnetRuntime>();

        JsonNode dotnetMinorVersionsManifest = (await HTTP_CLIENT.GetFromJsonAsync<JsonNode>(DOTNET_RELEASE_INDEX))!;

        IList<DotnetRelease> dotnetReleases = (await Task.WhenAll(dotnetMinorVersionsManifest["releases-index"]!.AsArray()
            .Where(release => release!["support-phase"]!.GetValue<string>() is "active" or "maintenance")
            .Select(async minorVersion => {
                string         minorVersionNumber  = minorVersion!["channel-version"]!.GetValue<string>();
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
                        string    downloadedSdkFilename = Path.Combine(TEMPDIR, Path.GetFileName(sdkDownloadUrl.LocalPath));
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
            }))).Compact().OrderByDescending(release => double.Parse(release.minorVersion)).ToList();

        DotnetRelease latestMinorVersion = dotnetReleases.First();
        latestMinorVersion.isLatestMinorVersion = true;

        if (dotnetReleases.FirstOrDefault(release => release.isSupportedLongTerm) is { } latestLongTerm) {
            latestLongTerm.isLatestOfSupportTerm = true;
        }

        if (dotnetReleases.FirstOrDefault(release => !release.isSupportedLongTerm) is { } latestShortTerm) {
            latestShortTerm.isLatestOfSupportTerm = true;
        }

        IList<DotnetPackageRequest> packageRequests = [];
        foreach (DebianRelease debianRelease in debianReleases) {
            foreach (CpuArchitecture cpuArchitecture in cpuArchitectures) {
                foreach (DotnetRelease dotnetRelease in dotnetReleases) {
                    foreach (DotnetRuntime dotnetRuntime in dotnetRuntimes) {
                        packageRequests.Add(new DotnetPackageRequest(dotnetRelease, dotnetRuntime, cpuArchitecture, debianRelease, dotnetRelease.downloadedSdkArchiveFilePaths[cpuArchitecture]));
                    }
                }
            }
        }

        Console.WriteLine("Generating packages");
        DebianPackage[] generatedPackages = await Task.WhenAll(packageRequests.AsParallel().Select(generateDebPackage));
        Console.WriteLine("Generated packages");

        Console.WriteLine("Generating package index files");
        IEnumerable<IGrouping<DebianRelease, string>> packageIndexFiles = (await Task.WhenAll(generatedPackages
                .GroupBy(package => (package.debianVersion, package.architecture))
                .Select(async suitePackages =>
                    (suitePackages.Key.debianVersion, packageIndexFiles: await generatePackageIndexFiles(suitePackages.Key.debianVersion, suitePackages.Key.architecture, suitePackages)))))
            .SelectMany(suitePackages => suitePackages.packageIndexFiles.Select(packageIndexFile => (suitePackages.debianVersion, packageIndexFile)))
            .GroupBy(suitePackages => suitePackages.debianVersion, suitePackages => suitePackages.packageIndexFile);
        Console.WriteLine("Generated package index files");

        Console.WriteLine("Generating release index files");
        await Task.WhenAll(packageIndexFiles.Select(releaseFiles => generateReleaseIndexFiles(releaseFiles.Key, releaseFiles)));
        Console.WriteLine("Generated release index files");

        string                 latestDotnetVersion = latestMinorVersion.patchVersion;
        var                    dotnetBadge         = new { latestVersion = latestDotnetVersion };
        await using FileStream dotnetBadgeFile     = File.Create(Path.Combine(BADGEDIR, "dotnet.json"));
        await JsonSerializer.SerializeAsync(dotnetBadgeFile, dotnetBadge);

        DebianRelease          latestDebianVersion = debianReleases.Max();
        var                    debianBadge         = new { latestVersion = $"{latestDebianVersion.getCodename()} ({latestDebianVersion.getMajorVersion():D})" };
        await using FileStream debiantBadgeFile    = File.Create(Path.Combine(BADGEDIR, "raspbian.json"));
        await JsonSerializer.SerializeAsync(debiantBadgeFile, debianBadge);

        overallTimer.Stop();
        Console.WriteLine($"Finished in {overallTimer.Elapsed.TotalSeconds:N0} seconds at Default/Optimal compression.");
    }

    private static async Task generateReleaseIndexFiles(DebianRelease debianRelease, IEnumerable<string> packageIndexFilesRelativeToSuite) {
        Console.WriteLine($"Generating Release index file for Debian {debianRelease}");
        string suiteDirectory = Path.Combine(REPOSITORY_BASEDIR, "dists", debianRelease.getCodename());

        string indexSha256Hashes = string.Join('\n', await Task.WhenAll(packageIndexFilesRelativeToSuite.Select(async filePath => {
            await using FileStream fileStream = File.OpenRead(Path.Combine(suiteDirectory, filePath));
            string                 sha256Hash = Convert.ToHexString(await SHA256.HashDataAsync(fileStream)).ToLowerInvariant();
            long                   fileSize   = fileStream.Length;
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
        await File.WriteAllTextAsync(Path.Combine(suiteDirectory, "Release.gpg"), await PGP.DetachedSignAsync(releaseFileCleartext), UTF8);
        Console.WriteLine($"Wrote Release.gpg signature of Release meta-index of package indices for Raspbian {debianRelease.getCodename()}");

        // gpg --sign --clearsign --armor
        await File.WriteAllTextAsync(Path.Combine(suiteDirectory, "InRelease"), await PGP.ClearSignAsync(releaseFileCleartext), UTF8);
        Console.WriteLine($"Wrote signed InRelease meta-index of package indices for Raspbian {debianRelease.getCodename()}");

        Console.WriteLine($"Generated Release index file for Debian {debianRelease}");
    }

    private static async Task<IEnumerable<string>> generatePackageIndexFiles(DebianRelease debianRelease, CpuArchitecture cpuArchitecture, IEnumerable<DebianPackage> debPackages) {
        Console.WriteLine($"Generating Packages index file for Debian {debianRelease} {cpuArchitecture}");
        string packageFileContents = string.Join("\n\n", await Task.WhenAll(debPackages.Select(async package => {
            await using FileStream debPackageStream = File.OpenRead(package.absoluteFilename);
            string                 fileSha256Hash   = Convert.ToHexString(await SHA256.HashDataAsync(debPackageStream)).ToLowerInvariant();
            return $"""
                    {package.controlMetadata}
                    Filename: packages/{debianRelease.getCodename()}/{Path.GetFileName(package.absoluteFilename)}
                    Size: {new FileInfo(package.absoluteFilename).Length:D}
                    SHA256: {fileSha256Hash}
                    """.ReplaceLineEndings("\n");
        })));

        string packageFileRelativeToSuite = Path.Combine("main", $"binary-{cpuArchitecture.toDebian()}", "Packages");
        string packageFileAbsolutePath    = Path.Combine(REPOSITORY_BASEDIR, "dists", debianRelease.getCodename(), packageFileRelativeToSuite);
        Directory.CreateDirectory(Path.GetDirectoryName(packageFileAbsolutePath)!);
        await using StreamWriter packageIndexStreamWriter = new(packageFileAbsolutePath, false, UTF8);
        await packageIndexStreamWriter.WriteAsync(packageFileContents);
        Console.WriteLine($"Wrote uncompressed Packages index for Raspbian {debianRelease.getCodename()} {cpuArchitecture.toDebian()}");

        string                   compressedPackageFileRelativeToSuite = Path.ChangeExtension(packageFileRelativeToSuite, "gz");
        string                   compressedPackageFileAbsolutePath    = Path.ChangeExtension(packageFileAbsolutePath, "gz");
        await using FileStream   compressedPackageIndexFileStream     = File.Create(compressedPackageFileAbsolutePath);
        await using GZipStream   gzipStream                           = new(compressedPackageIndexFileStream, GZ_COMPRESSION_LEVEL);
        await using StreamWriter compressedPackageIndexStreamWriter   = new(gzipStream, UTF8);
        await compressedPackageIndexStreamWriter.WriteAsync(packageFileContents);
        Console.WriteLine($"Wrote compressed Packages.gz index for Raspbian {debianRelease.getCodename()} {cpuArchitecture.toDebian()}");

        Console.WriteLine($"Generated Packages index file for Debian {debianRelease} {cpuArchitecture}");
        return [packageFileRelativeToSuite, compressedPackageFileRelativeToSuite];
    }

    private static async Task<DebianPackage> generateDebPackage(DotnetPackageRequest packageToGenerate) {
        Console.WriteLine($"Generating package for Debian {packageToGenerate.debian} {packageToGenerate.architecture} {packageToGenerate.runtime} {packageToGenerate.dotnetRelease.minorVersion}");
        string packageName                 = packageToGenerate.runtime.getPackageName();
        bool   isCliPackage                = packageToGenerate.runtime == DotnetRuntime.CLI;
        string packageNameWithMinorVersion = isCliPackage ? packageName : $"{packageName}-{packageToGenerate.dotnetRelease.minorVersion}";

        DataSize installedSize = new();
        ISet<string> existingDirectories = new HashSet<string> {
            "./usr/share",
            "./usr/bin"
        };

        await using Stream dataArchiveStream = new MemoryStream();

        await using (FileStream sdkArchiveStream = File.OpenRead(packageToGenerate.sdkArchivePath))
        using (IReader downloadReader = ReaderFactory.Open(sdkArchiveStream))
        await using (SharpCompress.Compressors.Deflate.GZipStream dataGzipStream = new(NonDisposingStream.Create(dataArchiveStream), CompressionMode.Compress, TAR_GZ_COMPRESSION_LEVEL))
        using (UnfuckedTarWriter dataArchiveWriter = new(dataGzipStream, new TarWriterOptions(CompressionType.None, true))) {
            while (downloadReader.MoveToNextEntry()) {
                IEntry entry = downloadReader.Entry;
                if (entry.IsDirectory) continue;

                string   sourcePath      = entry.Key[2..]; // remove leading "./"
                string   destinationPath = $"./usr/share/dotnet/{sourcePath}";
                int?     fileMode        = null;
                DateTime lastModified    = entry.LastModifiedTime ?? entry.CreatedTime ?? entry.ArchivedTime ?? DateTime.Now;

                switch (packageToGenerate.runtime) {
                    case DotnetRuntime.CLI:
                        if (sourcePath == "dotnet") {
                            fileMode = o(755);
                        }

                        break;
                    case DotnetRuntime.RUNTIME:
                        if (sourcePath.StartsWith("shared/Microsoft.NETCore.App/")) {
                            fileMode = Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) == "createdump" ? o(755) : o(644);
                        } else if (sourcePath is "ThirdPartyNotices.txt" or "LICENSE.txt") {
                            fileMode = o(644);
                        } else if (sourcePath.StartsWith("host/fxr")) {
                            fileMode = o(755);
                        }

                        break;
                    case DotnetRuntime.ASPNETCORE_RUNTIME:
                        if (sourcePath.StartsWith("shared/Microsoft.AspNetCore.App/")) {
                            fileMode = o(644);
                        }

                        break;
                    case DotnetRuntime.SDK:
                        if (sourcePath.StartsWith("packs/") || sourcePath.StartsWith("sdk") || sourcePath.StartsWith("templates/")) {
                            fileMode = o(Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) is "apphost" or "singlefilehost" ? 744 : 644);
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

                    installedSize += entry.Size;
                    // It is very important to dispose each EntryStream, otherwise the IReader will randomly throw an IncompleteArchiveException on a later file in the archive
                    await using EntryStream downloadedInnerFileStream = downloadReader.OpenEntryStream();
                    dataArchiveWriter.WriteFile(destinationPath, downloadedInnerFileStream, lastModified, entry.Size, fileMode);

                    if (destinationPath == "./usr/share/dotnet/dotnet") {
                        const string SYMLINK_PATH = "./usr/bin/dotnet";
                        // can't figure out how to make absolute symlinks in deb packages, so making it relative
                        dataArchiveWriter.WriteSymLink(SYMLINK_PATH, "../share/dotnet/dotnet", lastModified);
                        installedSize += 1024;
                    }
                }
            }
        }

        dataArchiveStream.Position = 0;

        IList<string> dependencies = getDependencies(packageToGenerate.runtime, packageToGenerate.dotnetRelease.minorVersion, packageToGenerate.dotnetRelease.patchVersion, packageToGenerate.debian)
            .ToList();
        string depends = dependencies.Any() ? $"Depends: {string.Join(", ", dependencies)}" : string.Empty;
        // TODO try suggesting a virtual package fulfilled by any runtime (like dotnet-runtime-8.0-or-greater) once they exist, so apt stops telling you that dotnet-runtime-8.0 is suggested when installing dotnet-runtime-6.0
        string suggests = isCliPackage ? $"Suggests: {DotnetRuntime.RUNTIME.getPackageName()}-{packageToGenerate.dotnetRelease.minorVersion}" : string.Empty;
        IList<string> providing = new[] {
            !isCliPackage && packageToGenerate.dotnetRelease.isLatestMinorVersion ? $"{packageName} (= {packageToGenerate.dotnetRelease.minorVersion})" : null,
            !isCliPackage && packageToGenerate.dotnetRelease.isLatestOfSupportTerm
                ? $"{packageName}-{(packageToGenerate.dotnetRelease.isSupportedLongTerm ? "lts" : "sts")} (= {packageToGenerate.dotnetRelease.minorVersion})"
                : null
        }.Compact();
        string provides = providing.Any() ? $"Provides: {string.Join(", ", providing)}" : string.Empty;

        string controlFileContents =
            Regex.Replace($"""
                           Package: {packageNameWithMinorVersion}
                           Version: {packageToGenerate.dotnetRelease.patchVersion}-0
                           Architecture: {packageToGenerate.architecture.toDebian()}
                           Maintainer: Ben Hutchison <ben@aldaviva.com>
                           Installed-Size: {Math.Round(installedSize.ConvertToUnit(Unit.Kilobyte).Quantity):F0}
                           {depends}
                           {suggests}
                           {provides}
                           Section: devel
                           Priority: optional
                           Homepage: https://dot.net
                           Description: {getDescription(packageToGenerate.runtime, packageToGenerate.dotnetRelease.minorVersion)}
                           """.ReplaceLineEndings("\n"), @"\n{2,}", "\n");

        await using Stream controlArchiveStream = new MemoryStream();
        using (IWriter controlArchiveWriter = WriterFactory.Open(controlArchiveStream, ArchiveType.Tar, new GZipWriterOptions { CompressionLevel = TAR_GZ_COMPRESSION_LEVEL })) {
            await using Stream controlFileBuffer = (controlFileContents + '\n').ToStream();
            controlArchiveWriter.Write("./control", controlFileBuffer);
        }

        controlArchiveStream.Position = 0;

        ArArchiveFile debArchive = new();
        debArchive.AddFile(new ArBinaryFile {
            Name   = "debian-binary",
            Stream = "2.0\n".ToStream()
        });
        debArchive.AddFile(new ArBinaryFile {
            Name   = "control.tar.gz",
            Stream = controlArchiveStream
        });
        debArchive.AddFile(new ArBinaryFile {
            Name   = "data.tar.gz",
            Stream = dataArchiveStream
        });

        string debFileAbsolutePath = Path.Combine(PACKAGEDIR, packageToGenerate.debian.getCodename(),
            $"{packageName}-{packageToGenerate.dotnetRelease.patchVersion}-{packageToGenerate.architecture.toDebian()}.deb");
        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (Stream debStream = File.Create(debFileAbsolutePath)) {
            debArchive.Write(debStream);
        }

        Console.WriteLine($"Wrote package {debFileAbsolutePath}");
        Console.WriteLine($"Generated package for Debian {packageToGenerate.debian} {packageToGenerate.architecture} {packageToGenerate.runtime} {packageToGenerate.dotnetRelease.minorVersion}");
        return new DebianPackage(packageNameWithMinorVersion, packageToGenerate.dotnetRelease.patchVersion, packageToGenerate.debian, packageToGenerate.architecture, packageToGenerate.runtime,
            controlFileContents, debFileAbsolutePath);
    }

    // Subsequent lines in each of these strings will be prefixed with a leading space in the package control file because that indentation is how multi-line descriptions work.
    //
    // Except for the summary first line of every description string, each line that starts with ".NET" actually starts with U+2024 ONE DOT LEADER instead of U+002E FULL STOP (a normal period).
    // This is because lines that start with periods have special meaning in Debian packages. Specifically, a period on a line of its own is interpreted as a blank line to differentiate it from a new package record, but a line that starts with a period and has more text after it (like .NET) is illegal and should not be used. Aptitude renders such lines as blank lines.
    //
    // https://www.debian.org/doc/debian-policy/ch-controlfields.html#description
    private static string getDescription(DotnetRuntime dotnetRuntime, string minorVersion) => (dotnetRuntime switch {
        DotnetRuntime.CLI =>
            $"""
             .NET CLI tool (without runtime)
             This package installs the '/usr/bin/dotnet' command-line interface, which is part of all .NET installations.

             This package does not install any .NET runtimes or SDKs by itself, so .NET applications won't run with only this package. This is just a common dependency used by the other .NET packages.

             To actually run or build .NET applications, you must also install one of the .NET runtime or SDK packages, for example, {DotnetRuntime.RUNTIME.getPackageName()}-{minorVersion}, {DotnetRuntime.ASPNETCORE_RUNTIME.getPackageName()}-{minorVersion}, or {DotnetRuntime.SDK.getPackageName()}-{minorVersion}.
             """,
        DotnetRuntime.RUNTIME =>
            """
            .NET CLI tools and runtime
            ․NET is a fast, lightweight and modular platform for creating cross platform applications that work on GNU/Linux, macOS and Windows.

            It particularly focuses on creating console applications, web applications, and micro-services.

            ․NET contains a runtime conforming to .NET Standards, a set of framework libraries, an SDK containing compilers, and a 'dotnet' CLI application to drive everything.
            """,
        DotnetRuntime.ASPNETCORE_RUNTIME =>
            """
            ASP.NET Core runtime
            The ASP.NET Core runtime contains everything needed to run .NET web applications. It includes a high performance Virtual Machine as well as the framework libraries used by .NET applications.

            ASP.NET Core is a fast, lightweight and modular platform for creating cross platform applications that work on GNU/Linux, macOS and Windows.

            It particularly focuses on creating console applications, web applications, and micro-services.
            """,
        DotnetRuntime.SDK =>
            $"""
             .NET {minorVersion} Software Development Kit
             The .NET SDK is a collection of command line applications to create, build, publish, and run .NET applications.

             ․NET is a fast, lightweight and modular platform for creating cross platform applications that work on GNU/Linux, macOS and Windows.

             It particularly focuses on creating console applications, web applications, and micro-services.
             """
    }).ReplaceLineEndings("\n").Replace("\n\n", "\n.\n").Replace("\n", "\n ");

    /**
     * Microsoft documentation:    https://github.com/dotnet/core/blob/main/release-notes/8.0/linux-packages.md#debian-11-bullseye
     *                             https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies
     * Microsoft container images: https://github.com/dotnet/dotnet-docker/blob/main/src/runtime-deps/8.0/bookworm-slim/arm32v7/Dockerfile
     * Ubuntu packages:            https://packages.ubuntu.com/mantic/dotnet-runtime-8.0
     */
    private static IEnumerable<string> getDependencies(DotnetRuntime dotnetRuntime, string dotnetMinorVersion, string dotnetPatchVersion, DebianRelease debian) {
        double dotnetMinor = double.Parse(dotnetMinorVersion);
        string libicuDependency = "libicu" + debian switch {
            DebianRelease.BUSTER => "63",
            DebianRelease.BULLSEYE => "67",
            DebianRelease.BOOKWORM => "72",
            _ => throw new ArgumentOutOfRangeException(nameof(debian), debian, $"Please specify which version of libicu is used by this version of Debian in {nameof(getDependencies)}")
        };

        return dotnetRuntime switch {
            DotnetRuntime.CLI => [],
            DotnetRuntime.RUNTIME => [
                $"{DotnetRuntime.CLI.getPackageName()} (>= {dotnetPatchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX})",
                "libc6",
                debian >= DebianRelease.BULLSEYE && dotnetMinor >= 8 ? "libgcc-s1" : "libgcc1",
                libicuDependency,
                "libgssapi-krb5-2",
                debian >= DebianRelease.BOOKWORM ? "libssl3" : "libssl1.1",
                "libstdc++6",
                "zlib1g",
                "tzdata" // this only shows up in the .NET 8 Bookworm Docker image
            ],
            DotnetRuntime.ASPNETCORE_RUNTIME => [$"{DotnetRuntime.RUNTIME.getPackageName()}-{dotnetMinorVersion} (= {dotnetPatchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX})"],
            DotnetRuntime.SDK                => [$"{DotnetRuntime.ASPNETCORE_RUNTIME.getPackageName()}-{dotnetMinorVersion} (= {dotnetPatchVersion}{DEBIAN_PACKAGE_VERSION_SUFFIX})"]
        };
    }

    public static int o(string octal) => Convert.ToInt32(octal, 8);
    public static int o(int    octal) => o(octal.ToString());

    public static string dos2UnixPathSeparators(string dosPath) => dosPath.Replace('\\', '/');

    private static bool isStableVersion(JsonNode? release) {
        string patchVersionNumber = release!["release-version"]!.GetValue<string>();
        return !patchVersionNumber.Contains("-preview.") && !patchVersionNumber.Contains("-rc.");
    }

}