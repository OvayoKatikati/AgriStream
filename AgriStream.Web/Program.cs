using Azure.Data.Tables;
using Azure.Storage.Blobs;
using AgriStream.Web.Data;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Application Insights (reads APPLICATIONINSIGHTS_CONNECTION_STRING from App Service settings)
builder.Services.AddApplicationInsightsTelemetry();

// ---- Azure SQL (user + role management, device registry) ----
// On App Service add a connection string named DefaultConnection (type: SQLAzure).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()));

// ---- Authentication: cookie auth is stateless (no server-side session) so it scales out ----
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// ---- Azure Storage (Blob + Table) ----
// App setting name on App Service: AzureStorage__ConnectionString
var storageConnection = builder.Configuration["AzureStorage:ConnectionString"] ?? "UseDevelopmentStorage=true";
builder.Services.AddSingleton(new BlobServiceClient(storageConnection));
builder.Services.AddSingleton(new TableServiceClient(storageConnection));
builder.Services.AddSingleton<MediaStorageService>();
builder.Services.AddSingleton<TelemetryService>();

// ---- Typed HttpClient that calls the Azure Functions (HTTP GET / POST) ----
builder.Services.AddHttpClient<FunctionsClient>();

var app = builder.Build();

// Create tables / container / seed data on start-up. Failures are logged but do not stop the site.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        DbSeeder.Seed(db);
        logger.LogInformation("Azure SQL schema ready and seeded.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Azure SQL initialisation failed. Check the DefaultConnection string.");
    }

    try
    {
        await scope.ServiceProvider.GetRequiredService<TelemetryService>().EnsureTablesAsync();
        await scope.ServiceProvider.GetRequiredService<MediaStorageService>().EnsureContainerAsync();
        logger.LogInformation("Azure Storage tables and container ready.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Azure Storage initialisation failed. Check AzureStorage:ConnectionString.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("healthy"));
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
