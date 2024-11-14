namespace RaspberryPiDotnetRepository.Data;

public record UploadableFile(string filePathRelativeToRepo, bool isUpToDateInBlobStorage = false);