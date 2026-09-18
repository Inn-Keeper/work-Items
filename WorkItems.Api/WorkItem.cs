using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using WorkItems.Contracts;

namespace WorkItems.Api;

// EF Core entity: storage details stay inside the API; clients see WorkItemResponse.
public sealed class WorkItem
{
    public int Id { get; set; }
    [Required, MaxLength(WorkItemLimits.TitleMaxLength)] public string Title { get; set; } = "";
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
    public int Id { get; set; }
    [MaxLength(WorkItemLimits.TagMaxLength)] public required string Name { get; set; }
    public List<WorkItem> WorkItems { get; set; } = [];
}

public static class WorkItemMapping
{
    // An expression (not a method) so EF translates it: tag names come back in the same SQL query
    // as their items instead of one extra query per item (N+1).
    public static readonly Expression<Func<WorkItem, WorkItemResponse>> ToResponse = item =>
        new WorkItemResponse(item.Id, item.Title, item.Description, item.Status, item.DueDate, item.CreatedAt, item.Version,
            item.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList());

    private static readonly Func<WorkItem, WorkItemResponse> Compiled = ToResponse.Compile();

    /// <summary>For an entity already in memory; its Tags must be loaded.</summary>
    public static WorkItemResponse ToResponseFromLoaded(this WorkItem item) => Compiled(item);
}

// Query string for GET /workitems; bound with [AsParameters]. Status and Sort are strings because
// minimal APIs bind enums case-sensitively, and clients send camelCase ("dueDate").
// Property names are the API's query keys; WorkItemListQuery (Contracts) must use the same ones.
public sealed record WorkItemQuery(
    string? Status = null,
    string? Search = null,
    string? Tag = null,
    string? Sort = null,
    bool Desc = false,
    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    int Page = 1,
    [Range(1, WorkItemLimits.MaxPageSize, ErrorMessage = "Page size must be between {1} and {2}.")]
    int PageSize = WorkItemLimits.DefaultPageSize) : IValidatableObject
{
    public WorkItemStatus? ParsedStatus => ParseOrDefault<WorkItemStatus>(Status);
    public WorkItemSort ParsedSort => ParseOrDefault<WorkItemSort>(Sort) ?? WorkItemSort.DueDate;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is not null && ParsedStatus is null)
            yield return new($"Status must be one of: {string.Join(", ", Enum.GetNames<WorkItemStatus>())}.", [nameof(Status)]);
        if (Sort is not null && ParseOrDefault<WorkItemSort>(Sort) is null)
            yield return new($"Sort must be one of: {string.Join(", ", Enum.GetNames<WorkItemSort>())}.", [nameof(Sort)]);
    }

    // Accepts names in any case or numeric values that map to a defined member.
    private static T? ParseOrDefault<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
}
