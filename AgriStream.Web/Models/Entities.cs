using Azure;
using Azure.Data.Tables;

namespace AgriStream.Web.Models;

// ---------- Azure SQL (relational) ----------

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;   // BCrypt hash, never plain text
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public string FarmId { get; set; } = "farm-001";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Master device registry. The queue-triggered function updates Status here.</summary>
public class Device
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string FarmId { get; set; } = "farm-001";
    public string FieldName { get; set; } = string.Empty;
    public string Status { get; set; } = "Idle";
    public string? LastCommand { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

// ---------- Azure Table Storage (NoSQL) ----------

/// <summary>PartitionKey = FarmId, RowKey = inverted ticks + DeviceId (newest rows sort first).</summary>
public class SensorReading : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string DeviceId { get; set; } = string.Empty;
    public double SoilMoisture { get; set; }
    public double SoilTemperature { get; set; }
    public int BatteryPercent { get; set; }
    public DateTime ReadingTimeUtc { get; set; }
}

/// <summary>JSON shape returned by the HTTP GET function.</summary>
public class TelemetryDto
{
    public string DeviceId { get; set; } = string.Empty;
    public double SoilMoisture { get; set; }
    public double SoilTemperature { get; set; }
    public int BatteryPercent { get; set; }
    public DateTime ReadingTimeUtc { get; set; }
}

// ---------- View models ----------

public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public string FarmId { get; set; } = string.Empty;
    public List<TelemetryDto> Telemetry { get; set; } = new();
    public List<Device> Devices { get; set; } = new();
    public string? TelemetryError { get; set; }
}

public class MediaItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
    public DateTimeOffset? UploadedUtc { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}

public class AdminViewModel
{
    public List<AppUser> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
    public List<Device> Devices { get; set; } = new();
}
