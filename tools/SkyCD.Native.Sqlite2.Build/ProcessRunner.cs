using System.Diagnostics;

namespace SkyCD.Native.Sqlite2.Build;

internal static class ProcessRunner
{
    internal static async Task<string> Run(string executable, IEnumerable<string> arguments, bool capture = false)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = capture };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new BuildToolException($"cannot start {executable}");
        var output = capture ? await process.StandardOutput.ReadToEndAsync() : "";
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new BuildToolException($"{Path.GetFileName(executable)} exited with {process.ExitCode}");
        return output.Trim();
    }
}
