using System.Net;
using System.Net.Http.Json;
using WorkItems.Api;
using WorkItems.Contracts;
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

        var put = new HttpRequestMessage(HttpMethod.Put, $"/workitems/{created.Id}")
        {
            Content = JsonContent.Create(new WorkItemInput("Learn EF Core", "Updated", WorkItemStatus.Done, null))
        };
        put.Headers.TryAddWithoutValidation("If-Match", $"\"{created.Version}\"");
        var update = await client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.Equal(WorkItemStatus.Done, updated!.Status);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);

        HttpRequestMessage Delete()
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/workitems/{created.Id}");
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{updated.Version}\"");
            return request;
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Delete())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/workitems/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Delete())).StatusCode);
    }

    [Fact]
    public async Task InvalidInputAndErrorsReturnProblemDetails()
    {
        using var api = new TestApi();
        using var client = api.Factory.CreateClient();

        foreach (var input in new[]
        {
            new WorkItemInput(" ", null, WorkItemStatus.Todo, null),
            new WorkItemInput(new string('x', WorkItemLimits.TitleMaxLength + 1), null, WorkItemStatus.Todo, null),
            new WorkItemInput("Ok", new string('x', WorkItemLimits.DescriptionMaxLength + 1), WorkItemStatus.Todo, null),
        })
        {
            var response = await client.PostAsJsonAsync("/workitems/", input);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }

        // Bodiless results (404) also come back as ProblemDetails, so clients can parse every error.
        var missing = await client.GetAsync("/workitems/999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }
}
