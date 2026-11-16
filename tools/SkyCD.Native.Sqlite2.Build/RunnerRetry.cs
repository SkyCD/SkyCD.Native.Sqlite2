using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SkyCD.Native.Sqlite2.Build;

internal static class RunnerRetry
{
    internal const int MaximumAttempts = 4;

    internal static bool IsAllocationFailure(JsonElement job)
    {
        if (job.GetProperty("conclusion").GetString() is not ("failure" or "timed_out")) return false;
        // Runner acquisition failures have no steps, or fail during runner setup before any workflow step starts.
        return !job.TryGetProperty("steps", out var steps) || steps.EnumerateArray().All(step =>
            step.GetProperty("name").GetString() == "Set up job" ||
            step.GetProperty("status").GetString() == "queued" ||
            step.GetProperty("conclusion").GetString() == "skipped");
    }

    internal static async Task Run(string repository, string runId)
    {
        var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? throw new BuildToolException("GH_TOKEN is required for runner retries");
        using var http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SkyCD.Native.Sqlite2.Build");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        using var run = await Get($"repos/{repository}/actions/runs/{runId}");
        var state = run.RootElement;
        var attempt = state.GetProperty("run_attempt").GetInt32();
        if (attempt >= MaximumAttempts || state.GetProperty("status").GetString() != "completed" ||
            state.GetProperty("conclusion").GetString() is not ("failure" or "timed_out"))
        {
            Console.WriteLine("No retry: run is active, cancelled, successful, or has reached the retry limit.");
            return;
        }
        var branch = Uri.EscapeDataString(state.GetProperty("head_branch").GetString()!);
        using var head = await Get($"repos/{repository}/branches/{branch}");
        if (head.RootElement.GetProperty("commit").GetProperty("sha").GetString() != state.GetProperty("head_sha").GetString())
        {
            Console.WriteLine("No retry: a newer commit supersedes this run.");
            return;
        }
        var failures = new List<JsonElement>();
        for (var page = 1; ; page++)
        {
            using var jobs = await Get($"repos/{repository}/actions/runs/{runId}/attempts/{attempt}/jobs?per_page=100&page={page}");
            var entries = jobs.RootElement.GetProperty("jobs").EnumerateArray().ToArray();
            failures.AddRange(entries.Where(job => job.GetProperty("conclusion").GetString() is "failure" or "timed_out").Select(job => job.Clone()));
            if (entries.Length < 100) break;
        }
        if (failures.Count == 0 || failures.Any(job => !IsAllocationFailure(job)))
        {
            Console.WriteLine("No retry: a workflow step failed, or no runner-allocation failure was found.");
            return;
        }
        // Recheck after the backoff, since another actor may have rerun or superseded this run.
        using var latest = await Get($"repos/{repository}/actions/runs/{runId}");
        if (latest.RootElement.GetProperty("run_attempt").GetInt32() != attempt || latest.RootElement.GetProperty("status").GetString() != "completed") return;
        using var response = await http.PostAsJsonAsync($"repos/{repository}/actions/runs/{runId}/rerun-failed-jobs", new { });
        response.EnsureSuccessStatusCode();
        Console.WriteLine($"Requeued {failures.Count} runner-allocation failure(s); attempt {attempt + 1}/{MaximumAttempts}.");

        async Task<JsonDocument> Get(string path)
        {
            using var response = await http.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
    }
}
