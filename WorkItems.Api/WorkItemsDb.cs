using Microsoft.EntityFrameworkCore;

namespace WorkItems.Api;

public sealed class WorkItemsDb(DbContextOptions<WorkItemsDb> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<WorkItem>();
        item.Property(value => value.Status).HasConversion<string>();
        // SQLite cannot ORDER BY DateTimeOffset. This converter writes the same TEXT format the provider
        // already used (no data migration), and that text sorts chronologically while every value is UTC.
        item.Property(value => value.DueDate).HasConversion<string>();
        item.Property(value => value.CreatedAt).HasConversion<string>();

        // NOCASE makes the unique index, IN (...) lookups and ?tag= filtering case-insensitive in SQL.
        var tag = modelBuilder.Entity<Tag>();
        tag.Property(value => value.Name).UseCollation("NOCASE");
        tag.HasIndex(value => value.Name).IsUnique();
    }
}
