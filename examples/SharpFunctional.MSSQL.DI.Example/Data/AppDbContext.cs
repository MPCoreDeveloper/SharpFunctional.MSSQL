using Microsoft.EntityFrameworkCore;
using SharpFunctional.MsSql.DiExample.Models;

namespace SharpFunctional.MsSql.DiExample.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Category).HasMaxLength(100);
            e.Property(p => p.Price).HasPrecision(18, 2);
        });
    }
}
