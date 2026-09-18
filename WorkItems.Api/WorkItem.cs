using System.ComponentModel.DataAnnotations;

namespace WorkItems.Api;

public enum WorkItemStatus { Todo, InProgress, Done }

// EF Core entity: storage details stay inside the API.
public sealed class WorkItem
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// Only editable fields come from the client; Id and CreatedAt are server-owned.
public sealed record WorkItemInput(
    [property: Required, MaxLength(200)] string Title,
    string? Description,
    WorkItemStatus Status,
    DateTimeOffset? DueDate);

public sealed record WorkItemResponse(
    int Id, string Title, string? Description, WorkItemStatus Status,
    DateTimeOffset? DueDate, DateTimeOffset CreatedAt)
{
    public static WorkItemResponse From(WorkItem item) =>
        new(item.Id, item.Title, item.Description, item.Status, item.DueDate, item.CreatedAt);
}
