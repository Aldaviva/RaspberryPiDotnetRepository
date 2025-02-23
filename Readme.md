<img src=".github/images/rpi-dotnet.svg" height="25" alt="logo" /> Raspberry Pi OS .NET APT Package Repository
===

![.NET latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraspbian.aldaviva.com%2Fbadges%2Fdotnet.json&query=%24.latestVersion&logo=dotnet&label=latest%20version&color=success) ![Raspberry Pi OS latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraspbian.aldaviva.com%2Fbadges%2Fraspbian.json&query=%24.latestVersion&logo=raspberrypi&label=latest%20version&color=success) [![GitHub Actions](https://img.shields.io/github/actions/workflow/status/Aldaviva/RaspberryPiDotnetRepository/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/RaspberryPiDotnetRepository/actions/workflows/dotnet.yml)

This public [APT repository](https://github.com/Aldaviva/RaspberryPiDotnetRepository/wiki/Debian-APT-package-repository-format) supplies armhf and arm64 .deb packages of [.NET](https://dotnet.microsoft.com/) runtimes and SDKs to install on [Raspberry Pis](https://www.raspberrypi.com/products/) running [Raspberry Pi OS/Raspbian](https://www.raspberrypi.com/software/operating-systems/).

Vendors like [Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian), [Red Hat](https://packages.fedoraproject.org/pkgs/dotnet8.0/), and [Canonical](https://packages.ubuntu.com/noble/dotnet8) provide official .deb packages for .NET, but none of them support armhf, so they can't be installed on Raspberry Pi OS with the default 32-bit architecture. Microsoft [recommends](https://learn.microsoft.com/en-us/dotnet/iot/deployment) installing .NET on Raspberry Pis using their build-machine–oriented [installation script](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install), which neither installs system-wide without extra manual steps, nor automatically updates or cleans up previous versions, nor lets you install the latest version without you having to manually look up whether STS or LTS is currently newer.

This repository comprises unofficial packages that install **official .NET Linux ARM releases built by Microsoft**, created from the exact same Linux ARM binary archives that the [official .NET download pages](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), [release notes](https://github.com/dotnet/core/blob/main/release-notes/8.0/8.0.3/8.0.3.md#downloads), and [installation script](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install) use.

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
    1. [List installed versions](#list-installed-versions)
- [Compatible versions](#compatible-versions)
    1. [Operating systems and .NET releases](#operating-systems-and-net-releases)
    1. [CPU architectures](#cpu-architectures)
    1. [Raspberry Pis](#raspberry-pis)
- [Developer information](#developer-information)
    1. [Application package dependencies](#application-package-dependencies)
    1. [DEB package and APT repository formats](#deb-package-and-apt-repository-formats)

<!-- /MarkdownTOC -->

## Installation

### Add APT repository

You only have to do this step once per Raspberry Pi OS installation.
```sh
wget -qO- https://raspbian.aldaviva.com/addrepo.sh | sh
```

Alternatively, you may do this step manually.
```sh
sudo wget -q https://raspbian.aldaviva.com/aldaviva.gpg.key -O /etc/apt/trusted.gpg.d/aldaviva.gpg
echo "deb https://raspbian.aldaviva.com/ $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/aldaviva.list > /dev/null
sudo apt update
```

The OpenPGP key fingerprint is [`B3BF3504BBD0A81DD82A8DFB45D66F054AB9A66A`](https://keys.openpgp.org/search?q=B3BF3504BBD0A81DD82A8DFB45D66F054AB9A66A). You may verify this with 
```sh
gpg --show-keys /etc/apt/trusted.gpg.d/aldaviva.gpg
```

### Install package

First, to install a package, choose the package name you want. The name is the concatenation of a name prefix and a versioning suffix. For example, if you want the latest version of the .NET Runtime, the package name would be `dotnet-runtime-latest`, which you could install by running

```sh
sudo apt install dotnet-runtime-latest
```

See the following sections for explanations all the package name possibilities.

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
<!-- Add new releases here -->
<li><code>9.0</code></li>
<li><code>8.0</code></li>
<li><code>7.0</code></li>
<li><code>6.0</code></li>
</ul></td>
</tr>
</tbody>
</table>

There are three package type prefixes to choose from:
- **`dotnet-runtime-`** for running command-line applications
- **`aspnetcore-runtime-`** for running web applications
- **`dotnet-sdk-`** for building applications

There are also three types of version specification suffixes to choose from, which control the versions that the package should install and allow upgrades to.
- [**`latest`**](#latest-version) installs the LTS or STS release with the greatest version number
- [**`latest-lts`**](#latest-lts-version) installs the LTS release with the greatest version number
- [**Specific minor versions**](#specific-minor-version) install and stick with one release permanently, like 8.0.*, only installing patch updates like 8.0.1

> [!NOTE]
> [*Long-Term Support (LTS)*](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#cadence) versions like 8.0 are released each November of odd-numbered years like 2023, have even major version numbers, and come with 3 years of support.<br>
> *Standard Term Support (STS)* versions like 7.0 are released each November of even-numbered years like 2022, with odd version numbers and 1.5 years of support.

Then, once you know which package you want, you can install it with `apt install <packagename>`, for example, **`sudo apt install dotnet-runtime-latest`**.

> [!TIP]
> Multiple .NET packages can be safely installed at the same time, even from different versions. For example, you can have both .NET 6 Runtime and .NET 8 Runtime installed side-by-side without causing a conflict. At run time, the .NET host framework resolver will choose the correct .NET runtime with which to launch each app based on Roll Forward settings and the app's target framework.

> [!TIP]
> If you find that a .NET application does not run after a major version upgrade, you can choose a different [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/versions/selection#framework-dependent-apps-roll-forward) behavior. For example, you can set the `DOTNET_ROLL_FORWARD` environment variable to `LatestMajor`.

#### Latest version
This will install the latest .NET version, regardless of whether it is an LTS or STS release. It will upgrade to greater major and minor versions, including new STS versions. It will never install previews or release candidates.

For example, if you `apt install dotnet-runtime-latest` in March 2024, it will install .NET Runtime 8. Later, if you run `apt upgrade` in December 2024, .NET 9 will have been released, so it will install .NET Runtime 9.

|Installation|Package name|Purpose|Also auto-installs|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest`|Build .NET apps|.NET & ASP.NET Core Runtimes|

#### Latest LTS version
This will install the latest Long Term Support .NET version. It can upgrade to greater major and minor LTS versions. It will never install an STS, release candidate, or preview release.

For example, if you `apt install dotnet-runtime-latest-lts` in March 2024, it will install .NET Runtime 8. Later, if you run `apt upgrade` in December 2024, it will upgrade to the latest 8.0.* release, but will not install the newly released .NET 9, because 9 is an STS release. It will stay on .NET 8 until November 2025, when .NET 10 is released, which is an LTS version like 8.

|Installation|Package name|Purpose|Also auto-installs|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest-lts`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest-lts`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest-lts`|Build .NET apps|.NET & ASP.NET Core Runtimes|

#### Specific minor version
If you want to stay on a specific minor version of .NET, such as 8.0, then you can `apt install dotnet-runtime-8.0` or one of the other numbered packages. This example will install .NET Runtime 8.0 and only ever upgrade it to newer patch versions, like 8.0.3, but never to newer major or minor versions like 9.0 or 10.0. It will not install previews or release candidates either.

|Installation|Package names|Purpose|Also auto-installs|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-9.0`<br>`dotnet-runtime-8.0`<br>`dotnet-runtime-7.0`<br>`dotnet-runtime-6.0`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-9.0`<br>`aspnetcore-runtime-8.0`<br>`aspnetcore-runtime-7.0`<br>`aspnetcore-runtime-6.0`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-9.0`<br>`dotnet-sdk-8.0`<br>`dotnet-sdk-7.0`<br>`dotnet-sdk-6.0`|Build .NET apps|.NET & ASP.NET Core Runtimes|
<!-- Add new releases here -->

### Update installed packages

#### Patch versions

When a new .NET patch version is released, you can update the installed packages to the new version.

```sh
sudo apt update
sudo apt upgrade
```

> [!IMPORTANT]  
> Be sure to restart any running .NET applications after installing a new version of the runtime they were using, or else they may mysteriously crash much later when a dynamically-loaded file cannot be found in an old, now-deleted versioned directory.

#### Major and minor versions

**Latest or Latest LTS installed:** If you want to update to a new major or minor version, you will need to have installed one of the [`latest[-lts]`](#latest-version) packages installed, such as `dotnet-runtime-latest` or `aspnetcore-runtime-latest-lts`, before you `apt update && apt upgrade`. You may clean up previous versions afterwards using `sudo apt autoremove`, or at installation time using `sudo apt upgrade --autoremove`.

**Specific minor version installed:** If you aren't using a `latest[-lts]` package, you can manually choose a new minor version to install using a command like `sudo apt install dotnet-runtime-8.0`. Afterwards, you may clean up previous versions using a command like `sudo apt remove dotnet-runtime-7.0`.

#### Automatic updates

To automatically install package updates without any user interaction, see [Debian Reference § 2.7.3: Automatic download and upgrade of packages](https://www.debian.org/doc/manuals/debian-reference/ch02.en.html#_automatic_download_and_upgrade_of_packages).

### List installed versions

```bash
dotnet --info
```
```bash
apt list --installed 'dotnet-*' 'aspnetcore-runtime-*'
```

## Compatible versions

### Operating systems and .NET releases
<!-- Add new releases here -->
|Raspberry Pi OS|OS CPU architecture|.NET 9|.NET 8|.NET 7|.NET 6|
|-:|-:|:-|:-|:-|:-|
|Bookworm (12)|ARM64|✅|✅|☑<sup>1</sup>|☑<sup>1</sup>|
|Bookworm (12)|ARM32|✅|✅|☑<sup>1</sup>|☑<sup>1</sup>|
|Bullseye (11)|ARM64|☑<sup>2</sup>|✅|☑<sup>1</sup>|☑<sup>1</sup>|
|Bullseye (11)|ARM32|❌<sup>3</sup>|✅|☑<sup>1</sup>|☑<sup>1</sup>|
|Buster (10)|ARM64|☑<sup>2</sup>|☑<sup>2</sup>|☑<sup>1</sup>|☑<sup>1</sup>|
|Buster (10)|ARM32|❌<sup>3</sup>|☑<sup>2</sup>|☑<sup>1</sup>|☑<sup>1</sup>|

✅ = Available, compatible, and currently officially supported<br>
☑ = Available and compatible, but not currently officially supported<br>
❌ = Unavailable, incompatible, and unsupported

> [!NOTE]
> 1. [This older version of .NET is no longer updated or supported by Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), although it still works.
> 1. [This combination of .NET and Debian versions was never supported by Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#supported-distributions), although it does work.
> 1. [.NET 9 on ARM32 Linux requires a newer version of glibc/libc6 (2.34)](https://github.com/dotnet/core/blob/main/release-notes/9.0/supported-os.md#linux-compatibility) than is provided by Debian 10 ([2.28](https://packages.debian.org/buster/libc6)) or 11 ([2.31](https://packages.debian.org/bullseye/libc6)), where the runtime will crash on launch.

##### Release information
- [Raspberry Pi OS releases](https://www.raspberrypi.com/software/operating-systems/) and [hardware compatibility](https://en.wikipedia.org/wiki/Raspberry_Pi_OS#Releases)
- [Debian releases](https://www.debian.org/releases/) and [details](https://wiki.debian.org/DebianReleases)
- [.NET releases](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#lifecycle), [details](https://github.com/dotnet/core/blob/main/releases.md), [Release Policies](https://github.com/dotnet/core/blob/main/release-policies.md), and [Supported OS Policy](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md)

##### Other OS distributions
In addition to Raspberry Pi OS, you should also be able to install these .deb packages on ARM builds of [Debian](https://raspi.debian.net) and other Debian-based distributions like [Ubuntu](https://ubuntu.com/download/raspberry-pi) and [Mobian](https://wiki.debian.org/Mobian/), because these packages are not specific to Raspberry Pi OS and only [depend on packages in the standard Debian repository](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies).

### CPU architectures
✅ 64-bit ARM64/AArch64/ARMv8<br>
✅ 32-bit ARM32/AArch32/ARMv7/armhf

### Raspberry Pis
✅ [Raspberry Pi 5](https://www.raspberrypi.com/products/raspberry-pi-5/)<br>
✅ [Raspberry Pi 4](https://www.raspberrypi.com/products/raspberry-pi-4-model-b/)<br>
✅ [Raspberry Pi 3](https://www.raspberrypi.com/products/raspberry-pi-3-model-b/)<br>
✅ [Raspberry Pi 2](https://www.raspberrypi.com/products/raspberry-pi-2-model-b/) (64-bit OS requires v1.2, not v1.1)<br>
✅ Other Raspberry Pis that have an ARMv7 or greater CPU, such as [Pi Pico 2](https://www.raspberrypi.com/products/raspberry-pi-pico-2/), [Compute Module 3](https://www.raspberrypi.com/products/compute-module-3-plus/) and [4](https://www.raspberrypi.com/products/compute-module-4/), [Pi Zero 2 W](https://www.raspberrypi.com/products/raspberry-pi-zero-2-w/), and [Pi 400](https://www.raspberrypi.com/products/raspberry-pi-400-unit/)<br>
⛔ [Raspberry Pi 1](https://www.raspberrypi.com/products/raspberry-pi-1-model-b-plus/), [Pi Pico](https://www.raspberrypi.com/products/raspberry-pi-pico/), [Compute Module 1](https://www.raspberrypi.com/products/compute-module-1/), and [Pi Zero](https://www.raspberrypi.com/products/raspberry-pi-zero/) are [_**not compatible with .NET**_](https://github.com/dotnet/core/issues/1232#issuecomment-359519481) because they only have an [ARMv6 CPU](https://en.wikipedia.org/wiki/Raspberry_Pi#Specifications), and [.NET requires ARMv7 or later](https://learn.microsoft.com/en-us/dotnet/iot/intro#supported-hardware-platforms)

## Developer information
### Application package dependencies
If you are a developer who maintains an APT package for a .NET application, you can declare a dependency on one of the packages in this repository to automatically install the correct .NET runtime when a user installs your package.

It is recommended to declare a dependency on a virtual package with a metapackage alternative. This way, it allows users to use newer .NET versions if they already have one installed. If your application targets a minimum version of .NET Runtime 6, for example, you can add this to your package's `control` file.

```text
Depends: dotnet-runtime-latest | dotnet-runtime-6.0-or-greater
```

If the user already has .NET Runtime 6, 7, or 8 installed, your app can launch with their existing runtime without downloading any new runtimes. Otherwise, the app package installation will automatically include .NET Runtime 8, or whatever the latest version is at the time.

There are virtual packages to represent minor version inequalities for other packages too, not just the above example (like `aspnetcore-6.0-or-greater` and `dotnet-sdk-6.0-or-greater`) and other versions (like `dotnet-runtime-7.0-or-greater` and `dotnet-runtime-8.0-or-greater`).

To allow your application to run with newer major runtime versions, be sure to add a [Roll Forward](https://learn.microsoft.com/en-us/dotnet/core/versions/selection#framework-dependent-apps-roll-forward) behavior to your `.csproj` project file or `*.runtimeconfig.json` runtime configuration file.

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

### DEB package and APT repository formats
To learn how to create your own DEB packages and serve them in an APT repository, you can refer to [Debian APT package repository format](https://github.com/Aldaviva/RaspberryPiDotnetRepository/wiki/Debian-APT-package-repository-format). The formats are confusing, misguided, and poorly designed, while their documentation is scattered and complex. This wiki page is an effort to accurately distill the steps to create simple repositories into the relevant information that covers normal cases while still being precise and avoiding common problems.
