using System.Net.Http.Json;
using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

/// <summary>Calls the Azure Functions HTTP endpoints (GET telemetry, POST irrigation command).</summary>
public class FunctionsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FunctionsClient> _logger;

    public FunctionsClient(HttpClient http, IConfiguration config, ILogger<FunctionsClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(15);

        var baseUrl = config["Functions:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        var key = config["Functions:Key"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            _http.DefaultRequestHeaders.Add("x-functions-key", key);
        }
    }

    /// <summary>HTTP GET function: latest telemetry for a farm.</summary>
    public async Task<(List<TelemetryDto> Items, string? Error)> GetLatestTelemetryAsync(string farmId, CancellationToken ct = default)
    {
        if (_http.BaseAddress is null)
            return (new List<TelemetryDto>(), "Functions:BaseUrl is not configured.");

        try
        {
            var items = await _http.GetFromJsonAsync<List<TelemetryDto>>(
                $"api/telemetry/{Uri.EscapeDataString(farmId)}", ct);
            return (items ?? new List<TelemetryDto>(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry function call failed for farm {FarmId}", farmId);
            return (new List<TelemetryDto>(), "Telemetry service unavailable: " + ex.Message);
        }
    }

    /// <summary>HTTP POST function: places an "Irrigation Command" message on the storage queue.</summary>
    public async Task<(bool Success, string Message)> TriggerIrrigationAsync(
        string deviceId, string farmId, string reason, string requestedBy, CancellationToken ct = default)
    {
        if (_http.BaseAddress is null)
            return (false, "Functions:BaseUrl is not configured.");

        try
        {
            var response = await _http.PostAsJsonAsync("api/irrigation",
                new { deviceId, farmId, reason, requestedBy }, ct);

            return response.IsSuccessStatusCode
                ? (true, $"Irrigation command queued for {deviceId}.")
                : (false, $"Irrigation request rejected ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Irrigation function call failed for device {DeviceId}", deviceId);
            return (false, "Irrigation service unavailable: " + ex.Message);
        }
    }
}
