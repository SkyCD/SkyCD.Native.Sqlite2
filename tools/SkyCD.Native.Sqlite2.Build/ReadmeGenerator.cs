using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SkyCD.Native.Sqlite2.Build;

internal static class ReadmeGenerator
{
    internal static string PublishedFrameworks(string root)
    {
        var badge = Regex.Match(File.ReadAllText(Path.Combine(root, "README.md")), @"label=\.NET&message=([^&\s)]+)");
        if (!badge.Success) throw new BuildToolException("published .NET badge was not found");
        return string.Join(';', Uri.UnescapeDataString(badge.Groups[1].Value).Split('|').Select(version => "net" + version.Trim()));
    }

    internal static void Run(string root, string version, string? frameworks = null)
    {
        if (!Regex.IsMatch(version, @"^[0-9A-Za-z.\-]+$")) throw new BuildToolException("invalid README package version");
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-source.json")));
        var text = File.ReadAllText(Path.Combine(root, "README.template.md"))
            .Replace("{{SQLITE_VERSION}}", source.RootElement.GetProperty("version").GetString())
            .Replace("{{PACKAGE_VERSION}}", version);
        if (text.Contains("{{DOTNET_VERSIONS}}", StringComparison.Ordinal))
        {
            frameworks ??= XDocument.Load(Path.Combine(root, "src/SkyCD.Native.Sqlite2/SkyCD.Native.Sqlite2.csproj"))
                .Descendants("TargetFrameworks").Single().Value;
            var versions = frameworks.Split(';').Select(x => x.StartsWith("net", StringComparison.Ordinal) && Version.TryParse(x[3..], out var parsed)
                ? parsed.ToString() : throw new BuildToolException("invalid README target framework"));
            text = text.Replace("{{DOTNET_VERSIONS}}", Uri.EscapeDataString(string.Join(" | ", versions)));
        }
        if (text.Contains("{{", StringComparison.Ordinal)) throw new BuildToolException("unresolved README template variable");
        File.WriteAllText(Path.Combine(root, "README.md"), text.Replace("\r\n", "\n"));
        var descriptionTemplate = Path.Combine(root, "PackageDescription.template.txt");
        if (File.Exists(descriptionTemplate))
        {
            var platforms = string.Join(", ", source.RootElement.GetProperty("platforms").EnumerateObject().Select(x => x.Value.GetString()));
            var description = File.ReadAllText(descriptionTemplate)
                .Replace("{{DOTNET_VERSIONS_TEXT}}", string.Join(", ", frameworks!.Split(';').Select(x => x[3..])))
                .Replace("{{SUPPORTED_PLATFORMS}}", platforms);
            if (description.Contains("{{", StringComparison.Ordinal)) throw new BuildToolException("unresolved package description template variable");
            new XDocument(new XElement("Project", new XElement("PropertyGroup", new XElement("Description", description.Trim()))))
                .Save(Path.Combine(root, "src/SkyCD.Native.Sqlite2/PackageMetadata.g.props"));
        }
    }
}
