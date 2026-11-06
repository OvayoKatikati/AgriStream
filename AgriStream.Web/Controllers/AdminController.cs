using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly TelemetryService _telemetry;

    public AdminController(AppDbContext db, TelemetryService telemetry)
    {
        _db = db;
        _telemetry = telemetry;
    }

    public async Task<IActionResult> Index()
    {
        return View(new AdminViewModel
        {
            Users = await _db.Users.Include(u => u.Role).AsNoTracking().OrderBy(u => u.Username).ToListAsync(),
            Roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(),
            Devices = await _db.Devices.AsNoTracking().OrderBy(d => d.DeviceId).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int userId, int roleId)
    {
        var user = await _db.Users.FindAsync(userId);
        var roleExists = await _db.Roles.AnyAsync(r => r.Id == roleId);

        if (user is null || !roleExists)
        {
            TempData["Error"] = "User or role not found.";
            return RedirectToAction(nameof(Index));
        }

        user.RoleId = roleId;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Updated role for {user.Username}.";
        return RedirectToAction(nameof(Index));
    }

    // Writes sample readings to Azure Table Storage so the dashboard has data to show.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedTelemetry()
    {
        var devices = await _db.Devices.AsNoTracking().ToListAsync();

        foreach (var farm in devices.GroupBy(d => d.FarmId))
        {
            await _telemetry.SeedSampleReadingsAsync(farm.Key, farm.Select(d => d.DeviceId));
        }

        TempData["Success"] = "Sample telemetry written to Azure Table Storage.";
        return RedirectToAction(nameof(Index));
    }
}
