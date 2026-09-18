using Microsoft.EntityFrameworkCore;

namespace WorkItems.Api;

public sealed class WorkItemsDb(DbContextOptions<WorkItemsDb> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<WorkItem>();
        item.Property(value => value.Status).HasConversion<string>();
        // SQLite cannot ORDER BY DateTimeOffset. This converter writes the same TEXT format the provider
        // already used (no data migration), and that text sorts chronologically while every value is UTC.
        item.Property(value => value.DueDate).HasConversion<string>();
        item.Property(value => value.CreatedAt).HasConversion<string>();
    }
}
