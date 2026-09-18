using WorkItems.Desktop;
using Xunit;

namespace WorkItems.Tests;

public sealed class DesktopClientTests
{
    [Fact]
    public async Task DesktopClientUsesTheApiForCrud()
    {
        using var api = new TestApi();
        using var client = new WorkItemsClient(api.Factory.CreateClient());

        var dueDate = WorkItem.ParseDate("29-02-2028");
        Assert.Equal("29-02-2028", WorkItem.FormatDate(dueDate!.Value));
        Assert.Throws<ArgumentException>(() => WorkItem.ParseDate("31-02-2028"));

        var created = await client.SaveAsync(null, new WorkItemInput("Desktop item", null, WorkItemStatus.Todo, dueDate));
        Assert.Equal(created.Id, Assert.Single((await client.GetItemsAsync()).Items).Id);
        Assert.Equal("Desktop item", created.Title);
        Assert.Equal("29-02-2028", WorkItem.FormatDate(created.DueDate!.Value));

        await client.SaveAsync(created.Id,
            new WorkItemInput("Updated item", "Done", WorkItemStatus.Done, null));
        var found = await client.GetItemsAsync(new ItemQuery(Status: WorkItemStatus.Done, Search: "updated"));
        Assert.Equal(WorkItemStatus.Done, Assert.Single(found.Items).Status);

        await client.DeleteAsync(created.Id);
        Assert.Equal(0, (await client.GetItemsAsync()).Total);
    }
}
