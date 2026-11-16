using System.IO.Compression;
using System.Xml.Linq;

namespace SkyCD.Native.Sqlite2.Build;

internal static class PackageVerifier
{
    internal static void Run(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        var entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.Ordinal);
        if (entries.Any(e => e.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || e.Contains("sqlite-source", StringComparison.OrdinalIgnoreCase) || e.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            throw new BuildToolException("package contains a source archive or code");
        if (entries.Contains("lib/net8.0/SkyCD.Native.Sqlite2.dll"))
        {
            using var nuspec = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
            var description = XDocument.Load(nuspec).Descendants().Single(e => e.Name.LocalName == "description").Value;
            foreach (var assembly in entries.Where(e => e.StartsWith("lib/net", StringComparison.Ordinal) && e.EndsWith("/SkyCD.Native.Sqlite2.dll", StringComparison.Ordinal)))
                if (!description.Contains(assembly.Split('/')[1][3..], StringComparison.Ordinal)) throw new BuildToolException("package description omits a supported .NET version");
            if (!description.Contains("Windows ARM64", StringComparison.Ordinal)) throw new BuildToolException("package description omits Windows ARM64");
            foreach (var rid in new[] { "win-x64", "win-arm64", "linux-x64", "osx-x64", "osx-arm64" })
                if (!entries.Any(e => e.StartsWith($"runtimes/{rid}/native/", StringComparison.Ordinal))) throw new BuildToolException($"missing runtime {rid}");
            foreach (var name in new[] { "buildTransitive/SkyCD.Native.Sqlite2.targets", "licenses/NOTICE.txt" })
                if (!entries.Contains(name)) throw new BuildToolException($"missing package entry {name}");
        }
        else if (!entries.Contains("tools/net10.0/any/SkyCD.Native.Sqlite2.Build.dll")) throw new BuildToolException("unexpected package type");
        if (!entries.Contains("README.md")) throw new BuildToolException("package README is missing");
        Console.WriteLine($"Verified {Path.GetFileName(package)}; no source ZIP is included.");
    }
}
