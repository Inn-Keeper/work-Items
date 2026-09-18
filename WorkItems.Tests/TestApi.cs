using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WorkItems.Tests;

// In-memory API host backed by a throwaway SQLite file, so tests never touch workitems.db.
internal sealed class TestApi : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"workitems-test-{Guid.NewGuid():N}.db");
    public WebApplicationFactory<Program> Factory { get; }

    public TestApi() =>
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:WorkItems", $"Data Source={_databasePath}"));

    public void Dispose()
    {
        Factory.Dispose();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }
}
