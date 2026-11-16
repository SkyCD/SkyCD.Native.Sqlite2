param([Parameter(Mandatory = $true)][string]$Package)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Package))
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    if ($entries | Where-Object { $_ -match '(?i)\.zip$|sqlite-source|\.cs$' }) { throw "Package includes upstream source/archive files." }
    foreach ($rid in @("win-x64", "linux-x64", "osx-x64", "osx-arm64")) {
        if (!($entries | Where-Object { $_.StartsWith("runtimes/$rid/native/") })) { throw "Missing native runtime $rid." }
    }
    foreach ($required in @("lib/net10.0/SkyCD.Native.Sqlite2.dll", "buildTransitive/SkyCD.Native.Sqlite2.targets", "licenses/NOTICE.txt", "README.md")) {
        if ($required -notin $entries) { throw "Missing package entry $required." }
    }
    Write-Host "Package content checks passed. No upstream source ZIP is included."
}
finally { $archive.Dispose() }
