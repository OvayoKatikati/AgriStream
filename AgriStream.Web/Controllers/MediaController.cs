using System.Text.RegularExpressions;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = "Farmer,Agronomist,Admin")]
public class MediaController : Controller
{
    // 25 MB: stays under the 30 MB default request limit of App Service / IIS.
    private const long MaxBytes = 25 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

    private readonly MediaStorageService _media;

    public MediaController(MediaStorageService media) => _media = media;

    private string FarmId => User.FindFirst("FarmId")?.Value ?? "farm-001";

    public async Task<IActionResult> Index()
    {
        // Admins see every farm; everyone else only sees their own farm's images.
        var prefix = User.IsInRole("Admin") ? null : FarmId + "-";
        return View(await _media.ListAsync(prefix));
    }

    // Uploading the file triggers the Blob-triggered Azure Function.
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Choose an image to upload.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            TempData["Error"] = "Only JPG, PNG and TIFF images are allowed.";
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxBytes)
        {
            TempData["Error"] = "File is larger than 25 MB.";
            return RedirectToAction(nameof(Index));
        }

        var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), "[^a-zA-Z0-9_-]", "_");
        var blobName = $"{FarmId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{safeName}{extension}";

        var metadata = new Dictionary<string, string>
        {
            ["farmid"] = FarmId,
            ["uploadedby"] = User.Identity?.Name ?? "unknown"
        };

        await using var stream = file.OpenReadStream();
        await _media.UploadAsync(stream, blobName, file.ContentType, metadata);

        TempData["Success"] = $"Uploaded {blobName}. The blob-triggered function will log it to Table Storage.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Open(string name)
    {
        // Block path tricks; blob names in this app are always flat.
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains(".."))
            return BadRequest();

        // Non-admins may only open their own farm's images.
        if (!User.IsInRole("Admin") && !name.StartsWith(FarmId + "-", StringComparison.Ordinal))
            return Forbid();

        var result = await _media.OpenReadAsync(name);
        if (result is null) return NotFound();

        return File(result.Value.Stream, result.Value.ContentType);
    }
}
