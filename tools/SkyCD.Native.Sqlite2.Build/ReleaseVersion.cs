using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SkyCD.Native.Sqlite2.Build;

internal static class ReleaseVersion
{
    internal static string Next(IEnumerable<string> releases, string sqliteVersion)
    {
        if (!Regex.IsMatch(sqliteVersion, @"^\d+\.\d+\.\d+$") ||
            !int.TryParse(sqliteVersion.Replace(".", ""), NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision <= 0)
            throw new BuildToolException("SQLite version cannot be represented as an integer revision");
        var patches = releases.Where(version => !PublicationPolicy.IsPreview(version.Trim().TrimStart('v'))).Select(version => Regex.Match(version.Trim(), @"^v?0\.1\.(\d+)(?:\.\d+|-[0-9A-Za-z.\-]+)?$"))
            .Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        var patch = patches.Length == 0 ? 0 : checked(patches.Max() + 1);
        return $"0.1.{patch}.{revision}";
    }

    internal static async Task Run(string root, string output, string eventName = "workflow_dispatch")
    {
        if (!PublicationPolicy.IsVersioned(eventName))
        {
            await File.AppendAllTextAsync(output, $"value={PublicationPolicy.PreviewVersion}\n");
            Console.WriteLine($"Preview package version: {PublicationPolicy.PreviewVersion}");
            return;
        }
        using var definition = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-source.json")));
        var source = definition.RootElement;
        var submodule = Path.Combine(root, source.GetProperty("path").GetString()!);
        var sqliteVersion = File.ReadAllText(Path.Combine(submodule, "VERSION")).Trim();
        if (sqliteVersion != source.GetProperty("version").GetString() ||
            await ProcessRunner.Run("git", ["-C", submodule, "rev-parse", "HEAD"], true) != source.GetProperty("commit").GetString())
            throw new BuildToolException("SQLite submodule does not match its pinned release metadata");
        var tags = await ProcessRunner.Run("git", ["tag", "--list", "v0.1.*"], true);
        var releases = tags.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var token = Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            // Publication may precede its README PR/tag; include published versions to avoid reusing that number.
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SkyCD.Native.Sqlite2.Build");
            for (var page = 1; ; page++)
            {
                using var response = await http.GetAsync($"https://api.github.com/orgs/SkyCD/packages/nuget/SkyCD.Native.Sqlite2/versions?per_page=100&page={page}");
                if (response.StatusCode == HttpStatusCode.NotFound) break;
                response.EnsureSuccessStatusCode();
                using var versions = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var entries = versions.RootElement.EnumerateArray().ToArray();
                releases.AddRange(entries.Select(version => version.GetProperty("name").GetString()!));
                if (entries.Length < 100) break;
            }
        }
        var version = Next(releases, sqliteVersion);
        await File.AppendAllTextAsync(output, $"value={version}\n");
        Console.WriteLine($"Package version: {version}");
    }
}
