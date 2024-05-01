<img src=".github/images/rpi-dotnet.svg" height="25" alt="logo" /> Raspberry Pi OS .NET APT Package Repository
===

![.NET latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwest.aldaviva.com%2Fraspbian%2Fbadges%2Fdotnet.json&query=%24.latestVersion&logo=dotnet&label=latest%20version&color=success) ![Raspberry Pi OS latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwest.aldaviva.com%2Fraspbian%2Fbadges%2Fraspbian.json&query=%24.latestVersion&logo=raspberrypi&label=latest%20version&color=success) [![GitHub Actions](https://img.shields.io/github/actions/workflow/status/Aldaviva/RaspberryPiDotnetRepository/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/RaspberryPiDotnetRepository/actions/workflows/dotnet.yml)

Public repository of armhf and arm64 APT packages for [.NET](https://dotnet.microsoft.com/) runtimes and SDKs to install on [Raspberry Pis](https://www.raspberrypi.com) running [Raspberry Pi OS (Raspbian)](https://www.raspberrypi.com/software/operating-systems/).

Vendors like [Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian), [Fedora](https://packages.fedoraproject.org/pkgs/dotnet8.0/), and [Ubuntu](https://packages.ubuntu.com/source/mantic/dotnet8) provide official DEB packages for .NET, but none of them support armhf, so they can't be installed on Raspberry Pi OS with the default 32-bit architecture. Microsoft [recommends](https://learn.microsoft.com/en-us/dotnet/iot/deployment) installing .NET on Raspberry Pis using their build-machine–oriented [installation script](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install), which neither installs system-wide without extra manual steps, nor automatically updates or cleans up previous versions, nor lets you install the latest minor version without you manually looking up whether STS or LTS is currently newer.

This repository comprises unofficial packages that install **official .NET Linux ARM releases built by Microsoft**, created from the exact same Linux ARM binary archives that are linked to by the [official .NET download pages](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), [release notes](https://github.com/dotnet/core/blob/main/release-notes/8.0/8.0.3/8.0.3.md#downloads), and [installation script](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install).

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2,3,4" bullets="-,1.,-" -->

- [Installation](#installation)
    1. [Add APT repository](#add-apt-repository)
    1. [Install package](#install-package)
        - [Latest version](#latest-version)
        - [Latest LTS version](#latest-lts-version)
        - [Specific minor version](#specific-minor-version)
    1. [Update installed packages](#update-installed-packages)
        - [Patch versions](#patch-versions)
        - [Major and minor versions](#major-and-minor-versions)
        - [Automatic updates](#automatic-updates)
- [Compatible versions](#compatible-versions)
    1. [Operating systems and .NET releases](#operating-systems-and-net-releases)
    1. [CPU architectures](#cpu-architectures)
    1. [Raspberry Pis](#raspberry-pis)
- [List installed versions](#list-installed-versions)
- [Application package dependencies](#application-package-dependencies)

<!-- /MarkdownTOC -->

## Installation

### Add APT repository

You only have to do this step once per Raspberry Pi.
```sh
sudo wget -q https://raspbian.aldaviva.com/aldaviva.gpg.key -O /etc/apt/trusted.gpg.d/aldaviva.gpg
echo "deb https://raspbian.aldaviva.com/ $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/aldaviva.list > /dev/null
sudo apt update
```

The OpenPGP key fingerprint is `B3BF 3504 BBD0 A81D D82A  8DFB 45D6 6F05 4AB9 A66A`.

### Install package

First, to install a package, choose the package name you want. The name is the concatenation of a name prefix and a versioning suffix. For example, if you want the latest version of the .NET Runtime, the package name would be `dotnet-runtime-latest`. See the following sections for explanations all the package name possibilities.

<table>
<thead>
<th colspan="2">Package name format</th>
<thead>

<tbody>
<tr>
<td colspan="2" align="center"><code>dotnet-runtime-latest</code></td>
</tr>

<tr>
<td align="center">↗️<br><strong>package type</strong></td>
<td align="center">↖️<br><strong>version spec</strong></td>
</tr>

<tr>
<td valign="top"><ul>
<li><code>dotnet-runtime-</code></li>
<li><code>aspnetcore-runtime-</code></li>
<li><code>dotnet-sdk-</code></li>
</ul></td>
<td valign="top"><ul>
<li><code>latest</code></li>
<li><code>latest-lts</code></li>
<li><code>8.0</code></li>
<li><code>7.0</code></li>
<li><code>6.0</code></li>
</ul></td>
</tr>
</tbody>
</table>

There are three package type prefixes to choose from:
- `dotnet-runtime-` for running command-line applications
- `aspnetcore-runtime-` for running web applications
- `dotnet-sdk-` for building applications

There are also three version specification suffixes to choose from, to control which versions the package should install and allow upgrades to.
- [`latest`](#latest-version) installs the LTS or STS release with the greatest version number
- [`latest-lts`](#latest-lts-version) installs the LTS release with the greatest version number
- [Specific minor versions](#specific-minor-version) install and stick with one release permanently, like 8.0.*, only installing patch updates

> [!NOTE]
> [*Long-Term Support (LTS)*](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#cadence) versions like 8.0 are released every other November with even major version numbers, and come with 3 years of support. *Standard Term Support (STS)* versions like 7.0 are released in the alternate Novembers with odd version numbers and 1.5 years of support.

Then, once you know which package you want, you can install it with `apt install <packagename>`, for example, `sudo apt install dotnet-runtime-latest`.

> [!TIP]
> Multiple .NET packages can be safely installed at the same time, even from different versions. For example, you can have both .NET 6 Runtime and .NET 8 Runtime installed side-by-side without causing a conflict. At run time, the .NET host framework resolver will choose the correct .NET runtime with which to launch each app based on Roll Forward settings and the app's target framework.

#### Latest version
This will install the latest .NET version, regardless of whether it is an LTS or STS release. It will upgrade to greater major and minor versions, including new STS versions. It will never install previews or release candidates.

For example, if you `apt install dotnet-runtime-latest` in March 2024, it will install .NET Runtime 8. Later, if you run `apt upgrade` in December 2024, .NET 9 will have been released, so it will install .NET Runtime 9.

|Installation|Package name|Purpose|Includes|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest`|Build .NET apps|.NET & ASP.NET Core Runtimes|

> [!TIP]
> If you find that a .NET application does not run after a major version upgrade, you can choose a different [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#major-version-runtime-roll-forward) behavior. For example, you can set the `DOTNET_ROLL_FORWARD` environment variable to `LatestMajor`.

#### Latest LTS version
This will install the latest Long Term Support .NET version. It can upgrade to greater major and minor LTS versions. It will never install an STS, release candidate, or preview release.

For example, if you `apt install dotnet-runtime-latest-lts` in March 2024, it will install .NET Runtime 8. Later, if you run `apt upgrade` in December 2024, it will upgrade to the latest 8.0.* release, but will not install the newly released .NET 9, because 9 is an STS release. It will stay on .NET 8 until November 2025, when .NET 10 is released, which is an LTS version like 8.

|Installation|Package name|Purpose|Includes|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest-lts`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest-lts`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest-lts`|Build .NET apps|.NET & ASP.NET Core Runtimes|

> [!TIP]
> If you find that a .NET application does not run after a major version upgrade, you can choose a different [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#major-version-runtime-roll-forward) behavior. For example, you can set the `DOTNET_ROLL_FORWARD` environment variable to `LatestMajor`.

#### Specific minor version
If you want to stay on a specific minor version of .NET, such as 8.0, then you can `apt install dotnet-runtime-8.0` or one of the other numbered packages. This example will install .NET Runtime 8.0 and only ever upgrade it to newer patch versions, like 8.0.3, but never to newer major or minor versions like 9.0 or 10.0. It will not install previews or release candidates either.

|Installation|Package names|Purpose|Includes|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-8.0`<br>`dotnet-runtime-7.0`<br>`dotnet-runtime-6.0`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-8.0`<br>`aspnetcore-runtime-7.0`<br>`aspnetcore-runtime-6.0`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-8.0`<br>`dotnet-sdk-7.0`<br>`dotnet-sdk-6.0`|Build .NET apps|.NET & ASP.NET Core Runtimes|

> [!NOTE]
> The SDK package versions are numbered like the runtime versions they are released in lockstep with, not with the \*.\*.100-based SDK numbering. For example, as of 2024-03-25, the latest .NET 8 SDK package is versioned `8.0.3-0`, not the 8.0.203 version number reported by the SDK once installed.

### Update installed packages

#### Patch versions

When a new .NET patch version is released, you can update the installed packages to the new version.

```sh
sudo apt update
sudo apt upgrade
```

#### Major and minor versions

If you want to update to a new major or minor version, you will need to have installed one of the [`latest`](#latest-version) packages, such as `dotnet-runtime-latest` or `aspnetcore-runtime-latest-lts`, before you `apt update`.

Alternatively, you can manually choose a new minor version to install using a command like `sudo apt install dotnet-runtime-8.0`.

#### Automatic updates

To automatically install package updates without any user interaction, see [Debian Reference § 2.7.3: Automatic download and upgrade of packages](https://www.debian.org/doc/manuals/debian-reference/ch02.en.html#_automatic_download_and_upgrade_of_packages).

## Compatible versions

### Operating systems and .NET releases
|Raspberry Pi OS|.NET 8|.NET 7|.NET 6|
|-:|:-:|:-:|:-:|
|Bookworm (12)|✅|✅|✅|
|Bullseye (11)|✅|✅|✅|
|Buster (10)|✅|✅|✅|

[Raspberry Pi OS releases](https://www.raspberrypi.com/software/operating-systems/)<br>
[Debian releases](https://www.debian.org/releases/)<br>
[.NET releases](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#lifecycle)

In addition to Raspberry Pi OS, you should also be able to install these DEB packages on ARM builds of Debian and other Debian-based distributions like Ubuntu and Mobian, because these packages are not specific to Raspberry Pi OS and only [depend on packages in the standard Debian repository](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies).

### CPU architectures
- ✅ ARM64 (AArch64/ARMv8, 64-bit)
- ✅ ARM32 (armhf/AArch32/ARMv7, 32-bit)

### Raspberry Pis
- ✅ Raspberry Pi 5 or greater
- ✅ Raspberry Pi 4
- ✅ Raspberry Pi 3
- ✅ Raspberry Pi 2
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
