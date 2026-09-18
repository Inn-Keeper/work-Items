using Microsoft.EntityFrameworkCore;
using WorkItems.Contracts;

namespace WorkItems.Api;

public static class WorkItemEndpoints
{
    public static void MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/workitems");
        items.MapGet("/", List).Produces<PagedResponse<WorkItemResponse>>(200).Produces(400);
        items.MapGet("/{id:int}", Get).Produces<WorkItemResponse>(200).Produces(404);
        items.MapPost("/", Create).Produces<WorkItemResponse>(201).Produces(400);
        // PUT and DELETE require If-Match with the version the client last saw, so a stale client
        // cannot silently overwrite someone else's change (lost update).
        items.MapPut("/{id:int}", Update).Produces<WorkItemResponse>(200).Produces(400).Produces(404).Produces(412).Produces(428);
        items.MapDelete("/{id:int}", Delete).Produces(204).Produces(404).Produces(412).Produces(428);

        app.MapGet("/tags", ListTags).Produces<List<TagCount>>(200);
    }

    // Filtering, sorting and paging run in SQL; read-only queries skip EF change tracking.
    private static async Task<IResult> List([AsParameters] WorkItemQuery query, WorkItemsDb db, CancellationToken ct)
    {
        var filtered = db.WorkItems.AsNoTracking();
        if (query.ParsedStatus is { } status)
            filtered = filtered.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE is case-insensitive in SQLite (string.Contains is not); escape its wildcards.
            var pattern = "%" + query.Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            filtered = filtered.Where(item => EF.Functions.Like(item.Title, pattern, @"\") ||
                                              (item.Description != null && EF.Functions.Like(item.Description, pattern, @"\")));
        }
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            filtered = filtered.Where(item => item.Tags.Any(value => value.Name == tag));
        }

        var total = await filtered.CountAsync(ct);
        var page = await filtered.SortBy(query.ParsedSort, query.Desc).ThenBy(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(WorkItemMapping.ToResponse)
            .ToListAsync(ct);
        return Results.Ok(new PagedResponse<WorkItemResponse>(page, total, query.Page, query.PageSize));
    }

    private static async Task<IResult> Get(int id, WorkItemsDb db, HttpResponse response, CancellationToken ct)
    {
        var item = await db.WorkItems.Where(item => item.Id == id).Select(WorkItemMapping.ToResponse).SingleOrDefaultAsync(ct);
        if (item is null) return Results.NotFound();
        response.Headers.ETag = Preconditions.ETag(item.Version);
        return Results.Ok(item);
    }

    private static async Task<IResult> Create(WorkItemInput input, WorkItemsDb db, HttpResponse response, CancellationToken ct)
    {
        var item = new WorkItem
        {
            Title = input.Title.Trim(), Description = input.Description,
            Status = input.Status, DueDate = input.DueDate?.ToUniversalTime(),
            Tags = await TagResolver.ResolveAsync(db, input.NormalizedTags(), ct)
        };
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
        response.Headers.ETag = Preconditions.ETag(item.Version);
        return Results.Created($"/workitems/{item.Id}", item.ToResponseFromLoaded());
    }

    private static async Task<IResult> Update(
        int id, WorkItemInput input, WorkItemsDb db, HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        if (Preconditions.IfMatchVersion(request) is not { } expected) return Preconditions.IfMatchError(request);
        var item = await db.WorkItems.Include(item => item.Tags).SingleOrDefaultAsync(item => item.Id == id, ct);
        if (item is null) return Results.NotFound();

        // Compare against the client's version, not the one just read: the check happens in the
        // UPDATE's WHERE clause, so a write that lands between our read and SaveChanges is still caught.
        db.Entry(item).Property(value => value.Version).OriginalValue = expected;
        item.Title = input.Title.Trim();
        item.Description = input.Description;
        item.Status = input.Status;
        item.DueDate = input.DueDate?.ToUniversalTime();
        var tags = await TagResolver.ResolveAsync(db, input.NormalizedTags(), ct);
        item.Tags.Clear();
        item.Tags.AddRange(tags); // EF diffs the join rows; bumping Version still guards the whole edit.
        item.Version = expected + 1;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Preconditions.VersionConflict(); }

        response.Headers.ETag = Preconditions.ETag(item.Version);
        return Results.Ok(item.ToResponseFromLoaded());
    }

    private static async Task<IResult> Delete(int id, WorkItemsDb db, HttpRequest request, CancellationToken ct)
    {
        if (Preconditions.IfMatchVersion(request) is not { } expected) return Preconditions.IfMatchError(request);
        var item = await db.WorkItems.FindAsync([id], ct);
        if (item is null) return Results.NotFound();
        db.Entry(item).Property(value => value.Version).OriginalValue = expected;
        db.WorkItems.Remove(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Preconditions.VersionConflict(); }
        return Results.NoContent();
    }

    // Tags in use, for filter suggestions. Tags left without items stay in the table but are hidden here.
    private static async Task<List<TagCount>> ListTags(WorkItemsDb db, CancellationToken ct) =>
        await db.Tags.Where(tag => tag.WorkItems.Any()).OrderBy(tag => tag.Name)
            .Select(tag => new TagCount(tag.Name, tag.WorkItems.Count)).ToListAsync(ct);
}
