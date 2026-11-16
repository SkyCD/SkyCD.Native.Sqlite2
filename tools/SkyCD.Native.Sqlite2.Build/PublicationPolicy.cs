namespace SkyCD.Native.Sqlite2.Build;

internal static class PublicationPolicy
{
    internal const string PreviewVersion = "0.1.0-next";
    internal static bool IsVersioned(string eventName) => eventName is "workflow_dispatch" or "schedule";
    internal static bool ShouldBuild(string eventName, bool unreleased) => !IsVersioned(eventName) || unreleased;
    internal static bool IsPreview(string version) => version is "0.1-next" or PreviewVersion;
}
