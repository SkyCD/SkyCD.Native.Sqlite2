using System.Text.Json;

namespace SkyCD.Native.Sqlite2.Build;

internal static class ReleaseMatrix
{
    internal static async Task Run(string output, string eventName = "workflow_dispatch")
    {
        using var http = new HttpClient();
        using var releases = JsonDocument.Parse(await http.GetStringAsync("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json"));
        var channels = releases.RootElement.GetProperty("releases-index").EnumerateArray()
            .Where(x => !x.GetProperty("latest-release").GetString()!.Contains('-', StringComparison.Ordinal))
            .Select(x => Version.Parse(x.GetProperty("channel-version").GetString()!))
            .Where(x => x.Major >= 8).Order().ToArray();
        if (channels.Length == 0) throw new BuildToolException("no stable .NET releases from version 8 were found");
        var platforms = new[] { ("ubuntu-latest", "linux-x64"), ("windows-latest", "win-x64"), ("windows-11-arm", "win-arm64"), ("macos-15", "osx-arm64"), ("macos-15-intel", "osx-x64") };
        var matrix = channels.SelectMany(channel => platforms.Select(platform => new { os = platform.Item1, rid = platform.Item2, framework = $"net{channel}", sdk = $"{channel}.x" }));
        var frameworks = string.Join(';', channels.Select(x => $"net{x}"));
        var tags = await ProcessRunner.Run("git", ["tag", "--merged", "HEAD", "--list", "v*", "--sort=-creatordate"], true);
        var latestTag = tags.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var unreleased = latestTag is null || await ProcessRunner.Run("git", ["rev-list", "--count", $"{latestTag}..HEAD"], true) != "0";
        await File.AppendAllTextAsync(output, $"matrix={JsonSerializer.Serialize(new { include = matrix })}\nframeworks={frameworks}\nlatest={channels[^1]}.x\n");
        await File.AppendAllTextAsync(output, $"unreleased={unreleased.ToString().ToLowerInvariant()}\n");
        await File.AppendAllTextAsync(output, $"build={PublicationPolicy.ShouldBuild(eventName, unreleased).ToString().ToLowerInvariant()}\n");
        Console.WriteLine($"Discovered stable .NET targets: {frameworks}");
    }
}
