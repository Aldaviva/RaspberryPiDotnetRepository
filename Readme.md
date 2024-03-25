Raspberry Pi OS .NET APT Package Repository
===

![.NET latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwest.aldaviva.com%2Fraspbian%2Fbadges%2Fdotnet.json&query=%24.latestVersion&logo=dotnet&label=latest%20version&color=success) ![Raspberry Pi OS latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwest.aldaviva.com%2Fraspbian%2Fbadges%2Fraspbian.json&query=%24.latestVersion&logo=raspberrypi&label=latest%20version&color=success)

Public repository of armhf and arm64 APT packages for [.NET](https://dotnet.microsoft.com/) runtimes and SDKs to install on [Raspberry Pis](https://www.raspberrypi.com) running [Raspberry Pi OS (Raspbian)](https://www.raspberrypi.com/software/operating-systems/).

Vendors like [Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian), [Fedora](https://packages.fedoraproject.org/pkgs/dotnet8.0/), and [Ubuntu](https://packages.ubuntu.com/source/mantic/dotnet8) provide official DEB packages for .NET, but none of them support armhf, so they can't be installed on Raspberry Pi OS with the default 32-bit architecture. Microsoft [recommends](https://learn.microsoft.com/en-us/dotnet/iot/deployment) installing .NET on Raspberry Pis using their build-machine–oriented [installation script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script), which does not install system-wide by default, does not update or clean up automatically, and does not handle the concept of installing whatever the latest minor version currently is (you must always choose a release channel like LTS or STS, but there's no simple way for the script to tell which one is newer).

This repository comprises unofficial packages that each contain **official .NET Linux ARM builds by Microsoft**, created from the same exact binaries you get when you install .NET from their installation script or click one of the Linux Arm32 or Arm64 Binaries links on a [.NET download page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2,3,4" bullets="-,1.,-" -->

- [Installation](#installation)
    1. [Add repository](#add-repository)
    1. [Install packages](#install-packages)
        - [Latest version](#latest-version)
        - [Latest LTS version](#latest-lts-version)
        - [Specific minor version](#specific-minor-version)
- [Compatible versions](#compatible-versions)
    1. [Operating systems](#operating-systems)
    1. [CPU architectures](#cpu-architectures)
    1. [Raspberry Pis](#raspberry-pis)
- [List installed versions](#list-installed-versions)
- [Application package dependencies](#application-package-dependencies)

<!-- /MarkdownTOC -->

## Installation

### Add repository

You only have to do this step once per Raspberry Pi.
```sh
sudo wget -q https://west.aldaviva.com/raspbian/aldaviva.gpg.key -O /etc/apt/trusted.gpg.d/aldaviva.gpg
echo "deb https://west.aldaviva.com/raspbian/ $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/aldaviva.list > /dev/null
sudo apt update
```

### Install packages

There are three main package name prefixes to choose from:
- `dotnet-runtime-*` for running command-line applications
- `aspnetcore-runtime-*` for running web applications
- `dotnet-sdk-*` for building applications

There are also three upgrade strategies to choose from, to control which versions the package is allowed to upgrade to.
- [Latest](#latest) (either LTS or STS [Standard Term Support, odd-numbered .NET releases that have 1.5 years of support])
- [Latest LTS](#latest-lts) (Long Term Support, even-numbered .NET releases that have 3 years of support)
- [Specific minor version](#specific-minor-version) (like 8.0 only)

#### Latest version
This will install the latest .NET version, regardless of whether it is an LTS or STS release. It can upgrade to greater major and minor versions, including new STS major versions. It will not install previews or release candidates.

For example, if you install `dotnet-runtime-latest` in March 2024, it will install .NET Runtime 8.0. Later, if you run `apt upgrade` in December 2024, .NET 9.0 will have been released, so it will install .NET Runtime 9.0.

|Installation|Package name|Purpose|Includes|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest`|Build .NET apps|.NET & ASP.NET Core Runtimes|

> [!TIP]
> If you find that a .NET application does not run after a major version upgrade, you can choose a different [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#major-version-runtime-roll-forward) behavior. For example, you can set the `DOTNET_ROLL_FORWARD` environment variable to `LatestMajor`.

#### Latest LTS version
This will install the latest LTS .NET version. It can upgrade to greater major and minor LTS versions. It will never install an STS release, preview, or release candidate.

For example, if you install `dotnet-runtime-latest-lts` in March 2024, it will install .NET Runtime 8.0. Later, if you run `apt upgrade` in December 2024, it will upgrade to the latest 8.0.* release, but not install the newly released .NET 9, because 9 is an STS release. It will stay on .NET 8 until November 2025, when .NET 10 is released, which is an LTS version like 8.

|Installation|Package name|Purpose|Includes|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest-lts`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest-lts`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest-lts`|Build .NET apps|.NET & ASP.NET Core Runtimes|

> [!TIP]
> If you find that a .NET application does not run after a major version upgrade, you can choose a different [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#major-version-runtime-roll-forward) behavior. For example, you can set the `DOTNET_ROLL_FORWARD` environment variable to `LatestMajor`.

#### Specific minor version
If you want to stay on a specific minor version of .NET, such as 8.0, then you can install one of the numbered packages, like `dotnet-runtime-8.0`. This will install .NET Runtime 8.0 and only ever upgrade it to newer patch versions, like 8.0.3, but never to newer major or minor versions like 9.0.0 or 10.0.0. It will not install previews or release candidates.

|Installation|Package name|Purpose|Includes|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-8.0`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-8.0`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-8.0`|Build .NET apps|.NET & ASP.NET Core Runtimes|

> [!NOTE]
> The preceding examples use .NET 8.0. If you want to install a different version instead, then you can replace `8.0` in these examples with another [supported version number](#supported-versions), such as `6.0` or `7.0`.

> [!NOTE]
> The SDK package versions are numbered like the runtime versions they are released in lockstep with, not with the \*.\*.100-based SDK numbering. For example, as of 2024-03-25, the latest .NET 8 SDK package is versioned `8.0.3-0`, not the 8.0.203 version number reported by the SDK once installed.

## Compatible versions

### Operating systems
|Raspberry Pi OS|.NET 6|.NET 7|.NET 8|
|-:|:-:|:-:|:-:|
|Buster (10)|✅|✅|✅|
|Bullseye (11)|✅|✅|✅|
|Bookworm (12)|✅|✅|✅|

### CPU architectures
- ✅ ARM32 (armhf/AArch32/ARMv7, 32-bit)
- ✅ ARM64 (AArch64/ARMv8, 64-bit)

### Raspberry Pis
- ✅ Raspberry Pi 2
- ✅ Raspberry Pi 3
- ✅ Raspberry Pi 4
- ✅ Raspberry Pi 5 or greater
- ✅ Other Raspberry Pis that have an ARMv7 or greater CPU, such as Compute Module 3 and 4, Pi Zero 2 W, and Pi 400
- ⛔ Raspberry Pi 1, Pi Pico, Compute Module 1, and Pi Zero are [not supported by .NET](https://github.com/dotnet/core/issues/1232#issuecomment-359519481) because they only have an [ARMv6 CPU](https://en.wikipedia.org/wiki/Raspberry_Pi#Specifications), and [.NET requires ARMv7 or later](https://learn.microsoft.com/en-us/dotnet/iot/intro#supported-hardware-platforms)

## List installed versions

<details>
    <summary>show output<pre>dotnet --info</pre></summary>

    .NET SDK:
     Version:           8.0.203
     Commit:            5e1ceea679
     Workload version:  8.0.200-manifests.4e94be9c

    Runtime Environment:
     OS Name:     raspbian
     OS Version:  12
     OS Platform: Linux
     RID:         linux-arm
     Base Path:   /usr/share/dotnet/sdk/8.0.203/

    .NET workloads installed:
    There are no installed workloads to display.

    Host:
      Version:      8.0.3
      Architecture: arm
      Commit:       9f4b1f5d66

    .NET SDKs installed:
      8.0.203 [/usr/share/dotnet/sdk]

    .NET runtimes installed:
      Microsoft.AspNetCore.App 8.0.3 [/usr/share/dotnet/shared/Microsoft.AspNetCore.App]
      Microsoft.NETCore.App 8.0.3 [/usr/share/dotnet/shared/Microsoft.NETCore.App]

    Other architectures found:
      None

    Environment variables:
      Not set

    global.json file:
      Not found

    Learn more:
      https://aka.ms/dotnet/info

    Download .NET:
      https://aka.ms/dotnet/download
</details>

## Application package dependencies
If you maintain an APT package for a .NET application, you can declare a dependency on one of the packages in this repository to automatically install the correct .NET runtime when a user installs your package.

It is recommended to declare a dependency on a virtual package with a metapackage alternative. This way, it allows users to use newer .NET versions if they already have one installed. If your application targets a minimum version of .NET Runtime 6, for example, you can add this to your package's `control` file.

```text
Depends: dotnet-runtime-latest | dotnet-runtime-6.0-or-greater
```

If the user already has .NET Runtime 6, 7, or 8 installed, your app can launch with their existing runtime without downloading any new runtimes. Otherwise, the app package installation will automatically include .NET Runtime 8, or whatever the latest version is at the time.

There are virtual packages to represent minor version inequalities for other packages too, not just the above example (like `aspnetcore-6.0-or-greater` and `dotnet-sdk-6.0-or-later`) and other versions (like `dotnet-runtime-7.0-or-greater` and `dotnet-runtime-8.0-or-greater`).

To allow your application to run with newer major runtime versions, be sure to add a [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#major-version-runtime-roll-forward) behavior to your `.csproj` project file or `*.runtimeconfig.json` runtime configuration file.

```xml
<PropertyGroup>
    <RollForward>LatestMajor</RollForward>
</PropertyGroup>
```

```json
{
    "runtimeOptions": {
        "rollForward": "LatestMajor"
    }
}
```