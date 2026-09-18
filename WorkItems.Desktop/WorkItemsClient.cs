using System.Net.Http.Json;
using System.Text.Json;

namespace WorkItems.Desktop;

internal sealed class WorkItemsClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkItemsClient() : this(new HttpClient())
    {
        var url = Environment.GetEnvironmentVariable("WORKITEMS_API_URL") ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
    }

    internal WorkItemsClient(HttpClient http) => _http = http;

    public async Task<ItemPage> GetItemsAsync(ItemQuery? query = null)
    {
        query ??= new ItemQuery();
        var url = $"workitems/?sort={query.Sort}&desc={query.Desc}&page={query.Page}&pageSize={query.PageSize}";
        if (query.Status is { } status) url += $"&status={status}";
        if (!string.IsNullOrWhiteSpace(query.Search)) url += $"&search={Uri.EscapeDataString(query.Search.Trim())}";
        return await _http.GetFromJsonAsync<ItemPage>(url, JsonOptions)
            ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<WorkItem> GetItemAsync(int id)
    {
        using var response = await _http.GetAsync($"workitems/{id}");
        await CheckResponseAsync(response);
        return (await response.Content.ReadFromJsonAsync<WorkItem>(JsonOptions))!;
    }

    /// <summary>Creates when <paramref name="existing"/> is null, otherwise updates it if its version is still current.</summary>
    public async Task<WorkItem> SaveAsync(WorkItem? existing, WorkItemInput input)
    {
        using var request = existing is null
            ? new HttpRequestMessage(HttpMethod.Post, "workitems/")
            : IfMatch(new HttpRequestMessage(HttpMethod.Put, $"workitems/{existing.Id}"), existing.Version);
        request.Content = JsonContent.Create(input, options: JsonOptions);
        using var response = await _http.SendAsync(request);
        await CheckResponseAsync(response);
        return (await response.Content.ReadFromJsonAsync<WorkItem>(JsonOptions))!;
    }

    public async Task DeleteAsync(WorkItem item)
    {
        using var request = IfMatch(new HttpRequestMessage(HttpMethod.Delete, $"workitems/{item.Id}"), item.Version);
        using var response = await _http.SendAsync(request);
        await CheckResponseAsync(response);
    }

    private static HttpRequestMessage IfMatch(HttpRequestMessage request, int version)
    {
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return request;
    }

    private static async Task CheckResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("This item no longer exists. Refresh the list.");
        if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            throw new VersionConflictException();

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);
        var fieldError = problem?.Errors?.Values.FirstOrDefault()?.FirstOrDefault();
        throw new InvalidOperationException(fieldError ?? $"API returned {(int)response.StatusCode}.");
    }

    public void Dispose() => _http.Dispose();

    private sealed record ValidationProblem(Dictionary<string, string[]>? Errors);
}

/// <summary>The item changed on the server since this client loaded it (HTTP 412).</summary>
internal sealed class VersionConflictException() : Exception("This item was changed elsewhere.");
