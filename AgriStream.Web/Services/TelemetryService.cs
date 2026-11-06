using Azure.Data.Tables;
using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

/// <summary>
/// Writes telemetry into Azure Table Storage. In production the sensors would do this; here it is used
/// to generate sample data so the dashboard (HTTP GET function) has something to show.
/// </summary>
public class TelemetryService
{
    public const string SensorTable = "SensorReadings";
    public const string AuditTable = "ProcessingAudit";
    public const string IrrigationTable = "IrrigationEvents";

    private readonly TableServiceClient _tables;

    public TelemetryService(TableServiceClient tables) => _tables = tables;

    public async Task EnsureTablesAsync()
    {
        await _tables.CreateTableIfNotExistsAsync(SensorTable);
        await _tables.CreateTableIfNotExistsAsync(AuditTable);
        await _tables.CreateTableIfNotExistsAsync(IrrigationTable);
    }

    /// <summary>Newest-first RowKey: later timestamps produce smaller keys, so Table queries return the latest rows first.</summary>
    public static string BuildRowKey(DateTime utc, string deviceId) =>
        $"{DateTime.MaxValue.Ticks - utc.Ticks:D19}_{deviceId}";

    public async Task SeedSampleReadingsAsync(string farmId, IEnumerable<string> deviceIds, int readingsPerDevice = 10)
    {
        var table = _tables.GetTableClient(SensorTable);
        await table.CreateIfNotExistsAsync();

        var rng = new Random();
        var now = DateTime.UtcNow;

        foreach (var deviceId in deviceIds)
        {
            for (var i = 0; i < readingsPerDevice; i++)
            {
                var time = now.AddMinutes(-i * 5);
                var reading = new SensorReading
                {
                    PartitionKey = farmId,
                    RowKey = BuildRowKey(time, deviceId),
                    DeviceId = deviceId,
                    SoilMoisture = Math.Round(15 + rng.NextDouble() * 45, 1),
                    SoilTemperature = Math.Round(14 + rng.NextDouble() * 14, 1),
                    BatteryPercent = rng.Next(40, 100),
                    ReadingTimeUtc = time
                };
                await table.UpsertEntityAsync(reading);
            }
        }
    }
}
