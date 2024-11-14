namespace RaspberryPiDotnetRepository.Data;

/// <summary>
/// Inject this by depending upon the <c>IOptions&lt;Options&gt;</c> type.
/// </summary>
public record Options {

    public string repositoryBaseDir { get; init; } = @".\raspbian\";

    public string tempDir { get; init; } = @".\temp\";

    public string gpgPublicKeyPath { get; init; } = @".\dotnet-raspbian.gpg.pub.asc";

    public string gpgPrivateKeyPath { get; init; } = @".\dotnet-raspbian.gpg.priv";

    public bool keepTempDownloads { get; init; }

    public bool dryRun { get; init; }

    // public bool forceRegenerate { get; init; }

    public string? cdnTenantId { get; set; }

    public string? cdnClientId { get; set; }

    public string? cdnCertFilePath { get; set; }

    public string cdnCertPassword { get; init; } = string.Empty;

    public string? cdnSubscriptionId { get; set; }

    public string? cdnResourceGroup { get; set; }

    public string? cdnProfile { get; set; }

    public string? cdnEndpointName { get; set; }

    /// <summary>
    /// From Azure Portal > Storage accounts > account > Access keys > Connection string
    /// </summary>
    public required string storageConnection { get; init; }

    public required string storageContainerName { get; init; }

    public int storageParallelUploads { get; set; } = 1;

    internal void sanitize() {
        storageParallelUploads = Math.Max(1, storageParallelUploads);
        cdnTenantId            = cdnTenantId?.EmptyToNull();
        cdnClientId            = cdnClientId?.EmptyToNull();
        cdnCertFilePath        = cdnCertFilePath?.EmptyToNull();
        cdnSubscriptionId      = cdnSubscriptionId?.EmptyToNull();
        cdnResourceGroup       = cdnResourceGroup?.EmptyToNull();
        cdnProfile             = cdnProfile?.EmptyToNull();
        cdnEndpointName        = cdnEndpointName?.EmptyToNull();
    }

}