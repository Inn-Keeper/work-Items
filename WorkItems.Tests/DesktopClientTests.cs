using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WorkItems.Desktop;
using Xunit;

namespace WorkItems.Tests;

public sealed class DesktopClientTests
{
    [Fact]
    public async Task DesktopClientUsesTheApiForCrud()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"workitems-desktop-test-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:WorkItems", $"Data Source={databasePath}"));
            using var client = new WorkItemsClient(factory.CreateClient());

            var dueDate = WorkItem.ParseDate("29-02-2028");
            Assert.Equal("29-02-2028", WorkItem.FormatDate(dueDate!.Value));
            Assert.Throws<ArgumentException>(() => WorkItem.ParseDate("31-02-2028"));

            await client.SaveAsync(null, new WorkItemInput("Desktop item", null, WorkItemStatus.Todo, dueDate));
            var created = Assert.Single(await client.GetItemsAsync());
            Assert.Equal("Desktop item", created.Title);
            Assert.Equal("29-02-2028", WorkItem.FormatDate(created.DueDate!.Value));

            await client.SaveAsync(created.Id,
                new WorkItemInput("Updated item", "Done", WorkItemStatus.Done, null));
            var updated = Assert.Single(await client.GetItemsAsync());
            Assert.Equal(WorkItemStatus.Done, updated.Status);

            await client.DeleteAsync(created.Id);
            Assert.Empty(await client.GetItemsAsync());
        }
        finally
        {
            foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
