[![Build, test and publish](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml/badge.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/blob/main/LICENSE)
[![Bundled SQLite](https://img.shields.io/badge/bundled_SQLite-2.8.17-blue.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/blob/main/native-source.json)
[![Supported .NET versions](https://img.shields.io/static/v1?label=.NET&message=8.0%20%7C%209.0%20%7C%2010.0%20%7C%2011.0&color=512BD4)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml)
[![NuGet build](https://img.shields.io/static/v1?label=NuGet&message=0.1.1.2817&color=blue)](https://github.com/orgs/SkyCD/packages/nuget/package/SkyCD.Native.Sqlite2)

# SkyCD.Native.Sqlite2

Read-only .NET access to **legacy SQLite 2 databases**, using bundled native libraries. This is a small reusable library extracted from [SkyCD](https://github.com/SkyCD/SkyCD), where it imports old CDCollect catalogs. It does not contain CDCollect-specific schema or domain logic.

## Why this exists

SQLite 2 and SQLite 3 use incompatible binary formats. Today's common .NET SQLite packages read SQLite 3, so they reject old SQLite 2 catalogs even when their package major version happens to be 2. This project uses the original SQLite engine through a focused .NET wrapper, avoiding both a custom managed b-tree parser and a requirement for users to install an obsolete conversion executable.

It is intended for reading and migrating old catalogs, backups, and application data. It is not a database for new application storage, an ORM, or a replacement for current SQLite 3 packages. New databases should use SQLite 3.

## Install from GitHub Packages

CI publishes prerelease packages to the [SkyCD organization NuGet registry](https://github.com/orgs/SkyCD/packages/nuget/package/SkyCD.Native.Sqlite2). Package versions are generated as `0.1.0-ci.<run-number>`; find a published version on the package page. No nuget.org publication is configured. Each CI build also retains the exact `.nupkg` as an Actions artifact.

Add `https://nuget.pkg.github.com/SkyCD/index.json` as a NuGet source named `skycd`, then authenticate. GitHub Packages requires authentication even for public NuGet packages. For local use, supply a classic personal access token with `read:packages` through the NuGet credential provider or an environment variable; never commit it:

```powershell
dotnet nuget add source https://nuget.pkg.github.com/SkyCD/index.json --name skycd
$env:NuGetPackageSourceCredentials_skycd = "Username=YOUR_GITHUB_USERNAME;Password=$env:GITHUB_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
dotnet add package SkyCD.Native.Sqlite2 --version YOUR_PUBLISHED_VERSION
```

Use NuGet package source mapping so only `SkyCD.Native.Sqlite2*` goes to `skycd`, while other packages use nuget.org. In GitHub Actions, grant the consuming repository read access under the package's **Manage Actions access**, set `permissions: packages: read`, and use its `GITHUB_TOKEN` as the feed credential.

## Use the API

The package includes the wrapper and native runtimes. You do not need the SQLite command-line tool or a compiler:

```csharp
using SkyCD.Native.Sqlite2;

using var input = File.OpenRead("legacy-catalog.db");
using var database = new Sqlite2Database(input);

Console.WriteLine($"Native engine: {Sqlite2Database.EngineVersion}");
foreach (var row in database.ReadTable("cds"))
{
    Console.WriteLine($"Row {row.RowId}: {string.Join(", ", row.Values)}");
}
```

`ReadTable` returns rows with their integer row ID and nullable string values in declared column order. SQLite 2 stores values as text; interpret dates and numbers according to the source application's schema. INTEGER PRIMARY KEY values are resolved by the native engine. `ReadTable("sqlite_master")` exposes the schema records. Table identifiers are validated; arbitrary SQL is not exposed.

Pass a `CancellationToken` to the constructor to cancel copying and subsequent native queries. Always dispose the database. The input stream can be nonseekable; a seekable input is read from its beginning. Its position advances, and the caller remains responsible for disposing it.

## Platforms and behavior

The wrapper supports the .NET versions shown in the badge above and bundles:

| Runtime | Native library | Minimum platform |
| --- | --- | --- |
| win-x64 | skycd_sqlite2.dll | Windows x64 |
| win-arm64 | skycd_sqlite2.dll | Windows ARM64 |
| linux-x64 | libskycd_sqlite2.so | glibc 2.17 |
| osx-x64 | libskycd_sqlite2.dylib | macOS 10.13 |
| osx-arm64 | libskycd_sqlite2.dylib | macOS 11 |

Both little- and big-endian SQLite 2 database files are covered by tests. NuGet deploys the matching native asset during restore/publish; the resolver also supports source-build `runtimes` directories. The unique native name allows SQLite 2 to coexist with SQLite 3 in one process. Unsupported platform/architecture combinations raise `Sqlite2RuntimeUnavailableException`.

The wrapper opens a private snapshot, so the original file is never opened for writing. It runs the engine's integrity check before reading. A native authorizer denies writes, attach, and all pragmas except the integrity check. Stored executable-plugin configuration is ordinary data, not executed commands. Imported application-specific behavior remains the caller's responsibility.

Limits are 256 MiB input, 16 MiB decoded records, 128 table columns, one million rows per query, 64 b-tree levels, UTF-8 text, and a native VM operation budget. Corruption or limit failures raise `Sqlite2Exception`; unsupported SQLite storage versions raise `Sqlite2UnsupportedFormatException`. Only SQLite 2 storage formats 2–4 are accepted. Encrypted databases and SQLite 3 files are not supported.

## Build and test

Install the .NET 10 SDK and [Zig 0.15.2](https://ziglang.org/download/0.15.2/), then run:

```powershell
git submodule update --init --recursive
dotnet run --project tools/SkyCD.Native.Sqlite2.Build -- native-build --zig /path/to/zig
dotnet restore SkyCD.Native.Sqlite2.slnx
dotnet format SkyCD.Native.Sqlite2.slnx --verify-no-changes
dotnet build SkyCD.Native.Sqlite2.slnx -c Release --no-restore
dotnet test SkyCD.Native.Sqlite2.slnx -c Release --no-build
dotnet pack src/SkyCD.Native.Sqlite2 -c Release -o artifacts/nuget
```

These commands build the bundled SQLite libraries from the official source and run the tests. If you only want to use the library, install the NuGet package instead; it includes the native libraries you need.

Packages are tested on the supported platforms before publication. Check the badges above for supported .NET versions, or the [package page](https://github.com/orgs/SkyCD/packages/nuget/package/SkyCD.Native.Sqlite2) for details about a particular version.

## Local .NET build tool

The repository includes a .NET build tool for contributors. Run it from the repository root:

```sh
dotnet run --project tools/SkyCD.Native.Sqlite2.Build -- native-build --zig /path/to/zig
dotnet run --project tools/SkyCD.Native.Sqlite2.Build -- verify-package --package /path/to/package.nupkg
```

To change this README, edit `README.template.md`; CI generates the published copy. Only the library is published to GitHub Packages.
