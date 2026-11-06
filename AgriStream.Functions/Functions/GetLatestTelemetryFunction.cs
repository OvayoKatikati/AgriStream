using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 2 - HTTP TRIGGER (GET).
/// Called by the MVC dashboard. The Table INPUT BINDING reads the newest 20 rows of the farm's partition
/// (RowKeys are inverted ticks, so the newest rows come first) - no table client code is needed.
/// </summary>
public class GetLatestTelemetryFunction
{
    [Function("GetLatestTelemetry")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/{farmId}")] HttpRequest req,
        string farmId,
        [TableInput("SensorReadings", "{farmId}", Take = 20, Connection = "AzureWebJobsStorage")]
        IEnumerable<SensorReading> readings)
    {
        var result = readings
            .OrderByDescending(r => r.ReadingTimeUtc)
            .Select(r => new
            {
                r.DeviceId,
                r.SoilMoisture,
                r.SoilTemperature,
                r.BatteryPercent,
                r.ReadingTimeUtc
            })
            .ToList();

        return new OkObjectResult(result);
    }
}
