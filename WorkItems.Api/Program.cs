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

// Read-only queries skip EF change tracking.
items.MapGet("/", async (WorkItemsDb db) =>
    (await db.WorkItems.AsNoTracking().OrderBy(item => item.Id).ToListAsync())
        .Select(WorkItemResponse.From))
    .Produces<IEnumerable<WorkItemResponse>>(200);

items.MapGet("/{id:int}", async (int id, WorkItemsDb db) =>
    await db.WorkItems.AsNoTracking().Where(item => item.Id == id)
        .Select(item => new WorkItemResponse(item.Id, item.Title, item.Description,
            item.Status, item.DueDate, item.CreatedAt)).SingleOrDefaultAsync() is { } item
        ? Results.Ok(item) : Results.NotFound())
    .Produces<WorkItemResponse>(200).Produces(404);

items.MapPost("/", async (WorkItemInput input, WorkItemsDb db) =>
{
    if (Validate(input) is { } error) return Results.ValidationProblem(error);
    var item = new WorkItem
    {
        Title = input.Title.Trim(), Description = input.Description,
        Status = input.Status, DueDate = input.DueDate
    };
    db.WorkItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/workitems/{item.Id}", WorkItemResponse.From(item));
}).Produces<WorkItemResponse>(201).Produces(400);

items.MapPut("/{id:int}", async (int id, WorkItemInput input, WorkItemsDb db) =>
{
    if (Validate(input) is { } error) return Results.ValidationProblem(error);
    var item = await db.WorkItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    item.Title = input.Title.Trim();
    item.Description = input.Description;
    item.Status = input.Status;
    item.DueDate = input.DueDate;
    await db.SaveChangesAsync();
    return Results.Ok(WorkItemResponse.From(item));
}).Produces<WorkItemResponse>(200).Produces(400).Produces(404);

items.MapDelete("/{id:int}", async (int id, WorkItemsDb db) =>
{
    var item = await db.WorkItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    db.WorkItems.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).Produces(204).Produces(404);

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

public partial class Program { }
