using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == model.Username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role!.Name),
            new("FarmId", user.FarmId)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    /// <summary>Self-registration always creates a Farmer. An Admin can promote users on the Admin page.</summary>
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length > 100)
            ModelState.AddModelError(nameof(model.Username), "Enter a username (max 100 characters).");

        if (model.Password is null || model.Password.Length < 8)
            ModelState.AddModelError(nameof(model.Password), "Password must be at least 8 characters.");

        if (model.Password != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");

        if (ModelState.IsValid && await _db.Users.AnyAsync(u => u.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");

        if (!ModelState.IsValid)
            return View(model);

        var farmerRole = await _db.Roles.FirstAsync(r => r.Name == "Farmer");
        _db.Users.Add(new AppUser
        {
            Username = model.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            RoleId = farmerRole.Id,
            FarmId = "farm-001"
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Account created. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
