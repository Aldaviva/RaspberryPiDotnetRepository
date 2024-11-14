using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Data;
using RaspberryPiDotnetRepository.DotnetUpstream;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers;
using System.Security.Cryptography;
using Unfucked.Compression.Writers.Tar;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Debian.Package;

/// <summary>
/// .NET specific logic for generating packages by converting upstream .NET SDK tarballs into .deb files.
/// Uses <see cref="PackageBuilder"/> to assemble the .deb files.
/// </summary>
public interface PackageGenerator {

    Task<DebianPackage> generateDebPackage(PackageRequest packageToGenerate, UpstreamReleasesSecondaryInfo secondaryInfo, RepositoryManifest? oldManifest);

}

public class PackageGeneratorImpl(IOptions<Options> options, StatisticsService statistics, ILogger<PackageGeneratorImpl> logger): PackageGenerator {

    private const CompressionLevel DEB_TGZ_COMPRESSION = CompressionLevel.Default;

    public async Task<DebianPackage> generateDebPackage(PackageRequest packageToGenerate, UpstreamReleasesSecondaryInfo secondaryInfo, RepositoryManifest? oldManifest) => packageToGenerate switch {
        DotnetPackageRequest r => await generateDebPackage(r, secondaryInfo, oldManifest),
        MetaPackageRequest r => await generateDebPackage(r, secondaryInfo, oldManifest),
        _ => throw new ArgumentOutOfRangeException(nameof(packageToGenerate), packageToGenerate, $"Update {nameof(generateDebPackage)} to handle {packageToGenerate.GetType().Name}")
    };

    private async Task<DebianPackage> generateDebPackage(DotnetPackageRequest packageToGenerate, UpstreamReleasesSecondaryInfo secondaryInfo, RepositoryManifest? oldManifest) {
        DebianPackage generatedPackage = new(packageToGenerate.packageType, packageToGenerate.dotnetRelease.runtimeVersion, packageToGenerate.dotnetRelease.sdkVersion,
            packageToGenerate.architecture);
        string debFileAbsolutePath = Path.GetFullPath(Path.Combine(options.Value.repositoryBaseDir, generatedPackage.filePathRelativeToRepo));

        if (oldManifest?.packages.FirstOrDefault(p =>
                !p.isMetaPackage &&
                p.architecture == packageToGenerate.architecture &&
                p.runtime == packageToGenerate.packageType &&
                p.sdkVersion.Equals(packageToGenerate.dotnetRelease.sdkVersion) &&
                p.versionSuffix == DebianPackage.VERSION_SUFFIX) is { } oldPackage) {
            return oldPackage;
        }

        ISet<string> existingDirectories = new HashSet<string> {
            "./usr/share",
            "./usr/bin"
        };

        await using PackageBuilder debPackageBuilder = new PackageBuilderImpl { gzipCompressionLevel = DEB_TGZ_COMPRESSION };

        await using (FileStream sdkArchiveStream = File.OpenRead(packageToGenerate.sdkArchivePath))
        using (IReader downloadReader = ReaderFactory.Open(sdkArchiveStream))
        using (TarWriter dataArchiveWriter = debPackageBuilder.data) {
            while (downloadReader.MoveToNextEntry()) {
                IEntry entry = downloadReader.Entry;
                if (entry.IsDirectory || entry.Key == null) continue;

                string sourcePath = entry.Key.TrimStart("./");
                int? fileMode = generatedPackage.runtime switch {
                    RuntimeType.CLI when sourcePath == "dotnet" => o(755),
                    RuntimeType.RUNTIME when sourcePath.StartsWith("shared/Microsoft.NETCore.App/") => Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) == "createdump"
                        ? o(755) : o(644),
                    RuntimeType.RUNTIME when sourcePath.StartsWith("host/fxr")                                    => o(755),
                    RuntimeType.ASPNETCORE_RUNTIME when sourcePath.StartsWith("shared/Microsoft.AspNetCore.App/") => o(644),
                    RuntimeType.SDK when sourcePath.StartsWith("packs/") || sourcePath.StartsWith("sdk") || sourcePath.StartsWith("templates/") => Path.GetExtension(sourcePath) == ".so"
                        || Path.GetFileName(sourcePath) is "apphost" or "singlefilehost" ? o(744) : o(644),
                    _ => null //exclude file
                };

                // #32: when attempting to install multiple Runtime packages, dpkg will fail because they all contain ThirdPartyNotices.txt with the same path, so rename them.
                if (generatedPackage.runtime == RuntimeType.RUNTIME && sourcePath is "ThirdPartyNotices.txt" or "LICENSE.txt") {
                    sourcePath = $"{Path.GetFileNameWithoutExtension(sourcePath)}-{generatedPackage.minorVersion}{Path.GetExtension(sourcePath)}";
                    fileMode   = o(644);
                }

                if (fileMode != null) {
                    string        destinationPath  = $"./usr/share/dotnet/{sourcePath}";
                    IList<string> directoriesToAdd = [];
                    for (string destinationDirectory = Paths.Dos2UnixSlashes(Path.GetDirectoryName(destinationPath)!);
                         existingDirectories.Add(destinationDirectory);
                         destinationDirectory = Paths.Dos2UnixSlashes(Path.GetDirectoryName(destinationDirectory)!)) {

                        directoriesToAdd.Add(destinationDirectory);
                    }

                    foreach (string directoryToAdd in directoriesToAdd.Reverse()) {
                        dataArchiveWriter.WriteDirectory(directoryToAdd, null, o(755));
                        generatedPackage.installationSize += 1024;
                    }

                    DateTime lastModified = entry.LastModifiedTime ?? entry.CreatedTime ?? entry.ArchivedTime ?? DateTime.Now;

                    // It is critical to dispose each EntryStream, otherwise the IReader will randomly throw an IncompleteArchiveException on a later file in the archive
                    await using EntryStream downloadedInnerFileStream = downloadReader.OpenEntryStream();
                    dataArchiveWriter.WriteFile(destinationPath, downloadedInnerFileStream, lastModified, entry.Size, fileMode);
                    generatedPackage.installationSize += entry.Size;

                    if (sourcePath == "dotnet") {
                        // can't figure out how to make absolute symlinks in deb packages, so making it relative
                        dataArchiveWriter.WriteSymLink("./usr/bin/dotnet", "../share/dotnet/dotnet", lastModified);
                        generatedPackage.installationSize += 1024;
                    }
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (Stream debStream = File.Create(debFileAbsolutePath)) {
            await debPackageBuilder.build(generatedPackage.getControl(secondaryInfo), debStream);
            generatedPackage.fileHashSha256 = await hashStream(debStream);
            generatedPackage.downloadSize   = new FileInfo(debFileAbsolutePath).Length;
        }

        logger.LogInformation("Generated package for Debian {arch} {name} {version} ({file})", generatedPackage.architecture.toDebian() ?? "all", generatedPackage.runtime.getFriendlyName(),
            generatedPackage.minorVersion, debFileAbsolutePath);
        statistics.onFileWritten(debFileAbsolutePath);
        return generatedPackage;
    }

    private async Task<DebianPackage> generateDebPackage(MetaPackageRequest packageToGenerate, UpstreamReleasesSecondaryInfo secondaryInfo, RepositoryManifest? oldManifest) {
        DebianPackage generatedPackage = new(packageToGenerate.packageType, packageToGenerate.concreteMinorVersion, packageToGenerate.concreteMinorVersion, packageToGenerate.architecture) {
            isMetaPackage                  = true,
            isMetaPackageSupportedLongTerm = packageToGenerate.mustBeSupportedLongTerm,
        };

        string debFileAbsolutePath = Path.GetFullPath(Path.Combine(options.Value.repositoryBaseDir, generatedPackage.filePathRelativeToRepo));

        if (oldManifest?.packages.FirstOrDefault(p =>
                p.isMetaPackage &&
                p.isMetaPackageSupportedLongTerm == packageToGenerate.mustBeSupportedLongTerm &&
                p.runtime == packageToGenerate.packageType &&
                p.version.AsMinor().Equals(packageToGenerate.concreteMinorVersion) &&
                p.versionSuffix == DebianPackage.VERSION_SUFFIX) is { } oldPackage) {
            return oldPackage;
        }

        await using PackageBuilder debPackageBuilder = new PackageBuilderImpl();
        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (Stream debStream = File.Create(debFileAbsolutePath)) {
            await debPackageBuilder.build(generatedPackage.getControl(secondaryInfo), debStream);

            generatedPackage.fileHashSha256 = await hashStream(debStream);
            generatedPackage.downloadSize   = new FileInfo(debFileAbsolutePath).Length;
        }

        logger.LogInformation("Generated package for Debian {arch} {name} {version} ({file})", generatedPackage.architecture.toDebian() ?? "all", generatedPackage.runtime.getFriendlyName(),
            generatedPackage.minorVersion, debFileAbsolutePath);
        statistics.onFileWritten(debFileAbsolutePath);
        return generatedPackage;
    }

    private static async Task<string> hashStream(Stream stream) {
        await stream.FlushAsync();
        stream.Position = 0;
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    public static int o(string octal) => Convert.ToInt32(octal, 8);
    public static int o(int octal) => o(octal.ToString());

}