namespace RaspberryPiDotnetRepository.Data;

/// <summary>
/// Inject this by depending upon the <c>IOptions&lt;Options&gt;</c> type.
/// </summary>
public record Options {

    public string repositoryBaseDir { get; set; } = @".\raspbian\";

    public string tempDir { get; set; } = @".\temp\";

    public string gpgPublicKeyPath { get; set; } = @".\dotnet-raspbian.gpg.pub.asc";

    public string gpgPrivateKeyPath { get; set; } = @".\dotnet-raspbian.gpg.priv";

    public bool keepTempDownloads { get; set; }

    public bool forceRegenerate { get; set; }

    public string? cdnTenantId { get; set; }

    public string? cdnClientId { get; set; }

    public string? cdnCertFilePath { get; set; }

    public string cdnCertPassword { get; set; } = string.Empty;

    public string? cdnSubscriptionId { get; set; }

    public string? cdnResourceGroup { get; set; }

    public string? cdnProfile { get; set; }

    public string? cdnEndpointName { get; set; }

    /// <summary>
    /// From Azure Portal > Storage accounts > account > Access keys > Connection string
    /// </summary>
    public required string storageConnection { get; set; }

    public required string storageContainerName { get; set; }

    public uint storageParallelUploads { get; set; } = 1;

}