using AgriStream.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(r => r.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(r => r.Name).IsUnique();
            e.HasData(
                new Role { Id = 1, Name = "Farmer" },
                new Role { Id = 2, Name = "Agronomist" },
                new Role { Id = 3, Name = "Admin" });
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(u => u.FarmId).HasMaxLength(50).IsRequired();
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
        });

        b.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.Property(d => d.DeviceId).HasMaxLength(50).IsRequired();
            e.HasIndex(d => d.DeviceId).IsUnique();
            e.Property(d => d.FarmId).HasMaxLength(50).IsRequired();
            e.Property(d => d.FieldName).HasMaxLength(100).IsRequired();
            e.Property(d => d.Status).HasMaxLength(30).IsRequired();
            e.Property(d => d.LastCommand).HasMaxLength(200);
        });
    }
}
