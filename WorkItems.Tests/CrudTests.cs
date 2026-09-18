using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WorkItems.Api;
using Xunit;

namespace WorkItems.Tests;

public sealed class CrudTests
{
    [Fact]
    public async Task WorkItemCanBeCreatedReadUpdatedAndDeleted()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"workitems-test-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:WorkItems", $"Data Source={databasePath}"));
            using var client = factory.CreateClient();

            // Keep the browser entry point and its module available alongside the API.
            Assert.Contains("Work Items", await client.GetStringAsync("/"));
            Assert.Contains("getItems", await client.GetStringAsync("/js/app.js"));

            var create = await client.PostAsJsonAsync("/workitems/",
                new WorkItemInput("Learn C#", null, WorkItemStatus.Todo, null));
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<WorkItemResponse>();
            Assert.NotNull(created);

            var all = await client.GetFromJsonAsync<List<WorkItemResponse>>("/workitems/");
            Assert.Single(all!);
            Assert.Equal(created.Id, all![0].Id);

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
        finally
        {
            foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
