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

app.MapWorkItemEndpoints();
app.Run();

// Lets WebApplicationFactory<Program> in the tests find this entry point.
public partial class Program { }
