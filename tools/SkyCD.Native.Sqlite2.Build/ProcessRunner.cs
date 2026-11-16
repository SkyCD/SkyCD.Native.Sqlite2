using System.Diagnostics;

namespace SkyCD.Native.Sqlite2.Build;

internal static class ProcessRunner
{
    internal static async Task<string> Run(string executable, IEnumerable<string> arguments, bool capture = false, string? workingDirectory = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = capture };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // dotnet run exports its SDK paths. A child using another global.json must resolve its own SDK.
            foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) || name.StartsWith("DOTNET_MSBUILD", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(name);
        }
        if (workingDirectory is not null) start.WorkingDirectory = workingDirectory;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new BuildToolException($"cannot start {executable}");
        var output = capture ? await process.StandardOutput.ReadToEndAsync() : "";
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new BuildToolException($"{Path.GetFileName(executable)} exited with {process.ExitCode}");
        return output.Trim();
    }
}
