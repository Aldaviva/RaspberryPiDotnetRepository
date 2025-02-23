using DataSizeUnits;
using RaspberryPiDotnetRepository.Data.ControlMetadata;
using RaspberryPiDotnetRepository.DotnetUpstream;
using System.Text.Json.Serialization;

namespace RaspberryPiDotnetRepository.Data;

public class DebianPackage(RuntimeType runtime, Version runtimeVersion, Version sdkVersion, CpuArchitecture architecture): IEquatable<DebianPackage> {

    public const string VERSION_SUFFIX = "-2";

    /// <summary>
    /// The name of the Debian package, such as <c>dotnet-runtime-8.0</c> or <c>aspnetcore-runtime-latest-lts</c>.
    /// </summary>
    public string nameWithMinorVersion => string.Join('-', ((string?[]) [
        runtime.getPackageName(), isMetaPackage ? "latest" : null, isMetaPackageSupportedLongTerm ? "lts" : null,
        !isMetaPackage && runtime != RuntimeType.CLI ? minorVersion : null
    ]).Compact());

    /// <summary>
    /// Which kind of runtime or SDK this package contains, such as ".NET Runtime". .NET does not have a name for this concept. Java calls this "package type." Can also be <see cref="RuntimeType.CLI"/> for the package that contains only the <c>/usr/bin/dotnet</c> tool.
    /// </summary>
    public RuntimeType runtime { get; } = runtime;

    /// <summary>
    /// Version of this runtime package, or if this is an SDK package, the associated runtime package version. (The patch version will be less than 100.)
    /// </summary>
    public Version runtimeVersion { get; } = runtimeVersion;

    /// <summary>
    /// Version of this SDK package, or if this is a runtime package, the associated SDK package version this runtime came from. (The patch version will be greater than or equal to 100.)
    /// </summary>
    public Version sdkVersion { get; } = sdkVersion;

    /// <summary>
    /// Which CPU architecture this package is for, or <c>null</c> for a package that can run on any architecture CPU.
    /// </summary>
    public CpuArchitecture architecture { get; } = architecture;

    /// <summary>
    /// <para>The oldest version of Debian that this package can run on, usually dictated by its libc6 version</para>
    /// <para>See <see href="https://github.com/dotnet/core/blob/main/release-notes/9.0/supported-os.md#linux-compatibility"/></para>
    /// <para>#28: .NET 9 and later don't run on ARM32 Debian 10 or 11</para>
    /// </summary>
    [JsonIgnore]
    public DebianRelease minimumDebianRelease => architecture == CpuArchitecture.ARM32 && version >= new Version(9, 0, 0) ? DebianRelease.BOOKWORM : DebianRelease.BUSTER;

    /// <summary>
    /// Like <c>packages/dotnet-runtime-8.0.5-0-armhf.deb</c> or <c>packages/aspnetcore-runtime-8.0-0-arm64-latest-lts.deb</c>
    /// </summary>
    public string filePathRelativeToRepo => Paths.Dos2UnixSlashes(Path.Combine("packages",
        string.Join(null, runtime.getPackageName(), "-", version.ToString(isMetaPackage ? 2 : 3), versionSuffix, $"-{architecture.toDebian()}", isMetaPackage ? "-latest" : "",
            isMetaPackageSupportedLongTerm ? "-lts" : "", ".deb")));

    /// <summary>
    /// Lowercase hexadecimal SHA-256 hash of the package file
    /// </summary>
    public string? fileHashSha256 { get; set; }

    /// <summary>
    /// The size of this package file
    /// </summary>
    public DataSize downloadSize { get; set; } = new(0);

    /// <summary>
    /// The disk space necessary to install this package
    /// </summary>
    public DataSize installationSize { get; set; } = new(0);

    /// <summary>
    /// <c>true</c> if this is a <c>-latest</c> or <c>-latest-lts</c> meta-dependency package, or <c>false</c> if it's a concrete CLI, runtime, or SDK implementation
    /// </summary>
    public bool isMetaPackage { get; init; }

    /// <summary>
    /// <c>true</c> if this is a <c>-latest-lts</c> meta-dependency package, or <c>false</c> if it's a <c>-latest</c> meta-dependency package or a concrete CLI, runtime, or SDK implementation
    /// </summary>
    public bool isMetaPackageSupportedLongTerm { get; init; }

    /// <summary>
    /// The version of this package, from upstream. SDK packages will use a patch version greater than or equal to 100.
    /// </summary>
    [JsonIgnore]
    public Version version => runtime == RuntimeType.SDK ? sdkVersion : runtimeVersion;

    /// <summary>
    /// First two version number segments
    /// </summary>
    [JsonIgnore]
    public string minorVersion => version.ToString(2);

    /// <summary>
    /// First three version number segments
    /// </summary>
    [JsonIgnore]
    public string patchVersion => version.ToString(3);

    /// <summary>
    /// Debian-specific revision to the package, not from upstream. Incremented if packages need to be regenerated before a new .NET upstream release.
    /// </summary>
    public string versionSuffix => VERSION_SUFFIX;

    [JsonIgnore]
    public Section section { get; } = Section.DEVEL;

    [JsonIgnore]
    public Priority priority { get; } = Priority.OPTIONAL;

    [JsonIgnore]
    public Uri homepage { get; } = new("https://dot.net");

    [JsonIgnore]
    public PersonWithEmail maintainer { get; } = new("Ben Hutchison", "ben@aldaviva.com");

    /// <summary>
    /// <para>Packages that this package depends on. They are mandatory to install when this package is installed, and package managers like apt will install them automatically.</para>
    /// <para> </para>
    /// <para>Microsoft documentation:    https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian#dependencies</para>
    /// <para>Microsoft release notes:    https://github.com/dotnet/core/blob/main/release-notes/9.0/os-packages.md#debian</para>
    /// <para>Microsoft container images: https://github.com/dotnet/dotnet-docker/blob/main/src/runtime-deps/8.0/bookworm-slim/arm32v7/Dockerfile</para>
    /// <para>Ubuntu packages:            https://packages.ubuntu.com/mantic/dotnet-runtime-8.0</para>
    /// <para> </para>
    /// <para>This uses an nice undocumented behavior of dpkg or apt where unknown package dependencies are ignored, so a list of alternatives can contain names that apply to other major Debian versions, like all the renamed libicu packages.</para>
    /// <para>This allows us to create a package that is agnostic to the Debian major version, which ends up saving about 2GB in this repository.</para>
    /// <para>If this weren't the case, we would need to make duplicate packages for each major Debian version with slightly different libicu dependencies to keep dpkg from failing on unknown dependencies. Luckily, this is not the case with dpkg 1.22.6 or apt 2.6.1.</para>
    /// </summary>
    [JsonIgnore]
    public IEnumerable<Dependency> dependencyPackages => runtime switch {
        _ when isMetaPackage => [new DependencyPackage($"{runtime.getPackageName()}-{minorVersion}")],
        RuntimeType.CLI      => [],
        RuntimeType.RUNTIME => ((List<Dependency?>) [
            new DependencyPackage(RuntimeType.CLI.getPackageName(), Inequality.GREATER_THAN_OR_EQUAL_TO, $"{runtimeVersion.ToString(3)}{versionSuffix}"),
            new DependencyPackage("libc6"),
            new DependencyAlternatives(new DependencyPackage("libgcc-s1"), new DependencyPackage("libgcc1")),
            new DependencyPackage("libgssapi-krb5-2"),
            new DependencyAlternatives(Enum.GetValues<DebianRelease>().OrderDescending().Select(release => new DependencyPackage(release.getLibIcuDependencyName()))),
            new DependencyAlternatives(new DependencyPackage("libssl3"), new DependencyPackage("libssl1.1")), // Trixie and Noble Numbat use libssl3t64
            new DependencyPackage("libstdc++6"),
            new DependencyPackage("tzdata"),
            new DependencyPackage("ca-certificates"),
            runtimeVersion >= new Version(9, 0, 0) ? null : new DependencyPackage("zlib1g") // #26 .NET ≥ 9 has built-in zlib-ng
        ]).Compact(),
        RuntimeType.ASPNETCORE_RUNTIME =>
            [new DependencyPackage($"{RuntimeType.RUNTIME.getPackageName()}-{runtimeVersion.ToString(2)}", Inequality.EQUAL, $"{runtimeVersion.ToString(3)}{versionSuffix}")],
        RuntimeType.SDK => [new DependencyPackage($"{RuntimeType.ASPNETCORE_RUNTIME.getPackageName()}-{runtimeVersion.ToString(2)}", Inequality.EQUAL, $"{runtimeVersion.ToString(3)}{versionSuffix}")],
        _               => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, "Unhandled runtime")
    };

    /*
     * Not declaring any suggested packages because
     * 1. Suggested packages are retained and not autoremoved, which makes it require explicit manual steps to clean up old versions after a minor version upgrade of a latest[-lts] package.
     * 2. APT will advertise suggested packages even when the suggester is transitively installed, which is annoying because it yells at you even when you're doing the right thing.
     */

    /// <summary>
    /// The runtime and SDK packages say they provide a virtual package with a version inequality.
    /// This is done so that dependent packages, such as third-party .NET apps, can depend on the concept of "any .NET runtime >= 6.0", for example, without having to manually write a giant list of every .NET minor version that fits this criteria, and would become out-of-date when a new .NET minor version is released.
    /// <para>Dependent example: <c>Depends: dotnet-runtime-latest | dotnet-runtime-6.0-or-greater</c></para>
    /// </summary>
    /// <param name="knownReleaseMinorVersions">a list of the latest minor versions of each .NET release in this repo >= 6.0, like [6.0, 7.0, 8.0]</param>
    /// <returns></returns>
    private IEnumerable<Dependency> providedPackages(IEnumerable<Version> knownReleaseMinorVersions) => runtime != RuntimeType.CLI && !isMetaPackage
        ? knownReleaseMinorVersions.Where(knownMinorVersion => knownMinorVersion <= version.AsMinor())
            .Select(knownMinorVersion => new DependencyPackage($"{runtime.getPackageName()}-{knownMinorVersion.ToString(2)}-or-greater")) : [];

    /// <summary>
    /// Textual summary of this package, shown in package managers
    /// </summary>
    [JsonIgnore]
    public string descriptionSummary => runtime switch {
        _ when isMetaPackage && isMetaPackageSupportedLongTerm  => $"{runtime.getFriendlyName()} (latest LTS)",
        _ when isMetaPackage && !isMetaPackageSupportedLongTerm => $"{runtime.getFriendlyName()} (latest LTS or STS)",
        RuntimeType.CLI                                         => ".NET CLI tool (without runtime)",
        RuntimeType.RUNTIME                                     => ".NET CLI tools and runtime",
        RuntimeType.ASPNETCORE_RUNTIME                          => "ASP.NET Core runtime",
        RuntimeType.SDK                                         => $".NET {minorVersion} Software Development Kit",
        _                                                       => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, "Unhandled runtime")
    };

    /// <summary>
    /// Textual description of this package, shown in package managers
    /// </summary>
    [JsonIgnore]
    public string descriptionBody => runtime switch {
        _ when isMetaPackage && isMetaPackageSupportedLongTerm =>
            $"""
             This is a dependency metapackage that installs the current latest Long Term Support (LTS) version of {runtime.getFriendlyName()}. Does not include preview or release candidate versions.

             Install this package if you want to always have the greatest LTS {runtime.getFriendlyName()} version installed. This will perform major and minor version upgrades — for example, if {runtime.getPackageName()}-6.0 were already installed, `apt upgrade` would install {runtime.getPackageName()}-{minorVersion}.

             If you instead want to always stay on the latest version, even if that means sometimes using an STS (Standard Term Support) release, then install {runtime.getPackageName()}-latest.

             If you instead want to always stay on a specific minor version, then install a numbered release, such as {runtime.getPackageName()}-{minorVersion}.

             If you are a developer who wants your application package to depend upon a certain minimum version of {runtime.getPackageName()}, it is suggested that you add a dependency on `{runtime.getPackageName()}-latest | {runtime.getPackageName()}-{minorVersion}-or-greater` (replace {minorVersion} with the minimum .NET version your app targets).
             """,
        _ when isMetaPackage && !isMetaPackageSupportedLongTerm =>
            $"""
             This is a dependency metapackage that installs the current latest version of {runtime.getFriendlyName()}, whether that is LTS (Long Term Support) or STS (Standard Term Support), whichever version number is greater. Does not include preview or release candidate versions.

             Install this package if you want to always have the highest stable .NET version installed, even if that version is not LTS. This will perform major and minor version upgrades — for example, if {runtime.getPackageName()}-{minorVersion} were already installed, `apt upgrade` would install {runtime.getPackageName()}-{new Version(version.Major + 1, 0)} when it was released.

             If you instead want to always stay on the latest LTS release and avoid STS, then install {runtime.getPackageName()}-latest-lts.

             If you instead want to always stay on a specific minor version, then install a numbered release, such as {runtime.getPackageName()}-{minorVersion}.

             If you are a developer who wants your application package to depend upon a certain minimum version of {runtime.getPackageName()}, it is suggested that you add a dependency on `{runtime.getPackageName()}-latest | {runtime.getPackageName()}-{minorVersion}-or-greater` (replace {minorVersion} with the minimum .NET version your app targets).
             """,
        RuntimeType.CLI =>
            $"""
             This package installs the '/usr/bin/dotnet' command-line interface, which is part of all .NET installations.

             This package does not install any .NET runtimes or SDKs by itself, so .NET applications won't run with only this package. This is just a common dependency used by the other .NET packages.

             To actually run or build .NET applications, you must also install one of the .NET runtime or SDK packages, such as {RuntimeType.RUNTIME.getPackageName()}-{minorVersion}, {RuntimeType.ASPNETCORE_RUNTIME.getPackageName()}-{minorVersion}, or {RuntimeType.SDK.getPackageName()}-{minorVersion}.
             """,
        RuntimeType.RUNTIME =>
            """
            .NET is a fast, lightweight, and modular platform for creating cross platform applications that work on GNU/Linux, Mac OS, and Windows.

            It particularly focuses on creating console applications, web applications, and microservices.

            .NET contains a runtime conforming to .NET Standards, a set of framework libraries, an SDK containing compilers, and a 'dotnet' CLI application to drive everything.
            """,
        RuntimeType.ASPNETCORE_RUNTIME =>
            """
            The ASP.NET Core runtime contains everything needed to run .NET web applications. It includes a high performance Virtual Machine as well as the framework libraries used by .NET applications.

            ASP.NET Core is a fast, lightweight, and modular platform for creating cross platform applications that work on GNU/Linux, Mac OS, and Windows.

            It particularly focuses on creating console applications, web applications, and microservices.
            """,
        RuntimeType.SDK =>
            """
            The .NET SDK is a collection of command-line applications to create, build, publish, and run .NET applications.

            .NET is a fast, lightweight, and modular platform for creating cross platform applications that work on GNU/Linux, Mac OS, and Windows.

            It particularly focuses on creating console applications, web applications, and microservices.
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, "Unhandled runtime")
    };

    public Control getControl(UpstreamReleasesSecondaryInfo upstreamInfo) => new(
        name: nameWithMinorVersion,
        version: $"{version.ToString(isMetaPackage ? 2 : 3)}{versionSuffix}",
        maintainer: maintainer,
        installedSize: installationSize,
        descriptionSummary: descriptionSummary,
        descriptionBody: descriptionBody) {

        architecture = architecture,
        section      = section,
        priority     = priority,
        homepage     = homepage,
        dependencies = dependencyPackages,
        provided     = providedPackages(upstreamInfo.knownReleaseMinorRuntimeVersions)
    };

    [JsonIgnore]
    public bool isUpToDateInBlobStorage { get; set; } = false;

    public override string ToString() => $"{runtime.getFriendlyName()} {patchVersion}{architecture}";

    public bool Equals(DebianPackage? other) => other is not null &&
        runtime == other.runtime &&
        version.Equals(other.version) &&
        versionSuffix == other.versionSuffix &&
        architecture == other.architecture;

    public override bool Equals(object? obj) => obj is not null && (ReferenceEquals(this, obj) || (obj is DebianPackage other && Equals(other)));

    public override int GetHashCode() => HashCode.Combine((int) runtime, version, versionSuffix, architecture);

}