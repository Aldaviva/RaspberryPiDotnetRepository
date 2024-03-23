using System.Runtime.InteropServices;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(8080));
WebApplication webapp = builder.Build();

webapp.MapGet("/", () => $"{RuntimeInformation.FrameworkDescription} {RuntimeInformation.RuntimeIdentifier} {RuntimeInformation.OSArchitecture} {(Environment.Is64BitOperatingSystem ? 64 : 32)}-bit");

webapp.Run();