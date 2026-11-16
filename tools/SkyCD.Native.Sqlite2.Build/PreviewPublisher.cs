using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SkyCD.Native.Sqlite2.Build;

internal static class PreviewPublisher
{
    internal static async Task Run()
    {
        var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? throw new BuildToolException("GH_TOKEN is required to replace the preview package");
        using var http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SkyCD.Native.Sqlite2.Build");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        Console.WriteLine($"Removed {await DeletePreviousPreview(http)} previous preview version(s).");
    }

    internal static async Task<int> DeletePreviousPreview(HttpClient http)
    {
        const string package = "orgs/SkyCD/packages/nuget/SkyCD.Native.Sqlite2/versions";
        var ids = new List<long>();
        // Collect before deleting, so removing entries cannot shift later pagination pages.
        for (var page = 1; ; page++)
        {
            using var response = await http.GetAsync($"{package}?per_page=100&page={page}");
            if (response.StatusCode == HttpStatusCode.NotFound) return 0;
            response.EnsureSuccessStatusCode();
            using var versions = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var entries = versions.RootElement.EnumerateArray().ToArray();
            ids.AddRange(entries.Where(version => PublicationPolicy.IsPreview(version.GetProperty("name").GetString()!)).Select(version => version.GetProperty("id").GetInt64()));
            if (entries.Length < 100) break;
        }
        foreach (var id in ids)
        {
            using var response = await http.DeleteAsync($"{package}/{id}");
            if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
        }
        return ids.Count;
    }
}
