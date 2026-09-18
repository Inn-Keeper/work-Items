using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

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
    public List<Tag> Tags { get; set; } = [];
}

// Many-to-many with WorkItem; EF creates the join table.
public sealed class Tag
{
    public const int MaxLength = 30;
    public int Id { get; set; }
    [MaxLength(MaxLength)] public required string Name { get; set; }
    public List<WorkItem> WorkItems { get; set; } = [];
}

public sealed record TagCount(string Name, int Count);

// Only editable fields come from the client; Id and CreatedAt are server-owned.
public sealed record WorkItemInput(
    [property: Required, MaxLength(200)] string Title,
    string? Description,
    WorkItemStatus Status,
    DateTimeOffset? DueDate,
    IReadOnlyList<string>? Tags = null)
{
    public const int MaxTags = 10;
}

public sealed record WorkItemResponse(
    int Id, string Title, string? Description, WorkItemStatus Status,
    DateTimeOffset? DueDate, DateTimeOffset CreatedAt, int Version, IReadOnlyList<string> Tags)
{
    // An expression (not a method) so EF translates it: tag names come back in the same SQL query
    // as their items instead of one extra query per item (N+1).
    public static readonly Expression<Func<WorkItem, WorkItemResponse>> Projection = item =>
        new(item.Id, item.Title, item.Description, item.Status, item.DueDate, item.CreatedAt, item.Version,
            item.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList());

    private static readonly Func<WorkItem, WorkItemResponse> Compiled = Projection.Compile();

    /// <summary>For an entity already in memory; its Tags must be loaded.</summary>
    public static WorkItemResponse From(WorkItem item) => Compiled(item);
}

public enum WorkItemSort { DueDate, CreatedAt, Title, Status }

// Query string for GET /workitems; bound with [AsParameters]. Status and Sort are strings because
// minimal APIs bind enums case-sensitively, and clients send camelCase ("dueDate").
public sealed record WorkItemQuery(
    string? Status = null,
    string? Search = null,
    string? Tag = null,
    string? Sort = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 50)
{
    public const int MaxPageSize = 100;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
