using AgriStream.Web.Models;

namespace AgriStream.Web.Data;

public static class DbSeeder
{
    /// <summary>Seeds demo users and devices. CHANGE THESE PASSWORDS for anything beyond a class demo.</summary>
    public static void Seed(AppDbContext db)
    {
        if (!db.Users.Any())
        {
            var roles = db.Roles.ToDictionary(r => r.Name, r => r.Id);

            db.Users.AddRange(
                new AppUser { Username = "admin",   PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),  RoleId = roles["Admin"],      FarmId = "farm-001" },
                new AppUser { Username = "farmer1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Farmer@123"), RoleId = roles["Farmer"],     FarmId = "farm-001" },
                new AppUser { Username = "agro1",   PasswordHash = BCrypt.Net.BCrypt.HashPassword("Agro@123"),   RoleId = roles["Agronomist"], FarmId = "farm-001" });
        }

        if (!db.Devices.Any())
        {
            db.Devices.AddRange(
                new Device { DeviceId = "sensor-001", FarmId = "farm-001", FieldName = "North Field" },
                new Device { DeviceId = "sensor-002", FarmId = "farm-001", FieldName = "South Field" },
                new Device { DeviceId = "sensor-003", FarmId = "farm-001", FieldName = "River Paddock" },
                new Device { DeviceId = "sensor-004", FarmId = "farm-001", FieldName = "Orchard Block" });
        }

        db.SaveChanges();
    }
}
