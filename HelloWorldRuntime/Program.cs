using System.Runtime.InteropServices;

Console.WriteLine(
    $"Hello from {RuntimeInformation.FrameworkDescription} {RuntimeInformation.RuntimeIdentifier} {RuntimeInformation.OSArchitecture} {(Environment.Is64BitOperatingSystem ? 64 : 32)}-bit");

using HttpClient          httpClient = new();
using HttpResponseMessage response   = await httpClient.GetAsync("https://aldaviva.com/");
response.EnsureSuccessStatusCode();
Console.WriteLine($"HTTPS request OK (HTTP {response.Version})");