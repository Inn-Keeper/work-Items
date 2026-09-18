using System.Net;
using System.Net.Http.Json;
using WorkItems.Api;
using Xunit;

namespace WorkItems.Tests;

public sealed class CrudTests
{
    [Fact]
    public async Task WorkItemCanBeCreatedReadUpdatedAndDeleted()
    {
        using var api = new TestApi();
        using var client = api.Factory.CreateClient();

        // Keep the browser entry point and its module available alongside the API.
        Assert.Contains("Work Items", await client.GetStringAsync("/"));
        Assert.Contains("getItems", await client.GetStringAsync("/js/app.js"));

        var create = await client.PostAsJsonAsync("/workitems/",
            new WorkItemInput("Learn C#", null, WorkItemStatus.Todo, null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.NotNull(created);

        var all = await client.GetFromJsonAsync<PagedResponse<WorkItemResponse>>("/workitems/");
        Assert.Equal(created.Id, Assert.Single(all!.Items).Id);

        var read = await client.GetFromJsonAsync<WorkItemResponse>($"/workitems/{created.Id}");
        Assert.Equal("Learn C#", read!.Title);

        var update = await client.PutAsJsonAsync($"/workitems/{created.Id}",
            new WorkItemInput("Learn EF Core", "Updated", WorkItemStatus.Done, null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.Equal(WorkItemStatus.Done, updated!.Status);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);

        var delete = await client.DeleteAsync($"/workitems/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/workitems/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/workitems/{created.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/workitems/",
                new WorkItemInput(" ", null, WorkItemStatus.Todo, null))).StatusCode);
    }
}
