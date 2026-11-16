# SkyCD.Native.Sqlite2

[![Build, test and publish](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml/badge.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml)

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

Use NuGet package source mapping so only `SkyCD.Native.Sqlite2` goes to `skycd`, while other packages use nuget.org. In GitHub Actions, grant the consuming repository read access under the package's **Manage Actions access**, set `permissions: packages: read`, and use its `GITHUB_TOKEN` as the feed credential.

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

The wrapper targets .NET 10 and currently bundles:

| Runtime | Native library | Minimum platform |
| --- | --- | --- |
| win-x64 | skycd_sqlite2.dll | Windows x64 |
| linux-x64 | libskycd_sqlite2.so | glibc 2.17 |
| osx-x64 | libskycd_sqlite2.dylib | macOS 10.13 |
| osx-arm64 | libskycd_sqlite2.dylib | macOS 11 |

Both little- and big-endian SQLite 2 database files are covered by tests. NuGet deploys the matching native asset during restore/publish; the resolver also supports source-build `runtimes` directories. The unique native name allows SQLite 2 to coexist with SQLite 3 in one process. Unsupported platform/architecture combinations raise `Sqlite2RuntimeUnavailableException`.

The wrapper opens a private snapshot, so the original file is never opened for writing. It runs the engine's integrity check before reading. A native authorizer denies writes, attach, and all pragmas except the integrity check. Stored executable-plugin configuration is ordinary data, not executed commands. Imported application-specific behavior remains the caller's responsibility.

Limits are 256 MiB input, 16 MiB decoded records, 128 table columns, one million rows per query, 64 b-tree levels, UTF-8 text, and a native VM operation budget. Corruption or limit failures raise `Sqlite2Exception`; unsupported SQLite storage versions raise `Sqlite2UnsupportedFormatException`. Only SQLite 2 storage formats 2–4 are accepted. Encrypted databases and SQLite 3 files are not supported.

## Build and test

Install the .NET 10 SDK, PowerShell 7 and [Zig 0.15.2](https://ziglang.org/download/0.15.2/), then run:

```powershell
./scripts/build-native.ps1 -ZigExecutable /path/to/zig
dotnet restore SkyCD.Native.Sqlite2.slnx
dotnet format SkyCD.Native.Sqlite2.slnx --verify-no-changes
dotnet build SkyCD.Native.Sqlite2.slnx -c Release --no-restore
dotnet test SkyCD.Native.Sqlite2.slnx -c Release --no-build
dotnet pack src/SkyCD.Native.Sqlite2 -c Release -o artifacts/nuget
```

The build script downloads the official [SQLite 2.8.17 generated sources](https://www.sqlite.org/sqlite-source-2_8_17.zip) into a unique temporary directory, verifies SHA-256 `e718e71fec6ff3757a01c46ec505661a76843e5bbce0327d12497176ad5add49`, compiles the four runtimes, and deletes the temporary source directory. **The upstream source ZIP is neither committed nor included in the NuGet package or workflow artifacts.** Generated native binaries and manifests are also excluded from Git.

The engine remains pinned for reproducibility. The build applies explicit 64-bit pointer/modern MinGW compatibility changes and small native `initPage` guards for cyclic ancestors, depth overflow and cell-array bounds, demonstrated necessary by malformed-file tests. The build script contains the complete transformations. No GPL implementation is copied.

CI builds the native assets, verifies formatting and package contents, and tests both the wrapper and the actual RID-published NuGet package on Windows, Linux, Intel macOS and ARM macOS. Publishing to GitHub Packages happens only for `main`, after every platform test passes, using the workflow's `GITHUB_TOKEN`. Pushes and manual runs generate new CI package versions automatically.

## License

Standalone wrapper/build additions are MIT. Code and synthetic fixtures extracted from SkyCD retain their BSD-2-Clause notice. SQLite is public domain with its blessing; bundled runtime portions retain their permissive notices. The package declares `MIT AND BSD-2-Clause AND blessing AND ZPL-2.1` and ships the corresponding notices. See `LICENSE`, `src/SkyCD.Native.Sqlite2/native/licenses`, and `native-sbom.json`.
