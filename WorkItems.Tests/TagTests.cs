using System.Net;
using System.Net.Http.Json;
using WorkItems.Api;
using Xunit;

namespace WorkItems.Tests;

public sealed class TagTests : IDisposable
{
    private readonly TestApi _api = new();
    private readonly HttpClient _client;

    public TagTests() => _client = _api.Factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    private async Task<WorkItemResponse> Create(string title, params string[] tags)
    {
        var response = await _client.PostAsJsonAsync("/workitems/", new WorkItemInput(title, null, WorkItemStatus.Todo, null, tags));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemResponse>())!;
    }

    private async Task<string[]> Titles(string query) =>
        (await _client.GetFromJsonAsync<PagedResponse<WorkItemResponse>>($"/workitems/?{query}"))!
            .Items.Select(item => item.Title).ToArray();

    [Fact]
    public async Task TagsAreTrimmedDedupedAndSharedCaseInsensitively()
    {
        var first = await Create("First", " EF-Core ", "ef-core", "", "Learning");
        Assert.Equal(["EF-Core", "Learning"], first.Tags);

        // "ef-core" reuses the existing tag and keeps its original spelling.
        var second = await Create("Second", "ef-core");
        Assert.Equal(["EF-Core"], second.Tags);

        var tags = await _client.GetFromJsonAsync<List<TagCount>>("/tags");
        Assert.Equal([new TagCount("EF-Core", 2), new TagCount("Learning", 1)], tags);
    }

    [Fact]
    public async Task FilterByTagIgnoresCaseAndCombinesWithOtherFilters()
    {
        await Create("Alpha", "linq");
        await Create("Beta", "LINQ", "async");
        await Create("Gamma", "async");

        Assert.Equal(["Alpha", "Beta"], await Titles("tag=Linq&sort=title"));
        Assert.Equal(["Beta"], await Titles("tag=linq&search=bet"));
    }

    [Fact]
    public async Task UpdateReplacesTagsAndHidesUnusedOnes()
    {
        var item = await Create("Item", "old", "keep");
        var put = new HttpRequestMessage(HttpMethod.Put, $"/workitems/{item.Id}")
        {
            Content = JsonContent.Create(new WorkItemInput("Item", null, WorkItemStatus.Todo, null, ["keep", "new"]))
        };
        put.Headers.TryAddWithoutValidation("If-Match", $"\"{item.Version}\"");
        var updated = await (await _client.SendAsync(put)).Content.ReadFromJsonAsync<WorkItemResponse>();

        Assert.Equal(["keep", "new"], updated!.Tags);
        Assert.Equal(["keep", "new"], (await _client.GetFromJsonAsync<List<TagCount>>("/tags"))!.Select(tag => tag.Name));
    }

    [Fact]
    public async Task InvalidTagsAreRejected()
    {
        var tooMany = Enumerable.Range(0, WorkItemInput.MaxTags + 1).Select(i => $"t{i}").ToArray();
        foreach (var tags in new[] { tooMany, [new string('x', Tag.MaxLength + 1)] })
        {
            var response = await _client.PostAsJsonAsync("/workitems/", new WorkItemInput("Bad", null, WorkItemStatus.Todo, null, tags));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task ListLoadsTagsWithoutOneQueryPerItem()
    {
        for (var i = 0; i < 5; i++) await Create($"Item {i}", "shared", $"own-{i}");

        _api.Queries.Reset();
        var page = await _client.GetFromJsonAsync<PagedResponse<WorkItemResponse>>("/workitems/");

        Assert.All(page!.Items, item => Assert.Equal(2, item.Tags.Count));
        // COUNT + one SELECT for the page, however many items it has. N+1 would be 7 here.
        Assert.Equal(2, _api.Queries.Count);
    }
}
