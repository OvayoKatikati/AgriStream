using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = "Farmer,Agronomist,Admin")]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly FunctionsClient _functions;

    public DashboardController(AppDbContext db, FunctionsClient functions)
    {
        _db = db;
        _functions = functions;
    }

    private string FarmId => User.FindFirst("FarmId")?.Value ?? "farm-001";

    // Calls the HTTP GET Azure Function for near-instant telemetry rendering.
    public async Task<IActionResult> Index()
    {
        var (items, error) = await _functions.GetLatestTelemetryAsync(FarmId);
        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.FarmId == FarmId)
            .OrderBy(d => d.DeviceId)
            .ToListAsync();

        return View(new DashboardViewModel
        {
            FarmId = FarmId,
            Telemetry = items,
            Devices = devices,
            TelemetryError = error
        });
    }

    // Calls the HTTP POST Azure Function, which places an Irrigation Command on the storage queue.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerIrrigation(string deviceId)
    {
        var device = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.FarmId == FarmId);

        if (device is null)
        {
            TempData["Error"] = "Device not found for your farm.";
            return RedirectToAction(nameof(Index));
        }

        var (success, message) = await _functions.TriggerIrrigationAsync(
            device.DeviceId, device.FarmId, "Manual trigger from dashboard", User.Identity?.Name ?? "unknown");

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
