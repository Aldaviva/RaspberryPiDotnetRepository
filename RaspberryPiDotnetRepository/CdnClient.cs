using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Cdn;
using System.Security.Cryptography.X509Certificates;

namespace RaspberryPiDotnetRepository;

public class CdnClient {

    private readonly ArmClient resourceManager;

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
    public CdnClient(string tenantId, string clientId, string certFilePath, string certPassword) {
        TokenCredential credentials = new ClientCertificateCredential(tenantId, clientId, new X509Certificate2(certFilePath, certPassword));

        resourceManager = new ArmClient(credentials);
    }

    public async Task<CdnEndpointResource?> getEndpoint(string subscriptionId, string resourceGroup, string profile, string endpoint) {
        ResourceIdentifier endpointId = CdnEndpointResource.CreateResourceIdentifier(subscriptionId, resourceGroup, profile, endpoint);

        return (await resourceManager.GetCdnEndpointResource(endpointId).GetAsync()).AsNullable();
    }

}