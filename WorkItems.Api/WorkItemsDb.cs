using Microsoft.EntityFrameworkCore;

namespace WorkItems.Api;

public sealed class WorkItemsDb(DbContextOptions<WorkItemsDb> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkItem>().Property(item => item.Status).HasConversion<string>();
    }
}
