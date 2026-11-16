[![Build, test and publish](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml/badge.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/blob/main/LICENSE)
[![Bundled SQLite](https://img.shields.io/badge/bundled_SQLite-{{SQLITE_VERSION}}-blue.svg)](https://github.com/SkyCD/SkyCD.Native.Sqlite2/blob/main/native-source.json)
[![NuGet build](https://img.shields.io/static/v1?label=NuGet&message={{PACKAGE_VERSION}}&color=blue)](https://github.com/orgs/SkyCD/packages/nuget/package/SkyCD.Native.Sqlite2)

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

The wrapper targets .NET 10 and currently bundles:

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

SQLite comes from the official [SQLite Git mirror](https://github.com/sqlite/sqlite) as the `upstream/sqlite` Git submodule, pinned to tag `version-{{SQLITE_VERSION}}` and its exact commit in `native-source.json`. The .NET build tool verifies that commit and rejects modified tracked source, copies the library sources to a temporary directory, generates the parser with upstream Lemon, and compiles all five native runtimes. It leaves the submodule unchanged. **No SQLite source ZIP is downloaded, committed, or included in packages or workflow artifacts.** Generated native binaries and manifests are also excluded from Git.

The engine remains pinned for reproducibility. The build applies explicit 64-bit pointer/modern MinGW compatibility changes and small native `initPage` guards for cyclic ancestors, depth overflow and cell-array bounds, demonstrated necessary by malformed-file tests. The .NET tool contains the complete transformations. No GPL implementation is copied. Windows CRT section sentinels and its no-op DLL callback are supplied by this project under MIT, avoiding unmarked default startup units.

CI reads Microsoft's official .NET release index on every build and automatically targets every stable major release from .NET 8 through the latest, excluding previews. The NuGet package includes an assembly for each discovered target. Each target is tested with an actual RID-published package on Windows x64, Windows ARM64, Linux x64, Intel macOS and ARM macOS. Dependabot checks Actions and NuGet dependencies monthly; its update PRs run the same discovery, so newly released .NET versions join those builds automatically. Build tooling itself requires .NET 10.

Publishing to GitHub Packages happens only for `main`, after every platform and .NET target passes, using the workflow's `GITHUB_TOKEN`. Pushes and manual runs generate new CI package versions automatically. CI then regenerates this README from `README.template.md`, updating the package and bundled SQLite badges, commits it without triggering another build, and tags the released commit as `v<package-version>`.

A scheduled check runs every four months (March 16, July 16 and November 16). It compares `main` against the latest reachable release tag and builds/releases only when commits remain untagged. A new .NET release alone does not force a scheduled release without new commits.

## License

The wrapper, build tools and synthetic fixtures are MIT licensed. SQLite is public domain with its blessing; bundled runtime portions retain their permissive notices. The package declares `MIT AND blessing` and ships the corresponding notices. The linked MinGW startup/runtime sources have explicit public-domain headers; the two default startup units without those headers are replaced by project-owned MIT definitions. No ZPL dependency is declared. See `LICENSE`, `src/SkyCD.Native.Sqlite2/native/licenses`, and `native-sbom.json`.

## Local .NET build tool

The helpers are a packaged .NET local tool, `SkyCD.Native.Sqlite2.Build`, from the same GitHub Packages feed. There are no PowerShell helper scripts in this repository. After configuring feed authentication, install the published tool into a local manifest:

```sh
dotnet new tool-manifest
dotnet tool install SkyCD.Native.Sqlite2.Build --version {{PACKAGE_VERSION}}
dotnet tool run sqlite2-build -- native-build --zig /path/to/zig
dotnet tool run sqlite2-build -- verify-package --package artifacts/nuget/SkyCD.Native.Sqlite2.{{PACKAGE_VERSION}}.nupkg
dotnet tool run sqlite2-build -- check-licenses --sbom native-sbom.json --policy dependency-license-policy.json --report artifacts/license-report.json
```

Before the first package is installed, bootstrap any command with `dotnet run --project tools/SkyCD.Native.Sqlite2.Build -- <command>`. `smoke-package` restores and RID-publishes a real package consumer; `generate-readme` expands this document from `README.template.md`. Edit the template, not the generated README. On each main-branch push, CI fills the bundled SQLite and CI package versions, embeds the generated document in the packages, and commits it back after publication without starting a workflow loop. Automated commits include the requested coauthor trailer.
