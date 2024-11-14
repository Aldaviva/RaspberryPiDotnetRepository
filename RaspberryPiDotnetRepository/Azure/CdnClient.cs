using Azure;
using Azure.ResourceManager.Cdn;
using Azure.ResourceManager.Cdn.Models;
using Microsoft.Extensions.Options;
using Options = RaspberryPiDotnetRepository.Data.Options;

namespace RaspberryPiDotnetRepository.Azure;

/*
 * There seems to be no upper limit to the duration of a certificate that Azure apps can use, unlike client secrets, which must be rotated at least once every 2 years.
 *
 * You can generate a new certificate using PowerShell:
 *
 * New-SelfSignedCertificate -KeyAlgorithm RSA -KeyLength 2048 -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable -KeySpec Signature -Subject "CN=RaspberryPiDotnetRepository" -NotAfter (Get-Date).AddYears(100)
 *
 * Then, use certmgr.msc to export this certificate as a CER file without the private key, and upload it to portal.azure.com > App registrations > your app > Certificates & secrets.
 *
 * Next, export the same certificate as a PFX file with the private key and a password, and pass its absolute path as certFilePath.
 *
 * You may now delete the cert from the Personal store if you want.
 */
public interface CdnClient {

    Task purge(IEnumerable<string> paths);

}

public class CdnClientImpl(CdnEndpointResource? cdnEndpoint, ILogger<CdnClientImpl> logger, IOptions<Options> options): CdnClient {

    public async Task purge(IEnumerable<string> paths) {
        if (cdnEndpoint == null) {
            logger.LogInformation("No CDN configured, not purging");
        } else if (options.Value.dryRun) {
            logger.LogInformation("Would have started CDN purge if not in dry run");
        } else {
            await cdnEndpoint.PurgeContentAsync(WaitUntil.Started, new PurgeContent(paths));
            logger.LogInformation("Starting CDN purge, will finish asynchronously later");
        }
    }

}