using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace WorkItems.Api;

internal static class WorkItemSorting
{
    // Must list every status in workflow order; anything unlisted sorts last.
    private static readonly Expression<Func<WorkItem, int>> StatusRank = item =>
        item.Status == WorkItemStatus.Todo ? 0 : item.Status == WorkItemStatus.InProgress ? 1 : 2;

    public static IOrderedQueryable<WorkItem> SortBy(this IQueryable<WorkItem> items, WorkItemSort sort, bool desc) => sort switch
    {
        // Items without a due date go last in either direction.
        WorkItemSort.DueDate => desc
            ? items.OrderBy(item => item.DueDate == null).ThenByDescending(item => item.DueDate)
            : items.OrderBy(item => item.DueDate == null).ThenBy(item => item.DueDate),
        WorkItemSort.CreatedAt => desc ? items.OrderByDescending(item => item.CreatedAt) : items.OrderBy(item => item.CreatedAt),
        WorkItemSort.Title => desc
            ? items.OrderByDescending(item => EF.Functions.Collate(item.Title, "NOCASE"))
            : items.OrderBy(item => EF.Functions.Collate(item.Title, "NOCASE")),
        // Status is stored as text, so a cast or plain OrderBy would sort alphabetically; CASE keeps workflow order.
        WorkItemSort.Status => desc ? items.OrderByDescending(StatusRank) : items.OrderBy(StatusRank),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Add a sort case for this option."),
    };
}
