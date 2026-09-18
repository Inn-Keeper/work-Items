using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace WorkItems.Api;

public enum WorkItemStatus { Todo, InProgress, Done }

// EF Core entity: storage details stay inside the API.
public sealed class WorkItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public int Id { get; set; }
    [Required, MaxLength(TitleMaxLength)] public string Title { get; set; } = "";
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

// Only editable fields come from the client; Id, CreatedAt and Version are server-owned
// (the client echoes Version back in If-Match, not in the body).
// Validated by AddValidation() before the endpoint runs; failures return 400 ProblemDetails.
public sealed record WorkItemInput(
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(WorkItem.TitleMaxLength, ErrorMessage = "Title must be at most {1} characters.")]
    string Title,
    [MaxLength(WorkItem.DescriptionMaxLength, ErrorMessage = "Description must be at most {1} characters.")]
    string? Description,
    [EnumDataType(typeof(WorkItemStatus), ErrorMessage = "Status is invalid.")]
    WorkItemStatus Status,
    DateTimeOffset? DueDate,
    IReadOnlyList<string>? Tags = null) : IValidatableObject
{
    public const int MaxTags = 10;

    // Tag rules apply after normalizing (trim, drop blanks, de-duplicate), which attributes can't express.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var tags = NormalizedTags();
        if (tags.Count > MaxTags)
            yield return new($"Use at most {MaxTags} tags.", [nameof(Tags)]);
        if (tags.Any(tag => tag.Length > Tag.MaxLength))
            yield return new($"Each tag must be at most {Tag.MaxLength} characters.", [nameof(Tags)]);
    }

    /// <summary>Trimmed, blanks dropped, duplicates removed ignoring case (first spelling wins).</summary>
    public List<string> NormalizedTags() =>
        (Tags ?? []).Select(tag => tag.Trim()).Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    int Page = 1,
    [Range(1, WorkItemQuery.MaxPageSize, ErrorMessage = "Page size must be between {1} and {2}.")]
    int PageSize = 50) : IValidatableObject
{
    public const int MaxPageSize = 100;

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

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
