using SkyCD.Native.Sqlite2.Build;
using Xunit;

namespace SkyCD.Native.Sqlite2.Tests;

public sealed class PublicationPolicyTests
{
    [Theory]
    [InlineData("push", false, true, false)]
    [InlineData("push", true, true, false)]
    [InlineData("pull_request", true, true, false)]
    [InlineData("workflow_dispatch", false, false, true)]
    [InlineData("workflow_dispatch", true, true, true)]
    [InlineData("schedule", false, false, true)]
    [InlineData("schedule", true, true, true)]
    public void EventAndUnreleasedCommits_SelectPreviewOrNumberedRelease(string eventName, bool unreleased, bool build, bool versioned)
    {
        Assert.Equal(build, PublicationPolicy.ShouldBuild(eventName, unreleased));
        Assert.Equal(versioned, PublicationPolicy.IsVersioned(eventName));
    }

    [Theory]
    [InlineData("0.1-next", true)]
    [InlineData("0.1.0-next", true)]
    [InlineData("0.1.1.2817", false)]
    [InlineData("0.1.0-ci.15", false)]
    [InlineData("0.1.0-next-other", false)]
    public void PreviewMatching_DoesNotIncludeNumberedReleases(string version, bool preview)
        => Assert.Equal(preview, PublicationPolicy.IsPreview(version));
}
