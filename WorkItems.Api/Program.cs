using Microsoft.EntityFrameworkCore;
using WorkItems.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<WorkItemsDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WorkItems") ?? "Data Source=workitems.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddValidation(); // Enforces the DataAnnotations and IValidatableObject rules on endpoint parameters.

var app = builder.Build();
// Every error, including unhandled exceptions and bodiless 404s, returns ProblemDetails JSON,
// so clients can always parse an error response the same way.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Fine for a single instance. With several instances (e.g. scaled-out App Service), run
// migrations as a deploy step instead, so instances don't race to migrate on startup.
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<WorkItemsDb>().Database.MigrateAsync();

// API docs are for local exploration; `dotnet run` uses Properties/launchSettings.json (Development).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseDefaultFiles();
app.UseStaticFiles();

var items = app.MapGroup("/workitems");

// Filtering, sorting and paging run in SQL; read-only queries skip EF change tracking.
items.MapGet("/", async ([AsParameters] WorkItemQuery query, WorkItemsDb db) =>
{
    var status = query.ParsedStatus;
    var sort = query.ParsedSort;

    var filtered = db.WorkItems.AsNoTracking();
    if (status is not null)
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

    var total = await filtered.CountAsync();
    var page = await Sort(filtered, sort, query.Desc).ThenBy(item => item.Id)
        .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
        .Select(WorkItemResponse.Projection)
        .ToListAsync();
    return Results.Ok(new PagedResponse<WorkItemResponse>(page, total, query.Page, query.PageSize));
}).Produces<PagedResponse<WorkItemResponse>>(200).Produces(400);

items.MapGet("/{id:int}", async (int id, WorkItemsDb db, HttpResponse response) =>
{
    var item = await db.WorkItems.Where(item => item.Id == id).Select(WorkItemResponse.Projection).SingleOrDefaultAsync();
    if (item is null) return Results.NotFound();
    response.Headers.ETag = ETag(item.Version);
    return Results.Ok(item);
}).Produces<WorkItemResponse>(200).Produces(404);

items.MapPost("/", async (WorkItemInput input, WorkItemsDb db, HttpResponse response) =>
{
    var item = new WorkItem
    {
        Title = input.Title.Trim(), Description = input.Description,
        Status = input.Status, DueDate = input.DueDate?.ToUniversalTime(),
        Tags = await ResolveTags(db, input.NormalizedTags())
    };
    db.WorkItems.Add(item);
    await db.SaveChangesAsync();
    response.Headers.ETag = ETag(item.Version);
    return Results.Created($"/workitems/{item.Id}", WorkItemResponse.From(item));
}).Produces<WorkItemResponse>(201).Produces(400);

// PUT and DELETE require If-Match with the version the client last saw, so a stale client
// cannot silently overwrite someone else's change (lost update).
items.MapPut("/{id:int}", async (int id, WorkItemInput input, WorkItemsDb db, HttpRequest request, HttpResponse response) =>
{
    if (IfMatchVersion(request) is not { } expected) return IfMatchError(request);
    var item = await db.WorkItems.Include(item => item.Tags).SingleOrDefaultAsync(item => item.Id == id);
    if (item is null) return Results.NotFound();

    // Compare against the client's version, not the one just read: the check happens in the
    // UPDATE's WHERE clause, so a write that lands between our read and SaveChanges is still caught.
    db.Entry(item).Property(value => value.Version).OriginalValue = expected;
    item.Title = input.Title.Trim();
    item.Description = input.Description;
    item.Status = input.Status;
    item.DueDate = input.DueDate?.ToUniversalTime();
    var tags = await ResolveTags(db, input.NormalizedTags());
    item.Tags.Clear();
    item.Tags.AddRange(tags); // EF diffs the join rows; bumping Version still guards the whole edit.
    item.Version = expected + 1;
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateConcurrencyException) { return VersionConflict(); }

    response.Headers.ETag = ETag(item.Version);
    return Results.Ok(WorkItemResponse.From(item));
}).Produces<WorkItemResponse>(200).Produces(400).Produces(404).Produces(412).Produces(428);

items.MapDelete("/{id:int}", async (int id, WorkItemsDb db, HttpRequest request) =>
{
    if (IfMatchVersion(request) is not { } expected) return IfMatchError(request);
    var item = await db.WorkItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    db.Entry(item).Property(value => value.Version).OriginalValue = expected;
    db.WorkItems.Remove(item);
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateConcurrencyException) { return VersionConflict(); }
    return Results.NoContent();
}).Produces(204).Produces(404).Produces(412).Produces(428);

// Tags in use, for filter suggestions. Tags left without items stay in the table but are hidden here.
app.MapGet("/tags", async (WorkItemsDb db) =>
    await db.Tags.Where(tag => tag.WorkItems.Any()).OrderBy(tag => tag.Name)
        .Select(tag => new TagCount(tag.Name, tag.WorkItems.Count)).ToListAsync())
    .Produces<List<TagCount>>(200);

app.Run();

// Reuses existing tags (matched case-insensitively via the NOCASE column) and creates the rest.
// ponytail: two requests creating the same new tag at once hit the unique index (500); retry on conflict if that matters.
static async Task<List<Tag>> ResolveTags(WorkItemsDb db, List<string> names)
{
    if (names.Count == 0) return [];
    var existing = await db.Tags.Where(tag => names.Contains(tag.Name)).ToListAsync();
    return names.Select(name =>
        existing.FirstOrDefault(tag => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? new Tag { Name = name }).ToList();
}

static string ETag(int version) => $"\"{version}\"";

// Only a single strong tag like "3" is a version. Weak tags (W/"3") can't be used with If-Match
// (RFC 9110), and "*" or a list can't identify the version the client edited.
static int? IfMatchVersion(HttpRequest request)
{
    var value = request.Headers.IfMatch.ToString();
    return value.Length > 2 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out var version)
        ? version : null;
}

static IResult IfMatchError(HttpRequest request) => request.Headers.IfMatch.Count == 0
    ? Results.Problem(statusCode: StatusCodes.Status428PreconditionRequired,
        title: "If-Match header required", detail: "Send If-Match with the item's current ETag (its version).")
    : Results.Problem(statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid If-Match header", detail: "Send a single strong ETag, e.g. If-Match: \"3\".");

static IResult VersionConflict() => Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed,
    title: "Item was changed elsewhere", detail: "Reload the item, or retry with its current version to overwrite.");

static IOrderedQueryable<WorkItem> Sort(IQueryable<WorkItem> items, WorkItemSort sort, bool desc) => sort switch
{
    // Items without a due date go last in either direction.
    WorkItemSort.DueDate => desc
        ? items.OrderBy(item => item.DueDate == null).ThenByDescending(item => item.DueDate)
        : items.OrderBy(item => item.DueDate == null).ThenBy(item => item.DueDate),
    WorkItemSort.CreatedAt => desc ? items.OrderByDescending(item => item.CreatedAt) : items.OrderBy(item => item.CreatedAt),
    WorkItemSort.Title => desc
        ? items.OrderByDescending(item => EF.Functions.Collate(item.Title, "NOCASE"))
        : items.OrderBy(item => EF.Functions.Collate(item.Title, "NOCASE")),
    // Status is stored as text, so a cast or plain OrderBy would sort alphabetically; CASE keeps workflow order.
    WorkItemSort.Status => desc ? items.OrderByDescending(StatusRank) : items.OrderBy(StatusRank),
    _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Add a sort case for this option."),
};

public partial class Program
{
    // Must list every status in workflow order; anything unlisted sorts last.
    private static readonly System.Linq.Expressions.Expression<Func<WorkItem, int>> StatusRank = item =>
        item.Status == WorkItemStatus.Todo ? 0 : item.Status == WorkItemStatus.InProgress ? 1 : 2;
}
