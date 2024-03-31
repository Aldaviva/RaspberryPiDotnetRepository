namespace RaspberryPiDotnetRepository.Unfucked.System.IO;

public static class Directory2 {

    public static void deleteQuietly(string path, bool recursive = false) {
        try {
            Directory.Delete(path, recursive);
        } catch (DirectoryNotFoundException) { }
    }

}