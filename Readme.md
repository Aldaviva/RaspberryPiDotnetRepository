Raspberry Pi OS .NET Repository
===

![.NET latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwest.aldaviva.com%2Fraspbian%2Fbadges%2Fdotnet.json&query=%24.latestVersion&logo=dotnet&label=latest%20version&color=success) ![Raspberry Pi OS latest version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwest.aldaviva.com%2Fraspbian%2Fbadges%2Fraspbian.json&query=%24.latestVersion&logo=raspberrypi&label=latest%20version&color=success)

Public repository of ARM32 (armhf) and ARM64 .deb packages for [.NET](https://dotnet.microsoft.com/) runtimes and SDKs to install on [Raspberry Pis](https://www.raspberrypi.com) running [Raspberry Pi OS (Raspbian)](https://www.raspberrypi.com/software/operating-systems/).

Vendors like [Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian), [Fedora](https://packages.fedoraproject.org/pkgs/dotnet8.0/), and [Ubuntu](https://packages.ubuntu.com/source/mantic/dotnet8) provide official DEB packages for .NET, but none of them support armhf, so they can't be installed on Raspberry Pi OS with the default 32-bit architecture. Microsoft [recommends](https://learn.microsoft.com/en-us/dotnet/iot/deployment) installing .NET on Raspberry Pis using their build-machine–oriented [installation script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script), which does not install system-wide by default, does not update or clean up automatically, and does not handle the concept of installing whatever the latest minor version currently is (you must always choose a release channel like LTS or STS, but there's no simple way for the script to tell which one is newer).

This repository comprises unofficial packages that each contain **official .NET Linux ARM builds by Microsoft**, created from the same exact binaries you get when you install .NET from their installation script or click one of the Linux Arm32 or Arm64 Binaries links on a [.NET download page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2,3" bullets="-,1." -->

- [Installation](#installation)
    1. [Add repository](#add-repository)
    1. [Install packages](#install-packages)
- [Compatible versions](#compatible-versions)
- [List installed versions](#list-installed-versions)

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

> ℹ *The following examples use the latest .NET release, 8.0. If you want to install an older version instead, then you can replace `8.0` in these examples with another [supported version number](#supported-versions), such as `6.0` or `7.0`.*

#### .NET Runtime

For running command-line applications.
```sh
sudo apt install dotnet-runtime-8.0
```

#### ASP.NET Core Runtime

For running web applications.

This package automatially installs the .NET Runtime as well.
```sh
sudo apt install aspnetcore-runtime-8.0
```

#### .NET SDK

For building .NET applications.

This package automatially installs the .NET Runtime and ASP.NET Core Runtime as well.
```sh
sudo apt install dotnet-sdk-8.0
```

## Compatible versions

#### Operating systems
|Raspberry Pi OS|.NET 6|.NET 7|.NET 8|
|-:|:-:|:-:|:-:|
|Buster (10)|✅|✅|✅|
|Bullseye (11)|✅|✅|✅|
|Bookworm (12)|✅|✅|✅|

#### CPU architectures
- ✅ ARM32 (armhf/AArch32/ARMv7, 32-bit)
- ✅ ARM64 (AArch64/ARMv8, 64-bit)

#### Raspberry Pis
- ✅ Raspberry Pi 2
- ✅ Raspberry Pi 3
- ✅ Raspberry Pi 4
- ✅ Raspberry Pi 5 or greater
- ✅ Other Raspberry Pis that have an ARMv7 or greater CPU, such as Compute Module 3 and 4, Pi Zero 2 W, and Pi 400
- ⛔ Raspberry Pi 1, Pi Pico, Compute Module 1, and Pi Zero are [not supported by .NET](https://github.com/dotnet/core/issues/1232#issuecomment-359519481) because they only have an [ARMv6 CPU](https://en.wikipedia.org/wiki/Raspberry_Pi#Specifications), and [.NET requires ARMv7 or later](https://learn.microsoft.com/en-us/dotnet/iot/intro#supported-hardware-platforms)

## List installed versions

```sh
$ dotnet --info
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
```
