using RaspberryPiDotnetRepository.Unfucked.System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

namespace RaspberryPiDotnetRepository.Unfucked.System.IO;

public static class Path2 {

    [return: NotNullIfNotNull(nameof(path))]
    public static string? trimSlashes(string? path) => path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string getTempDirectory(string? parentDir = null) {
        parentDir ??= Path.GetTempPath();

        string tempDirectory;
        do {
            tempDirectory = Path.Combine(parentDir, "temp-" + RandomStringGenerator.getString(8));
        } while (Directory.Exists(tempDirectory));

        Directory.CreateDirectory(tempDirectory);

        return tempDirectory;
    }

    [return: NotNullIfNotNull(nameof(dosPath))]
    public static string? dos2UnixSlashes(string? dosPath) => dosPath?.Replace('\\', '/');

}