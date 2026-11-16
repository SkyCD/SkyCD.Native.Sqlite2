using System.Xml.Linq;
using System.Text.Json;

namespace SkyCD.Native.Sqlite2.Build;

internal static class PackageSmoke
{
    internal static async Task Run(string root, string version, string rid, string feed, string framework)
    {
        Directory.CreateDirectory(Path.Combine(root, "artifacts"));
        var config = Path.Combine(root, "artifacts/smoke.NuGet.Config");
        new XDocument(new XElement("configuration", new XElement("packageSources", new XElement("clear"),
            new XElement("add", new XAttribute("key", "local"), new XAttribute("value", Path.GetFullPath(feed))),
            new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))))).Save(config);
        var project = Path.Combine(root, "tests/PackageSmoke/PackageSmoke.csproj");
        var projectDirectory = Path.GetDirectoryName(project)!;
        if (!framework.StartsWith("net", StringComparison.Ordinal) || !Version.TryParse(framework[3..], out var targetFramework))
            throw new BuildToolException("invalid smoke test target framework");
        File.WriteAllText(Path.Combine(projectDirectory, "global.json"), JsonSerializer.Serialize(new { sdk = new { version = $"{targetFramework}.100", rollForward = "latestFeature" } }));
        var output = Path.Combine(root, "artifacts/smoke");
        await ProcessRunner.Run("dotnet", ["restore", project, "-r", rid, $"-p:TargetFramework={framework}", $"-p:NativeSqlite2Version={version}", "--configfile", config], workingDirectory: projectDirectory);
        await ProcessRunner.Run("dotnet", ["publish", project, "-c", "Release", "-r", rid, $"-p:TargetFramework={framework}", "--self-contained", "false", "--no-restore", $"-p:NativeSqlite2Version={version}", "-o", output], workingDirectory: projectDirectory);
        await ProcessRunner.Run("dotnet", [Path.Combine(output, "PackageSmoke.dll"), Path.Combine(root, "tests/SkyCD.Native.Sqlite2.Tests/Fixtures/cds.db")]);
    }
}
