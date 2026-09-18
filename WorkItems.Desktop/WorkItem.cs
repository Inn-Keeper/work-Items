using System.Globalization;

namespace WorkItems.Desktop;

internal enum WorkItemStatus { Todo, InProgress, Done }

internal sealed record WorkItem(
    int Id, string Title, string? Description, WorkItemStatus Status,
    DateTimeOffset? DueDate, DateTimeOffset CreatedAt, int Version)
{
    public override string ToString() =>
        $"{StatusLabel(Status),-12}  {Title}" +
        (DueDate is { } due ? $"  ·  Due {FormatDate(due)}" : "");

    public static string StatusLabel(WorkItemStatus status) => status switch
    {
        WorkItemStatus.Todo => "Todo",
        WorkItemStatus.InProgress => "In progress",
        WorkItemStatus.Done => "Done",
        _ => "Unknown"
    };

    public static string FormatDate(DateTimeOffset date) =>
        date.UtcDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    public static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParseExact(value.Trim(), "dd-MM-yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ArgumentException("Use a valid due date in DD-MM-YYYY format.");
        return new DateTimeOffset(date, TimeSpan.Zero);
    }
}

internal sealed record WorkItemInput(
    string Title, string? Description, WorkItemStatus Status, DateTimeOffset? DueDate);

internal enum ItemSort { DueDate, CreatedAt, Title, Status }

internal sealed record ItemQuery(
    WorkItemStatus? Status = null, string? Search = null, ItemSort Sort = ItemSort.DueDate,
    bool Desc = false, int Page = 1, int PageSize = 50);

internal sealed record ItemPage(List<WorkItem> Items, int Total, int Page, int PageSize);
