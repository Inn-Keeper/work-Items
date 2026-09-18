using Microsoft.EntityFrameworkCore;

namespace WorkItems.Api;

internal static class TagResolver
{
    // Reuses existing tags (matched case-insensitively via the NOCASE column) and creates the rest.
    // ponytail: two requests creating the same new tag at once hit the unique index (500); retry on conflict if that matters.
    public static async Task<List<Tag>> ResolveAsync(WorkItemsDb db, List<string> names, CancellationToken ct)
    {
        if (names.Count == 0) return [];
        var existing = await db.Tags.Where(tag => names.Contains(tag.Name)).ToListAsync(ct);
        return names.Select(name =>
            existing.FirstOrDefault(tag => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? new Tag { Name = name }).ToList();
    }
}
