using AudioEvolution.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AudioEvolution.Data;

public sealed class ProjectDbContext : DbContext
{
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    public ProjectDbContext(DbContextOptions<ProjectDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired();
            e.Property(p => p.TracksJson).IsRequired();
            e.Property(p => p.MarkersJson).IsRequired();
        });
    }
}
