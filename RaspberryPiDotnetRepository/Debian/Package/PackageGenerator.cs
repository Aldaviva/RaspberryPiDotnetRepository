using DataSizeUnits;
using Microsoft.Extensions.Options;
using RaspberryPiDotnetRepository.Data;
using RaspberryPiDotnetRepository.DotnetUpstream;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
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

    private const CompressionLevel DEB_TGZ_COMPRESSION = CompressionLevel.SmallestSize;

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
                p.versionSuffix == DebianPackage.VERSION_SUFFIX) is {} oldPackage) {
            return oldPackage;
        }

        HashSet<string> existingDirectories = ["./usr/share", "./usr/bin"];

        await using PackageBuilder debPackageBuilder = new PackageBuilderImpl { gzipCompressionLevel = DEB_TGZ_COMPRESSION };

        {
            await using FileStream sdkArchiveStream  = File.OpenRead(packageToGenerate.sdkArchivePath);
            await using GZipStream gzipReadStream    = new(sdkArchiveStream, CompressionMode.Decompress);
            await using TarReader  tarReader         = new(gzipReadStream);
            await using TarWriter  dataArchiveWriter = debPackageBuilder.data;

            while (await tarReader.GetNextEntryAsync(true) is {} entry) {
                if (entry.EntryType == TarEntryType.Directory) continue;

                string sourcePath = entry.Name.TrimStart("./");
                UnixFileMode? fileMode = generatedPackage.runtime switch {
                    RuntimeType.CLI when sourcePath is "dotnet" or "dnx" => o(755),
                    RuntimeType.RUNTIME when sourcePath.StartsWith("shared/Microsoft.NETCore.App/") =>
                        Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) == "createdump" ? o(755) : o(644),
                    RuntimeType.RUNTIME when sourcePath.StartsWith("host/fxr")                                    => o(755),
                    RuntimeType.ASPNETCORE_RUNTIME when sourcePath.StartsWith("shared/Microsoft.AspNetCore.App/") => o(644),
                    RuntimeType.SDK when sourcePath.StartsWith("packs/") || sourcePath.StartsWith("sdk") || sourcePath.StartsWith("templates/") =>
                        Path.GetExtension(sourcePath) == ".so" || Path.GetFileName(sourcePath) is "apphost" or "singlefilehost" or "csc" or "VBCSCompiler" or "vbc" ? o(755) : o(644),
                    _ => null //exclude file
                };

                // #32: when attempting to install multiple Runtime packages, dpkg will fail because they all contain ThirdPartyNotices.txt with the same path, so rename them.
                if (generatedPackage.runtime == RuntimeType.RUNTIME && sourcePath is "ThirdPartyNotices.txt" or "LICENSE.txt") {
                    sourcePath = Path.Dos2UnixSlashes(Path.Combine(Path.GetDirectoryName(sourcePath)!,
                        $"{Path.GetFileNameWithoutExtension(sourcePath)}-{generatedPackage.minorVersion}{Path.GetExtension(sourcePath)}"));
                    fileMode = o(644);
                }

                if (fileMode != null) {
                    string        destinationPath  = $"./usr/share/dotnet/{sourcePath}";
                    IList<string> directoriesToAdd = [];
                    for (string destinationDirectory = Path.Dos2UnixSlashes(Path.GetDirectoryName(destinationPath)!);
                         existingDirectories.Add(destinationDirectory);
                         destinationDirectory = Path.Dos2UnixSlashes(Path.GetDirectoryName(destinationDirectory)!)) {

                        directoriesToAdd.Add(destinationDirectory);
                    }

                    foreach (string directoryToAdd in directoriesToAdd.Reverse()) {
                        await dataArchiveWriter.WriteEntryAsync(new GnuTarEntry(TarEntryType.Directory, directoryToAdd) { Mode = o(755) });
                        generatedPackage.installationSize += 4096;
                    }

                    if (sourcePath is "dotnet" or "dnx") {
                        string symlinkTarget = $"../share/dotnet/{sourcePath}";
                        await dataArchiveWriter.WriteEntryAsync(new GnuTarEntry(TarEntryType.SymbolicLink, $"./usr/bin/{sourcePath}") {
                            LinkName         = symlinkTarget,
                            ModificationTime = entry.ModificationTime
                        });
                        generatedPackage.installationSize += symlinkTarget.Length;
                    } else if (entry is { EntryType: TarEntryType.SymbolicLink, LinkName: {} symlinkTarget }) {
                        // Upstream SDK tarball contained a symlink for deduplication purposes, so preserve that link
                        await dataArchiveWriter.WriteEntryAsync(new GnuTarEntry(TarEntryType.SymbolicLink, destinationPath) {
                            LinkName         = symlinkTarget,
                            ModificationTime = entry.ModificationTime
                        });
                        generatedPackage.installationSize += symlinkTarget.Length;
                    } else {
                        await using Stream? downloadedInnerFileStream = entry.DataStream;
                        await dataArchiveWriter.WriteEntryAsync(new GnuTarEntry(TarEntryType.RegularFile, destinationPath) {
                            DataStream       = downloadedInnerFileStream,
                            Mode             = fileMode.Value,
                            ModificationTime = entry.ModificationTime
                        });
                        generatedPackage.installationSize += entry.Length;
                    }
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (Stream debStream = File.Create(debFileAbsolutePath)) {
            await debPackageBuilder.build(generatedPackage.getControl(secondaryInfo), debStream);
            await debStream.FlushAsync();
            debStream.Position              = 0;
            generatedPackage.fileHashSha256 = Convert.ToHexString(await SHA256.HashDataAsync(debStream)).ToLowerInvariant();
            generatedPackage.downloadSize   = new FileInfo(debFileAbsolutePath).FileSize;
        }

        logger.Info("Generated package for Debian {arch} {name} {version} ({file})", generatedPackage.architecture.toDebian(), generatedPackage.runtime.getFriendlyName(),
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
                p.architecture == packageToGenerate.architecture &&
                p.version.AsMinor.Equals(packageToGenerate.concreteMinorVersion) &&
                p.versionSuffix == DebianPackage.VERSION_SUFFIX) is {} oldPackage) {
            return oldPackage;
        }

        await using PackageBuilder debPackageBuilder = new PackageBuilderImpl();
        Directory.CreateDirectory(Path.GetDirectoryName(debFileAbsolutePath)!);
        await using (FileStream debStream = File.Create(debFileAbsolutePath)) {
            await debPackageBuilder.build(generatedPackage.getControl(secondaryInfo), debStream);
            await debStream.FlushAsync();

            debStream.Position              = 0;
            generatedPackage.fileHashSha256 = Convert.ToHexString(await SHA256.HashDataAsync(debStream)).ToLowerInvariant();
            generatedPackage.downloadSize   = new FileInfo(debFileAbsolutePath).Length;
        }

        logger.Info("Generated package for Debian {arch} {name} {version} ({file})", generatedPackage.architecture.toDebian(), generatedPackage.runtime.getFriendlyName(),
            generatedPackage.minorVersion, debFileAbsolutePath);
        statistics.onFileWritten(debFileAbsolutePath);
        return generatedPackage;
    }

    public static UnixFileMode o(string octal) => (UnixFileMode) Convert.ToInt32(octal, 8);
    public static UnixFileMode o(int octal) => o(octal.ToString());

}