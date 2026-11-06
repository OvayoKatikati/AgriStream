using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

/// <summary>
/// FUNCTION 3 - HTTP TRIGGER (POST).
/// Called by the MVC app when a sensor detects a problem or a user clicks "Trigger irrigation".
/// The Queue OUTPUT BINDING places the "Irrigation Command" message on the storage queue.
/// </summary>
public class QueueIrrigationCommandFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<QueueIrrigationCommandFunction> _logger;

    public QueueIrrigationCommandFunction(ILogger<QueueIrrigationCommandFunction> logger) => _logger = logger;

    [Function("QueueIrrigationCommand")]
    public async Task<IrrigationOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation")] HttpRequest req)
    {
        IrrigationRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<IrrigationRequest>(req.Body, JsonOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.DeviceId) || string.IsNullOrWhiteSpace(body.FarmId))
        {
            return new IrrigationOutput
            {
                QueueMessage = null, // nothing is written to the queue
                HttpResponse = new BadRequestObjectResult(new { error = "deviceId and farmId are required." })
            };
        }

        var command = new IrrigationCommand
        {
            DeviceId = body.DeviceId!,
            FarmId = body.FarmId!,
            Reason = body.Reason ?? "Unspecified",
            RequestedBy = body.RequestedBy ?? "unknown"
        };

        _logger.LogInformation("Queuing irrigation command {CommandId} for device {DeviceId}", command.CommandId, command.DeviceId);

        return new IrrigationOutput
        {
            QueueMessage = JsonSerializer.Serialize(command, JsonOptions),
            HttpResponse = new ObjectResult(new { status = "queued", commandId = command.CommandId })
            {
                StatusCode = StatusCodes.Status202Accepted
            }
        };
    }
}
