using System.Security.Cryptography;

namespace Tests;

public class DirectorySwapTest {

    [Fact]
    public async Task swapWithMove() {
        string oldDir, newDir, dirToDelete;
        do {
            oldDir = Path.Combine(Path.GetTempPath(), generateRandomString(16));
        } while (Directory.Exists(oldDir));

        do {
            newDir = Path.Combine(Path.GetTempPath(), generateRandomString(16));
        } while (Directory.Exists(newDir));

        do {
            dirToDelete = Path.Combine(Path.GetTempPath(), generateRandomString(16));
        } while (Directory.Exists(dirToDelete));

        Directory.CreateDirectory(oldDir);
        Directory.CreateDirectory(newDir);

        string oldFilename = Path.Combine(oldDir, "oldFile.txt");
        string newFilename = Path.Combine(newDir, "newFile.txt");

        await File.WriteAllTextAsync(oldFilename, "Old file");
        await File.WriteAllTextAsync(newFilename, "New file");

        Directory.Move(oldDir, dirToDelete);
        Directory.Move(newDir, oldDir);
        Directory.Delete(dirToDelete, true);

        string actual = await File.ReadAllTextAsync(Path.Combine(oldDir, "newFile.txt"));
        actual.Should().Be("New file");

        Directory.Exists(dirToDelete).Should().BeFalse();
        Directory.Exists(newDir).Should().BeFalse();
    }

    [Theory]
    [InlineData(@".\raspbian\")]
    [InlineData(@"./raspbian/")]
    [InlineData(@".\raspbian")]
    [InlineData(@"C:\raspbian")]
    [InlineData(@"C:\raspbian\")]
    [InlineData(@"C:/raspbian/")]
    public void tempDirectoryName(string originalDir) {
        string newDir = Path.GetFullPath(originalDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "-new";
        newDir.Should().EndWith(@"\raspbian-new");
    }

    // https://stackoverflow.com/a/73101585/979493
    public static string generateRandomString(uint length, string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789") {
        char[] distinctAlphabet       = alphabet.Distinct().ToArray();
        int    distinctAlphabetLength = distinctAlphabet.Length;
        char[] result                 = new char[length];
        for (int i = 0; i < length; i++) {
            result[i] = distinctAlphabet[RandomNumberGenerator.GetInt32(distinctAlphabetLength)];
        }

        return new string(result);
    }

}