using System.Text.Json;
using SkyCD.Native.Sqlite2.Build;
using Xunit;

namespace SkyCD.Native.Sqlite2.Tests;

public sealed class RunnerRetryTests
{
    [Theory]
    [InlineData("""{"conclusion":"failure","steps":[]}""", true)]
    [InlineData("""{"conclusion":"timed_out","steps":[]}""", true)]
    [InlineData("""{"conclusion":"cancelled","steps":[]}""", false)]
    [InlineData("""{"conclusion":"success","steps":[]}""", false)]
    [InlineData("""{"conclusion":"failure","steps":[{"name":"Set up job","status":"completed","conclusion":"failure"}]}""", true)]
    [InlineData("""{"conclusion":"failure","steps":[{"name":"Tests","status":"completed","conclusion":"failure"}]}""", false)]
    [InlineData("""{"conclusion":"failure","steps":[{"name":"License checks","status":"completed","conclusion":"failure"}]}""", false)]
    [InlineData("""{"conclusion":"timed_out","steps":[{"name":"Tests","status":"in_progress","conclusion":null}]}""", false)]
    public void RetryPolicy_DistinguishesRunnerFailuresFromWorkflowFailures(string json, bool expected)
    {
        using var job = JsonDocument.Parse(json);
        Assert.Equal(expected, RunnerRetry.IsAllocationFailure(job.RootElement));
    }
}
