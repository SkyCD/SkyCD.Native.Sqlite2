namespace SkyCD.Native.Sqlite2.Build;

internal sealed class BuildToolException(string reason) : InvalidOperationException($"Build tool failed: {reason}");
