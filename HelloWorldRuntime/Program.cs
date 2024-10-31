using System.Runtime.InteropServices;

Console.WriteLine($"Hello from {RuntimeInformation.FrameworkDescription} {RuntimeInformation.RuntimeIdentifier} {RuntimeInformation.OSArchitecture} {(Environment.Is64BitOperatingSystem ? 64 : 32)}-bit");