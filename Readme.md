<img src=".github/images/rpi-dotnet.svg" height="25" alt="logo" /> Raspberry Pi OS .NET APT Package Repository
===

![latest .NET version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraspbian.aldaviva.com%2Fbadges%2Fdotnet.json&query=%24.latestVersion&logo=dotnet&label=latest%20version&color=success) ![latest Raspberry Pi OS version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraspbian.aldaviva.com%2Fbadges%2Fraspbian.json&query=%24.latestVersion&logo=raspberrypi&label=latest%20version&color=success) [![GitHub Actions](https://img.shields.io/github/actions/workflow/status/Aldaviva/RaspberryPiDotnetRepository/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/RaspberryPiDotnetRepository/actions/workflows/dotnet.yml)

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
- [Alternatives](#alternatives)
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

Alternatively, you may do this step manually with the following commands.
```sh
sudo wget -q https://raspbian.aldaviva.com/aldaviva.gpg.key -O /etc/apt/trusted.gpg.d/aldaviva.gpg
echo "deb https://raspbian.aldaviva.com/ $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/aldaviva.list > /dev/null
sudo apt-get update
```

The OpenPGP key fingerprint is [`B3BF3504BBD0A81DD82A8DFB45D66F054AB9A66A`](https://keys.openpgp.org/search?q=B3BF3504BBD0A81DD82A8DFB45D66F054AB9A66A). You may verify this with 
```sh
gpg --show-keys /etc/apt/trusted.gpg.d/aldaviva.gpg
```

### Install package

First, to install a package, choose the package name you want. The name is the concatenation of a type prefix and a versioning suffix. For example, if you want the latest version of the .NET Runtime, the package name would be `dotnet-runtime-latest`, which you could install by running

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
> Version numbers are described with the nomenclature *major*.*minor*.*patch*. For example, .NET 9.0.4 has a major version of 9, a minor version of 0, and a patch version of 4.

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

For example, if you `apt install dotnet-runtime-latest` in March 2024, it will install .NET Runtime 8. Later, if you run `apt full-upgrade` in December 2024, .NET 9 will have been released, so it will install .NET Runtime 9.

|Installation|Package name|Purpose|Also auto-installs|
|-|-|-|-|
|.NET Runtime|`dotnet-runtime-latest`|Run .NET CLI apps||
|ASP.NET Core Runtime|`aspnetcore-runtime-latest`|Run .NET web apps|.NET Runtime|
|.NET SDK|`dotnet-sdk-latest`|Build .NET apps|.NET & ASP.NET Core Runtimes|

#### Latest LTS version
This will install the latest Long Term Support .NET version. It can upgrade to greater major and minor LTS versions. It will never install an STS, release candidate, or preview release.

For example, if you `apt install dotnet-runtime-latest-lts` in March 2024, it will install .NET Runtime 8. Later, if you run `apt full-upgrade` in December 2024, it will upgrade to the latest 8.0.* release, but will not install the newly released .NET 9, because 9 is an STS release instead of LTS. It will stay on .NET 8 until November 2025, when .NET 10 is released, which will be installed because it's an LTS version like 8.

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
sudo apt-get update
sudo apt-get full-upgrade
```

> [!IMPORTANT]  
> Be sure to restart any running .NET applications after installing a new version of the runtime they were using, or else they may mysteriously crash much later when a dynamically-loaded file cannot be found in an old, now-deleted versioned directory.

#### Major and minor versions

##### Latest or Latest LTS installed
If you want to update to a new major or minor version, you will need to have installed one of the [`latest[-lts]`](#latest-version) packages installed, such as `dotnet-runtime-latest` or `aspnetcore-runtime-latest-lts`, before you `sudo apt-get update && sudo apt-get full-upgrade`.

Using `full-upgrade` instead of `upgrade` is recommended because `full-upgrade` allows removal of packages and thus enables major and minor version upgrades, in addition to patch upgrades. The difference is shown in the following example where you had .NET Runtime 9.0.10 installed with `dotnet-runtime-latest` when .NET 10.0.0 was released on 2025-11-11.
- **`apt-get full-upgrade`**: installs .NET Runtime **10.0.0**, removes .NET Runtime 9.0.10
- **`apt-get upgrade`**: installs .NET Runtime **9.0.11**, removes .NET Runtime 9.0.10

If any unneeded, automatically installed packages are left installed after upgrading, you may remove them with `sudo apt autoremove`.

##### Specific minor version installed
If you aren't using a `latest[-lts]` package, you can manually choose a new minor version to install using a command like `sudo apt install dotnet-runtime-8.0`. Afterwards, you may clean up previous versions using a command like `sudo apt remove dotnet-runtime-7.0`.

#### Automatic updates

To automatically install package updates without any user interaction, see [Debian Reference § 2.7.3: Automatic download and upgrade of packages](https://www.debian.org/doc/manuals/debian-reference/ch02.en.html#_automatic_download_and_upgrade_of_packages) and [this quick summary](https://gist.github.com/Aldaviva/9db64e47324f467a7c9b7e468a454c76#file-debian-autoupdate-md). This uses the `apt full-upgrade` [behavior](#latest-or-latest-lts-installed), which will install new major or minor versions if you have one of the `-latest` packages installed, and will automatically remove the old version.

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
|Raspberry Pi OS|OS architecture|.NET 9|.NET 8|.NET 7|.NET 6|
|-:|-:|:-|:-|:-|:-|
|Trixie (13)|ARM64|✅|✅|☑<sup>1</sup>|☑<sup>1</sup>|
|Trixie (13)|ARM32|✅|❌<sup>4</sup>|❌<sup>4</sup>|☑<sup>1</sup>|
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
> 1. [Due to Y2038 compatibility](https://github.com/dotnet/core/discussions/9285), [.NET &ge; 9 on ARM32 Linux requires a newer version of glibc/libc6 (&ge; 2.34)](https://github.com/dotnet/core/blob/main/release-notes/9.0/supported-os.md#linux-compatibility) than is provided by Debian 10 ([2.28](https://packages.debian.org/buster/libc6)) or 11 ([2.31](https://packages.debian.org/bullseye/libc6)), where the runtime will crash on launch.
> 1. [Due to Y2038 compatibility](https://github.com/dotnet/runtime/issues/101444), .NET 7–8 will throw an `AuthenticationException` from HTTPS connections because of the newer glibc and OpenSSL dependencies in ARM32 Debian &ge; 13.

##### Release information
- [Raspberry Pi OS releases](https://www.raspberrypi.com/software/operating-systems/) and [hardware compatibility](https://en.wikipedia.org/wiki/Raspberry_Pi_OS#Releases)
- [Debian releases](https://www.debian.org/releases/) and [details](https://wiki.debian.org/DebianReleases)
- [.NET releases](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#lifecycle), [details](https://github.com/dotnet/core/blob/main/releases.md), [Release Policies](https://github.com/dotnet/core/blob/main/release-policies.md), and [Supported OS Policy](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md)

##### Other OS distributions
In addition to Raspberry Pi OS, you should also be able to install these .deb packages on ARM builds of [Debian](https://raspi.debian.net) and other Debian-based distributions like [Ubuntu](https://ubuntu.com/download/raspberry-pi) and [Mobian](https://wiki.debian.org/Mobian/), because these packages are not specific to Raspberry Pi OS and only [depend on packages in the standard Debian repository](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies).

### CPU architectures
✅ 64-bit ARMv8/ARM64/AArch64<br>
✅ 32-bit ARMv7/ARM32/AArch32/armhf<br>
❌ 32-bit ARMv6

### Raspberry Pis
✅ [Raspberry Pi 5](https://www.raspberrypi.com/products/raspberry-pi-5/)<br>
✅ [Raspberry Pi 4](https://www.raspberrypi.com/products/raspberry-pi-4-model-b/)<br>
✅ [Raspberry Pi 3](https://www.raspberrypi.com/products/raspberry-pi-3-model-b/)<br>
✅ [Raspberry Pi 2](https://www.raspberrypi.com/products/raspberry-pi-2-model-b/) (64-bit OS requires Pi 2 v1.2, not v1.1)<br>
✅ Other Raspberry Pis that have an ARMv7 or greater CPU, such as [Compute Module 3](https://www.raspberrypi.com/products/compute-module-3-plus/), [4](https://www.raspberrypi.com/products/compute-module-4/), and [5](https://www.raspberrypi.com/products/compute-module-5/); [Pi Zero 2 W](https://www.raspberrypi.com/products/raspberry-pi-zero-2-w/); and [Pi 400](https://www.raspberrypi.com/products/raspberry-pi-400-unit/), [500](https://www.raspberrypi.com/products/raspberry-pi-500/), and [500+](https://www.raspberrypi.com/products/raspberry-pi-500-plus/)<br>
❌ [Raspberry Pi Pico](https://www.raspberrypi.com/products/raspberry-pi-pico/) and [Pico 2](https://www.raspberrypi.com/products/raspberry-pi-pico-2/) are _**not compatible with .NET**_ because they don't run Linux, and only support embedded C, C++, and Python<br>
❌ [Raspberry Pi 1](https://www.raspberrypi.com/products/raspberry-pi-1-model-b-plus/), [Compute Module 1](https://www.raspberrypi.com/products/compute-module-1/), and [Pi Zero](https://www.raspberrypi.com/products/raspberry-pi-zero/) are [_**not compatible with .NET**_](https://github.com/dotnet/core/issues/1232#issuecomment-359519481) because they only have an [ARMv6 CPU](https://en.wikipedia.org/wiki/Raspberry_Pi#Specifications), and [.NET requires ARMv7 or later](https://learn.microsoft.com/en-us/dotnet/iot/intro#supported-hardware-platforms)

## Alternatives
Here are other ways to run .NET applications on a Raspberry Pi besides installing the packages from this repository.
- **Add the [Microsoft Linux Package Repositories from PMC (packages.microsoft.com)](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#debian-12)**
    ```sh
    wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    ```
    ✅ [Hosted by Microsoft](https://github.com/microsoft/linux-package-repositories)<br>
    ❌ [Not available until .NET 10 is released in November 2025](https://github.com/dotnet/runtime/issues/3298#issuecomment-2573369838)<br>
    ❌ Does not support .NET 9 or earlier<br>
    ❌ Does not support Debian 11 or earlier<br>
    ❌ Does not support armhf operating systems like 32-bit Raspberry Pi OS, even if your CPU architecture is arm64

- **Run the [dotnet-install script](https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install)**
    ```sh
    wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
    sudo bash dotnet-install.sh --channel LTS --runtime dotnet --install-dir /usr/share/dotnet/ # channel must be LTS, STS, or a minor version
    sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
    rm dotnet-install.sh
    ```
    ✅ [Hosted by Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/scripts)<br>
    ❌ Must re-run `dotnet-install.sh` each time you want to update .NET<br>
    ❌ If you want the latest version, you must have prior knowledge of which channel, LTS or STS, is the latest at any given time, which is difficult to automate<br>
    ❌ More steps to make a system-wide installation, since this tool is meant for temporary non-root cases like CI build machines

- **Bundle the runtime inside each app, instead of installing the runtime system-wide with a package or script**
    ```sh
    dotnet publish -r linux-arm -p:PublishSingleFile=true --self-contained # runtime must be linux-arm or linux-arm64
    ```
    ✅ If you want to make your app smaller and start faster with [Native AOT compilation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/), then this self-contained publishing is required, although you can also publish self-contained apps without Native AOT<br>
    ❌ Installing multiple .NET JIT apps takes more time and storage space, and wears out your SD card faster<br>
    ❌ Updating the runtime requires recompiling and reinstalling all .NET apps running on the system, instead of just running `apt full-upgrade` once<br>
    ❌ Only useful for cross-compilation from another machine, and does not let you install the SDK on a Raspberry Pi, if you wanted it<br>
    ❌ Native AOT has extremely limited reflection support and is incompatible with many libraries, especially for deserialization<br>
    ❌ Native AOT compilation takes a longer time (self-contained JIT publishing is slow, and AOT compilation is even slower)<br>
    ❌ Native AOT compilation requires you to install the Microsoft Visual C++ compiler and Windows SDK on your development machine, which take up a lot of space and aren't needed for normal .NET development

- **Install an alternative operating system distribution**<br>
    ✅ [**Fedora** can run on Raspberry Pis](https://docs.fedoraproject.org/en-US/quick-docs/raspberry-pi/) and [provides official ARM64 packages for .NET](https://packages.fedoraproject.org/pkgs/dotnet9.0/dotnet-runtime-9.0/)<br>
    ✅ [**Ubuntu** can run on Raspberry Pis](https://ubuntu.com/download/raspberry-pi) and [provides official ARM64 packages for .NET](https://packages.ubuntu.com/plucky/dotnet-runtime-9.0)<br>
    ❌ Raspberry Pi OS is the default and most popular distro choice for Raspberry Pis, so it's extremely well tested and easy to find answers to any questions<br>
    ❌ Neither Fedora nor Ubuntu run on 32-bit armhf CPUs, such as the Raspberry Pi 2 v1.1

- **Install Mono**<br>
    ❌ Horrifically outdated and useless, and should not be used anymore<br>
    ❌ Whatever you do, don't pick this technique, all of the other options are better<br>
    ❌ This is why .NET (Core) was created to replace .NET Framework on non-Windows operating systems

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
