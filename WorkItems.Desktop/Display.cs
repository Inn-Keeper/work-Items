using System.Globalization;
using WorkItems.Contracts;

namespace WorkItems.Desktop;

/// <summary>How the desktop client shows contract values.</summary>
internal static class Display
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
