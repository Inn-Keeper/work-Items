using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WorkItems.Api.Migrations;

[DbContext(typeof(WorkItemsDb))]
public sealed class WorkItemsDbModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
        modelBuilder.Entity<WorkItem>(item =>
        {
            item.Property(value => value.Id).ValueGeneratedOnAdd().HasColumnType("INTEGER");
            item.Property(value => value.Title).IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            item.Property(value => value.Description).HasColumnType("TEXT");
            item.Property(value => value.Status).HasConversion<string>().HasColumnType("TEXT");
            item.Property(value => value.DueDate).HasConversion<string>().HasColumnType("TEXT");
            item.Property(value => value.CreatedAt).HasConversion<string>().HasColumnType("TEXT");
            item.Property(value => value.Version).IsConcurrencyToken().HasColumnType("INTEGER");
            item.HasKey(value => value.Id);
            item.ToTable("WorkItems");
        });
    }
}
