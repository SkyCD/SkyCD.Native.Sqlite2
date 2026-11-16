using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkyCD.Native.Sqlite2.Build;
using Xunit;

namespace SkyCD.Native.Sqlite2.Tests;

public sealed class PreviewPublisherTests
{
    [Fact]
    public async Task Replacement_CollectsEveryPageBeforeDeletingOnlyPreviewVersions()
    {
        using var registry = new Registry(request =>
        {
            if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
            var versions = request.RequestUri!.Query.Contains("&page=1", StringComparison.Ordinal)
                ? Enumerable.Range(1, 100).Select(id => new { id, name = id == 1 ? "0.1.0-next" : $"0.1.{id}.2817" })
                : new[] { new { id = 101, name = "0.1-next" } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(versions)) };
        });
        using var client = new HttpClient(registry) { BaseAddress = new Uri("https://api.github.com/") };
        Assert.Equal(2, await PreviewPublisher.DeletePreviousPreview(client));
        Assert.Equal(new[] { "GET", "GET", "DELETE", "DELETE" }, registry.Requests.Select(request => request.Method));
        Assert.EndsWith("/versions/1", registry.Requests[2].Url, StringComparison.Ordinal);
        Assert.EndsWith("/versions/101", registry.Requests[3].Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingPackage_RequiresNoDeletion()
    {
        using var registry = new Registry(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(registry) { BaseAddress = new Uri("https://api.github.com/") };
        Assert.Equal(0, await PreviewPublisher.DeletePreviousPreview(client));
        Assert.Single(registry.Requests);
    }

    [Fact]
    public async Task PermissionFailure_StopsBeforeDeletingAnything()
    {
        using var registry = new Registry(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var client = new HttpClient(registry) { BaseAddress = new Uri("https://api.github.com/") };
        await Assert.ThrowsAsync<HttpRequestException>(() => PreviewPublisher.DeletePreviousPreview(client));
        Assert.Equal("GET", Assert.Single(registry.Requests).Method);
    }

    private sealed class Registry(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal List<(string Method, string Url)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.AbsoluteUri));
            return Task.FromResult(respond(request));
        }
    }
}
