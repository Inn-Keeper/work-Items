using System.Net;
using System.Net.Http.Json;
using WorkItems.Api;
using Xunit;

namespace WorkItems.Tests;

public sealed class QueryTests : IDisposable
{
    private readonly TestApi _api = new();
    private readonly HttpClient _client;

    public QueryTests() => _client = _api.Factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    private async Task Seed()
    {
        var utc = TimeSpan.Zero;
        WorkItemInput[] items =
        [
            new("banana", "has 100% effort", WorkItemStatus.Done, new DateTimeOffset(2027, 3, 1, 0, 0, 0, utc)),
            new("Apple", null, WorkItemStatus.Todo, null),
            new("cherry", "Learn LINQ", WorkItemStatus.InProgress, new DateTimeOffset(2027, 1, 1, 0, 0, 0, utc)),
            // Non-UTC input is stored as UTC (2026-12-31 22:00Z), so it still sorts before cherry.
            new("date", null, WorkItemStatus.Todo, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(2))),
        ];
        foreach (var item in items)
            (await _client.PostAsJsonAsync("/workitems/", item)).EnsureSuccessStatusCode();
    }

    private async Task<PagedResponse<WorkItemResponse>> Get(string query) =>
        (await _client.GetFromJsonAsync<PagedResponse<WorkItemResponse>>($"/workitems/?{query}"))!;

    private async Task<string[]> Titles(string query) =>
        (await Get(query)).Items.Select(item => item.Title).ToArray();

    [Fact]
    public async Task DueDateSortPutsItemsWithoutDateLastInBothDirections()
    {
        await Seed();
        Assert.Equal(["date", "cherry", "banana", "Apple"], await Titles("sort=dueDate"));
        Assert.Equal(["banana", "cherry", "date", "Apple"], await Titles("sort=dueDate&desc=true"));
    }

    [Fact]
    public async Task StatusSortFollowsWorkflowOrderAndTitleSortIgnoresCase()
    {
        await Seed();
        Assert.Equal(["Apple", "date", "cherry", "banana"], await Titles("sort=status"));
        Assert.Equal(["Apple", "banana", "cherry", "date"], await Titles("sort=title"));
    }

    [Fact]
    public async Task FiltersByStatusAndSearchesCaseInsensitivelyWithLiteralWildcards()
    {
        await Seed();
        Assert.Equal(["Apple", "date"], await Titles("status=Todo&sort=title"));
        Assert.Equal(["cherry"], await Titles("search=linq"));
        Assert.Equal(["banana"], await Titles("search=100%25"));
        Assert.Empty(await Titles("search=_"));
    }

    [Fact]
    public async Task PagesReportTheFilteredTotal()
    {
        await Seed();
        var page = await Get("sort=title&page=2&pageSize=3");
        Assert.Equal(4, page.Total);
        Assert.Equal(["date"], page.Items.Select(item => item.Title));
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/workitems/?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/workitems/?page=0")).StatusCode);
    }
}
