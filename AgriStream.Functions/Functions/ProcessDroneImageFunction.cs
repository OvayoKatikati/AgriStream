using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 1 - BLOB TRIGGER.
/// Fires when a drone image lands in the "drone-images" container, logs the upload details and
/// writes a processing-status audit entry directly into Azure Table Storage.
/// </summary>
public class ProcessDroneImageFunction
{
    private readonly TableServiceClient _tables;
    private readonly ILogger<ProcessDroneImageFunction> _logger;

    public ProcessDroneImageFunction(TableServiceClient tables, ILogger<ProcessDroneImageFunction> logger)
    {
        _tables = tables;
        _logger = logger;
    }

    [Function("ProcessDroneImage")]
    public async Task Run(
        [BlobTrigger("drone-images/{name}", Connection = "AzureWebJobsStorage")] BlobClient blob,
        string name)
    {
        var props = (await blob.GetPropertiesAsync()).Value;

        props.Metadata.TryGetValue("farmid", out var farmId);
        props.Metadata.TryGetValue("uploadedby", out var uploadedBy);

        _logger.LogInformation(
            "Drone image uploaded: {Name} ({Size} bytes, {ContentType}) by {User} for farm {Farm}",
            name, props.ContentLength, props.ContentType, uploadedBy ?? "unknown", farmId ?? "unknown");

        var table = _tables.GetTableClient("ProcessingAudit");
        await table.CreateIfNotExistsAsync();

        var now = DateTime.UtcNow;
        var audit = new TableEntity("DroneImages", $"{DateTime.MaxValue.Ticks - now.Ticks:D19}_{Guid.NewGuid():N}")
        {
            { "BlobName", name },
            { "FarmId", farmId ?? string.Empty },
            { "UploadedBy", uploadedBy ?? string.Empty },
            { "SizeBytes", props.ContentLength },
            { "ContentType", props.ContentType ?? string.Empty },
            { "Status", "Received" },
            { "ProcessedUtc", now }
        };

        await table.AddEntityAsync(audit);
    }
}
