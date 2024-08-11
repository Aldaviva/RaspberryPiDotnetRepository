using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Cdn;
using Azure.Storage.Blobs;
using Bom.Squad;
using Org.BouncyCastle.Bcpg;
using PgpCore;
using RaspberryPiDotnetRepository;
using RaspberryPiDotnetRepository.Azure;
using RaspberryPiDotnetRepository.Debian.Package;
using RaspberryPiDotnetRepository.Debian.Repository;
using RaspberryPiDotnetRepository.DotnetUpstream;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Unfucked.PGP;
using Options = RaspberryPiDotnetRepository.Data.Options;
using PGP = Unfucked.PGP.PGP;

#pragma warning disable CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint. - You can safely try to register a singleton with a null value, it just won't register. Just make sure you inject it as nullable.

// apt will get its limbs blown off with "Clearsigned file isn't valid, got 'NOSPLIT' (does the network require authentication?)" if InRelease starts with UTF-8 BOM
BomSquad.DefuseUtf8Bom();

HostApplicationBuilder appConfig = Host.CreateApplicationBuilder(args);
appConfig.Configuration.AlsoSearchForJsonFilesInExecutableDirectory();

appConfig.Logging.AddUnfuckedConsole(options => options.IncludeNamespaces = false);

appConfig.Services
    .Configure<Options>(appConfig.Configuration)

    // Business logic
    .AddSingleton<SdkDownloader, SdkDownloaderImpl>()
    .AddSingleton<PackageRequester, PackageRequesterImpl>()
    .AddSingleton<PackageGenerator, PackageGeneratorImpl>()
    .AddTransient<PackageBuilder, PackageBuilderImpl>()
    .AddSingleton<StatisticsService, StatisticsServiceImpl>()
    .AddSingleton<Indexer, IndexerImpl>()
    .AddSingleton<ExtraFileGenerator, ExtraFileGeneratorImpl>()
    .AddSingleton<ManifestManager, ManifestManagerImpl>()
    .AddHostedService<Orchestrator>()

    // HTTP client
    .AddSingleton(new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 16 }) { Timeout = TimeSpan.FromSeconds(30) })

    // Azure Blob Storage
    .AddSingleton(provider => new BlobServiceClient(provider.options().storageConnection, new BlobClientOptions { Retry = { MaxRetries = 0, NetworkTimeout = TimeSpan.FromMinutes(30) } }))
    .AddSingleton(provider => provider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(provider.options().storageContainerName))
    .AddSingleton<BlobStorageClient, BlobStorageClientImpl>()

    // Azure CDN
    .AddSingleton<ArmClient?>(provider => provider.options() is { cdnTenantId: { } tenantId, cdnClientId: { } clientId, cdnCertFilePath: { } certPath } opts
        ? new ArmClient(new ClientCertificateCredential(tenantId, clientId, new X509Certificate2(certPath, opts.cdnCertPassword))) : null)
    .AddSingleton<CdnEndpointResource?>(provider => provider.options() is { cdnSubscriptionId: { } s, cdnResourceGroup: { } r, cdnProfile: { } p, cdnEndpointName: { } e } ?
        provider.GetService<ArmClient>()?.GetCdnEndpointResource(CdnEndpointResource.CreateResourceIdentifier(s, r, p, e)).Get().AsNullable() : null)
    .AddSingleton<CdnClient, CdnClientImpl>()

    // Facades
    .AddSingleton<IPGP>(provider => new PGP(new EncryptionKeys(File.ReadAllText(provider.options().gpgPrivateKeyPath, Encoding.UTF8), string.Empty)) { HashAlgorithmTag = HashAlgorithmTag.Sha256 });

using IHost app = appConfig.Build();
await app.RunAsync();