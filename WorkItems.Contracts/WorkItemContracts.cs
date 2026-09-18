using System.ComponentModel.DataAnnotations;

namespace WorkItems.Contracts;

public enum WorkItemStatus { Todo, InProgress, Done }

public enum WorkItemSort { DueDate, CreatedAt, Title, Status }

/// <summary>Limits enforced by the API; UIs mirror them in their input fields.</summary>
public static class WorkItemLimits
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int TagMaxLength = 30;
    public const int MaxTags = 10;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

// Only editable fields come from the client; Id, CreatedAt and Version are server-owned
// (the client echoes Version back in If-Match, not in the body).
// The API validates this (AddValidation) before the endpoint runs; failures return 400 ProblemDetails.
public sealed record WorkItemInput(
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(WorkItemLimits.TitleMaxLength, ErrorMessage = "Title must be at most {1} characters.")]
    string Title,
    [MaxLength(WorkItemLimits.DescriptionMaxLength, ErrorMessage = "Description must be at most {1} characters.")]
    string? Description,
    [EnumDataType(typeof(WorkItemStatus), ErrorMessage = "Status is invalid.")]
    WorkItemStatus Status,
    DateTimeOffset? DueDate,
    IReadOnlyList<string>? Tags = null) : IValidatableObject
{
    // Tag rules apply after normalizing (trim, drop blanks, de-duplicate), which attributes can't express.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var tags = NormalizedTags();
        if (tags.Count > WorkItemLimits.MaxTags)
            yield return new($"Use at most {WorkItemLimits.MaxTags} tags.", [nameof(Tags)]);
        if (tags.Any(tag => tag.Length > WorkItemLimits.TagMaxLength))
            yield return new($"Each tag must be at most {WorkItemLimits.TagMaxLength} characters.", [nameof(Tags)]);
    }

    /// <summary>Trimmed, blanks dropped, duplicates removed ignoring case (first spelling wins).</summary>
    public List<string> NormalizedTags() =>
        (Tags ?? []).Select(tag => tag.Trim()).Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

public sealed record WorkItemResponse(
    int Id, string Title, string? Description, WorkItemStatus Status,
    DateTimeOffset? DueDate, DateTimeOffset CreatedAt, int Version, IReadOnlyList<string> Tags);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record TagCount(string Name, int Count);

/// <summary>Typed filters for GET /workitems, for .NET clients.</summary>
public sealed record WorkItemListQuery(
    WorkItemStatus? Status = null,
    string? Search = null,
    string? Tag = null,
    WorkItemSort Sort = WorkItemSort.DueDate,
    bool Desc = false,
    int Page = 1,
    int PageSize = WorkItemLimits.DefaultPageSize)
{
    // Keys must match the API's query parameters (a test checks each property has one).
    public string ToQueryString()
    {
        var parts = new List<string> { $"sort={Sort}", $"desc={Desc}", $"page={Page}", $"pageSize={PageSize}" };
        if (Status is { } status) parts.Add($"status={status}");
        if (!string.IsNullOrWhiteSpace(Search)) parts.Add($"search={Uri.EscapeDataString(Search.Trim())}");
        if (!string.IsNullOrWhiteSpace(Tag)) parts.Add($"tag={Uri.EscapeDataString(Tag.Trim())}");
        return string.Join('&', parts);
    }
}
