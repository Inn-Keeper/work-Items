using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Api;

namespace WorkItems.Tests;

// In-memory API host backed by a throwaway SQLite file, so tests never touch workitems.db.
internal sealed class TestApi : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"workitems-test-{Guid.NewGuid():N}.db");
    public WebApplicationFactory<Program> Factory { get; }
    /// <summary>Counts SQL commands the API runs, to catch N+1 queries.</summary>
    public QueryCounter Queries { get; } = new();

    public TestApi() =>
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:WorkItems", $"Data Source={_databasePath}")
            .ConfigureServices(services => services.ConfigureDbContext<WorkItemsDb>(options => options.AddInterceptors(Queries))));

    public void Dispose()
    {
        Factory.Dispose();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }
}

internal sealed class QueryCounter : DbCommandInterceptor
{
    private int _count;
    public int Count => _count;
    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
