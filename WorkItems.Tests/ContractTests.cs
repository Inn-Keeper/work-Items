using WorkItems.Api;
using WorkItems.Contracts;
using Xunit;

namespace WorkItems.Tests;

public sealed class ContractTests
{
    // WorkItemListQuery (shared, typed) builds the query string that the API's WorkItemQuery binds.
    // They're separate types because the API binds status/sort as strings, so check the keys still line up.
    [Fact]
    public void EveryListQueryFieldIsAnApiQueryParameter()
    {
        var apiParameters = typeof(WorkItemQuery).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var query = new WorkItemListQuery(WorkItemStatus.Done, "x", "y", WorkItemSort.Title, true, 2, 10);
        var keys = query.ToQueryString().Split('&').Select(part => part.Split('=')[0]).ToList();

        Assert.Equal(typeof(WorkItemListQuery).GetProperties().Length, keys.Count);
        Assert.All(keys, key => Assert.Contains(key, apiParameters));
    }
}
