using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 4 - QUEUE TRIGGER.
/// Listens to the "irrigation-commands" queue, processes each command, updates the device's operational
/// status in the master registry table (Devices) in Azure SQL, and records an event in Table Storage.
/// </summary>
public class ProcessIrrigationCommandFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TableServiceClient _tables;
    private readonly ILogger<ProcessIrrigationCommandFunction> _logger;

    public ProcessIrrigationCommandFunction(TableServiceClient tables, ILogger<ProcessIrrigationCommandFunction> logger)
    {
        _tables = tables;
        _logger = logger;
    }

    [Function("ProcessIrrigationCommand")]
    public async Task Run(
        [QueueTrigger("irrigation-commands", Connection = "AzureWebJobsStorage")] string message)
    {
        var command = JsonSerializer.Deserialize<IrrigationCommand>(message, JsonOptions);

        if (command is null || string.IsNullOrWhiteSpace(command.DeviceId))
        {
            // A malformed message can never succeed, so log it and complete instead of retrying forever.
            _logger.LogWarning("Discarding invalid irrigation command: {Message}", message);
            return;
        }

        var sqlConnection = Environment.GetEnvironmentVariable("SqlConnectionString")
            ?? throw new InvalidOperationException("SqlConnectionString is not configured.");

        var lastCommand = $"Irrigation started: {command.Reason} (by {command.RequestedBy})";
        if (lastCommand.Length > 200) lastCommand = lastCommand[..200];

        // 1) Update the master device registry in Azure SQL.
        await using (var conn = new SqlConnection(sqlConnection))
        {
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                @"UPDATE Devices
                  SET Status = @status, LastCommand = @lastCommand, LastUpdatedUtc = SYSUTCDATETIME()
                  WHERE DeviceId = @deviceId AND FarmId = @farmId", conn);
            cmd.Parameters.AddWithValue("@status", "Irrigating");
            cmd.Parameters.AddWithValue("@lastCommand", lastCommand);
            cmd.Parameters.AddWithValue("@deviceId", command.DeviceId);
            cmd.Parameters.AddWithValue("@farmId", command.FarmId);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
            {
                _logger.LogWarning("No device {DeviceId} found in farm {FarmId}; registry not updated.", command.DeviceId, command.FarmId);
            }
        }

        // 2) Record the irrigation event in Table Storage (high-frequency, non-relational data).
        var table = _tables.GetTableClient("IrrigationEvents");
        await table.CreateIfNotExistsAsync();

        var now = DateTime.UtcNow;
        await table.UpsertEntityAsync(new TableEntity(command.FarmId, $"{DateTime.MaxValue.Ticks - now.Ticks:D19}_{command.CommandId}")
        {
            { "DeviceId", command.DeviceId },
            { "CommandId", command.CommandId },
            { "Reason", command.Reason },
            { "RequestedBy", command.RequestedBy },
            { "RequestedUtc", command.RequestedUtc },
            { "ProcessedUtc", now },
            { "Status", "Irrigating" }
        });

        _logger.LogInformation("Irrigation command {CommandId} processed for device {DeviceId}", command.CommandId, command.DeviceId);
    }
}
