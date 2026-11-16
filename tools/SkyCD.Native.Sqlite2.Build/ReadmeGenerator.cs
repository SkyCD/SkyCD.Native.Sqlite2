using System.Text.Json;
using System.Text.RegularExpressions;

namespace SkyCD.Native.Sqlite2.Build;

internal static class ReadmeGenerator
{
    internal static void Run(string root, string version)
    {
        if (!Regex.IsMatch(version, @"^[0-9A-Za-z.\-]+$")) throw new BuildToolException("invalid README package version");
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-source.json")));
        var text = File.ReadAllText(Path.Combine(root, "README.template.md"))
            .Replace("{{SQLITE_VERSION}}", source.RootElement.GetProperty("version").GetString())
            .Replace("{{PACKAGE_VERSION}}", version);
        if (text.Contains("{{", StringComparison.Ordinal)) throw new BuildToolException("unresolved README template variable");
        File.WriteAllText(Path.Combine(root, "README.md"), text.Replace("\r\n", "\n"));
    }
}
