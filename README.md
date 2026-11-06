# AgriStream IoT - Real-Time Processing System

ASP.NET Core 8 MVC web app + four Azure Functions (.NET 8 isolated worker) on Azure.

```
AgriStream.sln
AgriStream.Web/        MVC app (App Service): login + roles, dashboard, drone image upload, admin
AgriStream.Functions/  Exactly four functions: Blob trigger, HTTP GET, HTTP POST, Queue trigger
sql/schema.sql         Optional manual Azure SQL schema (the app also creates it automatically)
loadtest/loadtest.py   Concurrent-user load generator (or use JMeter / Azure Load Testing)
AZURE_GUIDE.md         Step-by-step Azure portal + deployment + load test + monitoring + screenshots
```

## Architecture

| Requirement | Implementation |
|---|---|
| Web hosting | `AgriStream.Web` on Azure App Service (Linux, Standard S1 so auto-scale is available) |
| Relational DB | Azure SQL: `Roles`, `Users` (BCrypt password hash + RoleId), `Devices` (master registry) |
| Blob storage | Drone images uploaded via **Drone Images** page into container `drone-images` |
| Table storage | `SensorReadings`, `ProcessingAudit`, `IrrigationEvents` |
| Queue storage | `irrigation-commands` |
| Function 1 - Blob trigger | `ProcessDroneImage` writes an audit row to `ProcessingAudit` |
| Function 2 - HTTP GET | `GetLatestTelemetry` (Table **input binding**) feeds the dashboard |
| Function 3 - HTTP POST | `QueueIrrigationCommand` (Queue **output binding**) queues an irrigation command |
| Function 4 - Queue trigger | `ProcessIrrigationCommand` updates `Devices.Status` in Azure SQL + logs to `IrrigationEvents` |
| Stateless design | Cookie auth (no server session), uploads streamed to Blob Storage, no local disk use |

## Run locally

1. Install .NET 8 SDK, Azure Functions Core Tools v4, and Azurite (or use a real storage account).
2. Terminal 1: `azurite`
3. Terminal 2: `cd AgriStream.Functions && func start`
4. Terminal 3: `cd AgriStream.Web && dotnet run`

Seeded logins (CHANGE before any real use): `admin / Admin@123`, `farmer1 / Farmer@123`, `agro1 / Agro@123`.
Sign in as `admin`, open **Admin** and press "Write sample telemetry", then open the **Field Dashboard**.

## Configuration (App Service settings)

| Name | Type | Value |
|---|---|---|
| `DefaultConnection` | Connection string (SQLAzure) | Azure SQL connection string |
| `AzureStorage__ConnectionString` | App setting | Storage account connection string |
| `Functions__BaseUrl` | App setting | `https://<function-app>.azurewebsites.net` |
| `Functions__Key` | App setting | Function app *default host key* |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App setting | From Application Insights |

Function app settings: `AzureWebJobsStorage` (same storage account), `SqlConnectionString`, `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`.

## Notes

- `Controllers/LoadController.cs` is a demo-only CPU-burning endpoint so the load test can trigger auto-scale. Delete it after marking.
- Never commit real keys or connection strings. `local.settings.json` contains local placeholders only.
- Targets .NET 8. If your machine only has a newer SDK, change `TargetFramework` in both projects and update the package versions to match.
