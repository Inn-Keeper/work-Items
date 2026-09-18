using Microsoft.EntityFrameworkCore;
using WorkItems.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<WorkItemsDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WorkItems") ?? "Data Source=workitems.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
// Apply versioned schema changes on startup.
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<WorkItemsDb>().Database.MigrateAsync();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

var items = app.MapGroup("/workitems");

// Filtering, sorting and paging run in SQL; read-only queries skip EF change tracking.
items.MapGet("/", async ([AsParameters] WorkItemQuery query, WorkItemsDb db) =>
{
    if (ValidateQuery(query) is { } error) return Results.ValidationProblem(error);
    var status = ParseOrDefault<WorkItemStatus>(query.Status);
    var sort = ParseOrDefault<WorkItemSort>(query.Sort) ?? WorkItemSort.DueDate;

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

    var total = await filtered.CountAsync();
    var page = await Sort(filtered, sort, query.Desc).ThenBy(item => item.Id)
        .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
        .ToListAsync();
    return Results.Ok(new PagedResponse<WorkItemResponse>(
        page.Select(WorkItemResponse.From).ToList(), total, query.Page, query.PageSize));
}).Produces<PagedResponse<WorkItemResponse>>(200).Produces(400);

items.MapGet("/{id:int}", async (int id, WorkItemsDb db, HttpResponse response) =>
{
    var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
    if (item is null) return Results.NotFound();
    response.Headers.ETag = ETag(item.Version);
    return Results.Ok(WorkItemResponse.From(item));
}).Produces<WorkItemResponse>(200).Produces(404);

items.MapPost("/", async (WorkItemInput input, WorkItemsDb db, HttpResponse response) =>
{
    if (Validate(input) is { } error) return Results.ValidationProblem(error);
    var item = new WorkItem
    {
        Title = input.Title.Trim(), Description = input.Description,
        Status = input.Status, DueDate = input.DueDate?.ToUniversalTime()
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
    if (Validate(input) is { } error) return Results.ValidationProblem(error);
    if (IfMatchVersion(request) is not { } expected) return IfMatchRequired();
    var item = await db.WorkItems.FindAsync(id);
    if (item is null) return Results.NotFound();

    // Compare against the client's version, not the one just read: the check happens in the
    // UPDATE's WHERE clause, so a write that lands between our read and SaveChanges is still caught.
    db.Entry(item).Property(value => value.Version).OriginalValue = expected;
    item.Title = input.Title.Trim();
    item.Description = input.Description;
    item.Status = input.Status;
    item.DueDate = input.DueDate?.ToUniversalTime();
    item.Version = expected + 1;
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateConcurrencyException) { return VersionConflict(); }

    response.Headers.ETag = ETag(item.Version);
    return Results.Ok(WorkItemResponse.From(item));
}).Produces<WorkItemResponse>(200).Produces(400).Produces(404).Produces(412).Produces(428);

items.MapDelete("/{id:int}", async (int id, WorkItemsDb db, HttpRequest request) =>
{
    if (IfMatchVersion(request) is not { } expected) return IfMatchRequired();
    var item = await db.WorkItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    db.Entry(item).Property(value => value.Version).OriginalValue = expected;
    db.WorkItems.Remove(item);
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateConcurrencyException) { return VersionConflict(); }
    return Results.NoContent();
}).Produces(204).Produces(404).Produces(412).Produces(428);

app.Run();

// Return field errors as ProblemDetails before writing to the database.
static Dictionary<string, string[]>? Validate(WorkItemInput input)
{
    if (string.IsNullOrWhiteSpace(input.Title))
        return new() { ["title"] = ["Title is required."] };
    if (input.Title.Length > 200)
        return new() { ["title"] = ["Title must be at most 200 characters."] };
    if (!Enum.IsDefined(input.Status))
        return new() { ["status"] = ["Status is invalid."] };
    return null;
}

static string ETag(int version) => $"\"{version}\"";

// Accepts If-Match: "3" (or W/"3"); returns null when missing or not a version number.
static int? IfMatchVersion(HttpRequest request) =>
    int.TryParse(request.Headers.IfMatch.ToString().Replace("W/", "").Trim('"'), out var version) ? version : null;

static IResult IfMatchRequired() => Results.Problem(statusCode: StatusCodes.Status428PreconditionRequired,
    title: "If-Match header required", detail: "Send If-Match with the item's current ETag (its version).");

static IResult VersionConflict() => Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed,
    title: "Item was changed elsewhere", detail: "Reload the item, or retry with its current version to overwrite.");

static Dictionary<string, string[]>? ValidateQuery(WorkItemQuery query)
{
    if (query.Status is not null && ParseOrDefault<WorkItemStatus>(query.Status) is null)
        return new() { ["status"] = [$"Status must be one of: {string.Join(", ", Enum.GetNames<WorkItemStatus>())}."] };
    if (query.Sort is not null && ParseOrDefault<WorkItemSort>(query.Sort) is null)
        return new() { ["sort"] = [$"Sort must be one of: {string.Join(", ", Enum.GetNames<WorkItemSort>())}."] };
    if (query.Page < 1)
        return new() { ["page"] = ["Page must be 1 or greater."] };
    if (query.PageSize is < 1 or > WorkItemQuery.MaxPageSize)
        return new() { ["pageSize"] = [$"Page size must be between 1 and {WorkItemQuery.MaxPageSize}."] };
    return null;
}

// Accepts names in any case or numeric values that map to a defined member.
static T? ParseOrDefault<T>(string? value) where T : struct, Enum =>
    Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

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
    _ => desc ? items.OrderByDescending(StatusRank) : items.OrderBy(StatusRank),
};

public partial class Program
{
    private static readonly System.Linq.Expressions.Expression<Func<WorkItem, int>> StatusRank = item =>
        item.Status == WorkItemStatus.Todo ? 0 : item.Status == WorkItemStatus.InProgress ? 1 : 2;
}
