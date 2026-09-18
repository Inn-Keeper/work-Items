using System.Globalization;

namespace WorkItems.Desktop;

internal enum WorkItemStatus { Todo, InProgress, Done }

internal sealed record WorkItem(
    int Id, string Title, string? Description, WorkItemStatus Status,
    DateTimeOffset? DueDate, DateTimeOffset CreatedAt, int Version, List<string>? Tags = null)
{
    public static string StatusLabel(WorkItemStatus status) => status switch
    {
        WorkItemStatus.Todo => "Todo",
        WorkItemStatus.InProgress => "In progress",
        WorkItemStatus.Done => "Done",
        _ => "Unknown"
    };

    public static string FormatDate(DateTimeOffset date) =>
        date.UtcDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}

internal sealed record WorkItemInput(
    string Title, string? Description, WorkItemStatus Status, DateTimeOffset? DueDate,
    IReadOnlyList<string>? Tags = null);

internal enum ItemSort { DueDate, CreatedAt, Title, Status }

internal sealed record ItemQuery(
    WorkItemStatus? Status = null, string? Search = null, string? Tag = null, ItemSort Sort = ItemSort.DueDate,
    bool Desc = false, int Page = 1, int PageSize = 50);

internal sealed record ItemPage(List<WorkItem> Items, int Total, int Page, int PageSize);
