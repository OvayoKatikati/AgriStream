using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AgriStream.Functions;

/// <summary>Azure Table Storage entity. PartitionKey = FarmId, RowKey = inverted ticks + DeviceId (newest first).</summary>
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

/// <summary>JSON body the MVC app POSTs to the irrigation function.</summary>
public class IrrigationRequest
{
    public string? DeviceId { get; set; }
    public string? FarmId { get; set; }
    public string? Reason { get; set; }
    public string? RequestedBy { get; set; }
}

/// <summary>Message placed on the storage queue and processed by the queue-triggered function.</summary>
public class IrrigationCommand
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = string.Empty;
    public string FarmId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Multi-output return type: one property is the HTTP response, the other is written to the queue
/// by the Queue OUTPUT BINDING (no queue client code needed).
/// </summary>
public class IrrigationOutput
{
    [QueueOutput("irrigation-commands", Connection = "AzureWebJobsStorage")]
    public string? QueueMessage { get; set; }

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = default!;
}
