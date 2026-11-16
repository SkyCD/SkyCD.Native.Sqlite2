namespace SkyCD.Native.Sqlite2.Build;

internal static class ChangeClassifier
{
    internal static bool IsDocsOnly(IEnumerable<string> paths) => paths.All(path =>
        path is "README.md" or "README.template.md" ||
        (path.StartsWith("docs/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)));

    internal static async Task Run(string baseCommit, string headCommit, string output)
    {
        var changed = await ProcessRunner.Run("git", ["diff", "--name-only", "-z", baseCommit, headCommit, "--"], true);
        var code = !IsDocsOnly(changed.Split('\0', StringSplitOptions.RemoveEmptyEntries));
        await File.AppendAllTextAsync(output, $"code={code.ToString().ToLowerInvariant()}\n");
        Console.WriteLine(code ? "Code changes require the full build and test matrix." : "Documentation-only changes require README generation checks.");
    }
}
