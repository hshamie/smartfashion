using Microsoft.EntityFrameworkCore;
using SmartFashion.Api.Models;

namespace SmartFashion.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserAuthProvider> UserAuthProviders => Set<UserAuthProvider>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<UserAuthProvider>()
            .HasIndex(x => new { x.Provider, x.ProviderUserId })
            .IsUnique();

        modelBuilder.Entity<UserAuthProvider>()
            .HasOne(x => x.User)
            .WithMany(u => u.AuthProviders)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}