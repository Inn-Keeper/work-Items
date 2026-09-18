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
    // Optimistic concurrency: EF adds "AND Version = @original" to UPDATE/DELETE and throws if no row matched.
    [ConcurrencyCheck] public int Version { get; set; }
}

// Only editable fields come from the client; Id and CreatedAt are server-owned.
public sealed record WorkItemInput(
    [property: Required, MaxLength(200)] string Title,
    string? Description,
    WorkItemStatus Status,
    DateTimeOffset? DueDate);

public sealed record WorkItemResponse(
    int Id, string Title, string? Description, WorkItemStatus Status,
    DateTimeOffset? DueDate, DateTimeOffset CreatedAt, int Version)
{
    public static WorkItemResponse From(WorkItem item) =>
        new(item.Id, item.Title, item.Description, item.Status, item.DueDate, item.CreatedAt, item.Version);
}

public enum WorkItemSort { DueDate, CreatedAt, Title, Status }

// Query string for GET /workitems; bound with [AsParameters]. Status and Sort are strings because
// minimal APIs bind enums case-sensitively, and clients send camelCase ("dueDate").
public sealed record WorkItemQuery(
    string? Status = null,
    string? Search = null,
    string? Sort = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 50)
{
    public const int MaxPageSize = 100;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
