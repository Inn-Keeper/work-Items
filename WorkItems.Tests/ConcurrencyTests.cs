using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Api;
using Xunit;

namespace WorkItems.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly TestApi _api = new();
    private readonly HttpClient _client;

    public ConcurrencyTests() => _client = _api.Factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    private async Task<WorkItemResponse> Create() =>
        (await (await _client.PostAsJsonAsync("/workitems/", new WorkItemInput("Original", null, WorkItemStatus.Todo, null)))
            .Content.ReadFromJsonAsync<WorkItemResponse>())!;

    private Task<HttpResponseMessage> Put(int id, string? ifMatch, string title = "Changed")
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/workitems/{id}")
        {
            Content = JsonContent.Create(new WorkItemInput(title, null, WorkItemStatus.Todo, null))
        };
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Delete(int id, string ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/workitems/{id}");
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task UpdateWithCurrentVersionSucceedsAndBumpsTheETag()
    {
        var created = await Create();
        Assert.Equal(0, created.Version);

        var response = await Put(created.Id, "\"0\"");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<WorkItemResponse>())!.Version);
    }

    [Fact]
    public async Task StaleOrMissingVersionIsRejectedAndLeavesTheItemUnchanged()
    {
        var created = await Create();
        (await Put(created.Id, "\"0\"", "First writer")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Put(created.Id, "\"0\"", "Second writer")).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await Put(created.Id, null)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Delete(created.Id, "\"0\"")).StatusCode);

        var current = await _client.GetFromJsonAsync<WorkItemResponse>($"/workitems/{created.Id}");
        Assert.Equal("First writer", current!.Title);
        Assert.Equal(HttpStatusCode.NoContent, (await Delete(created.Id, "\"1\"")).StatusCode);
    }

    [Fact]
    public async Task WriteBetweenReadAndSaveIsCaughtByTheDatabase()
    {
        var created = await Create();
        using var scope = _api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkItemsDb>();
        var item = await db.WorkItems.SingleAsync(value => value.Id == created.Id); // read sees version 0

        (await Put(created.Id, "\"0\"")).EnsureSuccessStatusCode(); // another client writes version 1

        item.Title = "Late write";
        item.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }
}
