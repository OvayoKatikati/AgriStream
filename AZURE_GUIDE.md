# AgriStream - Azure deployment, load test and evidence guide

Do these in order. Every **[SHOT n]** is a screenshot your lecturer needs; paste each into the Word document at the matching placeholder.
Use one resource group so everything can be deleted together afterwards.

## 0. Names to decide first

| Item | Example | Rule |
|---|---|---|
| Resource group | `rg-agristream` | any |
| Web app name | `<your student number>` | must be globally unique; gives `https://<student number>.azurewebsites.net` |
| Storage account | `stagristream<digits>` | 3-24 chars, lowercase letters and digits only |
| SQL server / database | `sql-agristream-<digits>` / `AgriStreamDb` | server name globally unique |
| Function app | `func-agristream-<digits>` | globally unique |
| App Insights | `appi-agristream` | any |

Pick one region for everything (e.g. South Africa North if your subscription allows it, otherwise West Europe).

## 1. Resource group
Portal > Resource groups > Create > `rg-agristream`. **[SHOT 1]** resource group overview (take it again at the end, when it lists all resources).

## 2. Storage account (Blob + Table + Queue)
Create a storage account in the resource group: Performance *Standard*, Redundancy **LRS** (sensor data; see theory answer for GRS), leave defaults.
- Security + networking > **Access keys** > copy *Connection string* of key1 (used below).
- The app creates container `drone-images` and tables `SensorReadings`, `ProcessingAudit`, `IrrigationEvents` on first start. The queue `irrigation-commands` is created by the first queued message.

**[SHOT 2]** storage account overview. Later, from *Storage browser*: **[SHOT 3]** `drone-images` container with an uploaded image, **[SHOT 4]** table `SensorReadings`, **[SHOT 5]** table `ProcessingAudit` (written by Blob function), **[SHOT 6]** table `IrrigationEvents`, **[SHOT 7]** queue `irrigation-commands`.

## 3. Azure SQL Database
Create SQL database `AgriStreamDb` with a new server, authentication *SQL authentication* (note the admin login/password), pick the cheapest tier (e.g. serverless or Basic).
- Server > **Networking**: tick *Allow Azure services and resources to access this server*, and add your client IP (so you can use Query editor / run locally).
- Database > **Connection strings** > ADO.NET (SQL authentication). Replace `{your_password}`.

**[SHOT 8]** SQL database overview. After the first start of the web app: **[SHOT 9]** Query editor result of
```sql
SELECT u.Id, u.Username, r.Name AS Role, u.FarmId FROM dbo.Users u JOIN dbo.Roles r ON r.Id = u.RoleId;
```
and **[SHOT 10]** `SELECT * FROM dbo.Devices;` (after triggering irrigation, one device shows `Irrigating`).
(Optional: you can run `sql/schema.sql` yourself first on the empty database.)

## 4. Application Insights
Create **Application Insights** (workspace-based, same region). Copy its *Connection string* from the Overview page. **[SHOT 11]** overview.

## 5. App Service plan + Web App
Create Web App:
- Name: `<student number>`; Publish *Code*; Runtime stack **.NET 8 (LTS)**; OS **Linux**.
- Pricing plan: **Standard S1** (rule-based auto-scale is NOT available on Free/Basic).
- Monitoring tab: enable Application Insights and select the one from step 4.

Web app > **Settings > Environment variables**:

| Where | Name | Value |
|---|---|---|
| Connection strings | `DefaultConnection` (type **SQLAzure**) | your SQL connection string |
| App settings | `AzureStorage__ConnectionString` | storage connection string |
| App settings | `Functions__BaseUrl` | `https://func-agristream-<digits>.azurewebsites.net` (fill in after step 6) |
| App settings | `Functions__Key` | function app default host key (after step 6) |

Apply and save. **[SHOT 12]** web app overview showing the URL and plan; **[SHOT 13]** environment variables page (hide the values).

## 6. Function App
Create Function App: Runtime stack **.NET**, version **8 (LTS) isolated**, Linux, same resource group/region.
- Hosting: **Consumption** if offered. If only Flex Consumption is offered, use *App Service plan* hosting and select the plan from step 5 instead (Flex Consumption needs an Event Grid source for blob triggers).
- Storage: **select the same storage account from step 2** so the blob and queue triggers watch your `drone-images` container and `irrigation-commands` queue (the wizard sets `AzureWebJobsStorage` to it).
- Monitoring: enable Application Insights with the same instance.

Function app > Environment variables > add `SqlConnectionString` = the SQL connection string. Confirm `AzureWebJobsStorage` points to the storage account from step 2 and `FUNCTIONS_WORKER_RUNTIME` = `dotnet-isolated`.

Function app > **App keys** > copy the **default** host key, and put it into the web app's `Functions__Key` (step 5). **[SHOT 14]** function app overview.

## 7. Deploy the code
**Functions** (from the `AgriStream.Functions` folder, Azure Functions Core Tools + `az login`):
```
func azure functionapp publish func-agristream-<digits>
```
(or Visual Studio: right-click project > Publish > Azure Function App).
Function app > Functions: you must see exactly **four** functions: `ProcessDroneImage`, `GetLatestTelemetry`, `QueueIrrigationCommand`, `ProcessIrrigationCommand`. **[SHOT 15]**

**Web app** (from the `AgriStream.Web` folder):
```
dotnet publish -c Release -o publish
cd publish && zip -r ../publish.zip . && cd ..
az webapp deploy --resource-group rg-agristream --name <student number> --src-path publish.zip --type zip
```
(or Visual Studio: right-click project > Publish > Azure App Service (Linux)).
Open `https://<student number>.azurewebsites.net`. **[SHOT 16]** the live site in the browser (URL bar visible).

## 8. Functional test (evidence that everything runs online)
1. Sign in `admin` / `Admin@123`. **[SHOT 17]** Admin page (users and roles from Azure SQL).
2. Admin > *Write sample telemetry*. Open **Field Dashboard**: telemetry table is rendered through the HTTP GET function. **[SHOT 18]**
3. Click **Trigger irrigation** on a device: success banner (HTTP POST function > queue). Refresh: the device shows `Irrigating` (queue function > SQL). **[SHOT 19]**
4. Drone Images > upload a JPG/PNG: **[SHOT 20]**. Within ~1 minute `ProcessingAudit` has a new row (blob trigger polling can take a minute).
5. Function app > each function > **Monitor** (invocations): **[SHOT 21]** one screenshot per function showing successful invocations.
6. Register a new user and sign in as `farmer1` to show Farmer role: Admin menu hidden / `/Admin` shows *Access denied*. **[SHOT 22]**

## 9. Auto-scaling (task g)
App Service plan > **Scale out (App Service plan)** > **Custom autoscale** > *Scale based on a metric*:
- Instance limits: Minimum **1**, Maximum **3**, Default **1**.
- Rule 1 (scale out): Metric **CPU Percentage** > **60** for **5 minutes** (average) > Increase count by **1**, cool down **5 min**.
- Rule 2 (scale in): CPU Percentage < **30** for 10 minutes > Decrease count by 1, cool down 10 min.
- Optional memory rule: **Memory Percentage** > 75 > increase by 1.

Save. **[SHOT 23]** the autoscale settings with both rules.

## 10. Load test (task e)
Run from your own computer, at least 10 minutes so the 5-minute average crosses the threshold:
```
python loadtest/loadtest.py https://<student number>.azurewebsites.net --users 150 --duration 600
```
Alternatives: Apache JMeter (Thread Group: 150 threads, 60 s ramp-up, loop forever, scheduler 10 min; HTTP samplers for `/`, `/Account/Login`, `/Load/Cpu?ms=200`) or **Azure Load Testing** (Create > Quick test with the URL).
The script prints total requests, failures, throughput, average/median/95th percentile latency and writes `results.csv`. **[SHOT 24]** the script/JMeter/Azure Load Testing summary.
If CPU stays below 60%, increase `--users` (e.g. 300) or lower the rule threshold to 50%.

## 11. Monitoring (task f) - during/after the test
Capture while load is running:
- App Service plan > **Monitoring > Metrics**: CPU Percentage and Memory Percentage, time range *last hour*. **[SHOT 25]**
- Web app > Metrics: *Requests*, *Average Response Time*, *Http 5xx*. **[SHOT 26]**
- App Insights > **Live Metrics** (requests/s, failures, server count). **[SHOT 27]**
- App Insights > **Performance** and **Failures**. **[SHOT 28]**
- App Service plan > Scale out > **Run history** + **Activity log** entries "Autoscale scale up". **[SHOT 29]**
- App Service plan > Metrics with *Instance count* (or the autoscale *Observed Capacity* metric) rising from 1 to 2/3. **[SHOT 30]**
- Function app > Monitor / App Insights > Live metrics for the functions. **[SHOT 31]**

Then write 1-2 paragraphs in the document: peak CPU, instance count reached, error rate, response-time trend, and whether the scalability/reliability expectations were met (use your real numbers).

## 12. Final evidence
**[SHOT 32]** resource group overview listing all resources (Web App, App Service plan, SQL server + database, Storage account, Function App, Application Insights).

## 13. Clean up (after marking)
Delete the resource group (S1 plan and SQL cost credits while they run). Remove `LoadController.cs` before showing the code publicly.

## Troubleshooting
| Symptom | Fix |
|---|---|
| Site shows HTTP 500.30 / blank | Web app > Log stream; usually `DefaultConnection` missing or wrong type |
| Login page works but cannot sign in | SQL firewall: tick *Allow Azure services*; check connection string password |
| Dashboard says "Telemetry service unavailable" | `Functions__BaseUrl` / `Functions__Key` wrong or functions not published |
| Dashboard empty | Admin > *Write sample telemetry* |
| Dashboard shows a 401/404 error from the function | Check `Functions__Key` is the function app's *default host key* and that the functions were published |
| Irrigation says queued but device never changes | Function app `SqlConnectionString` missing, or `AzureWebJobsStorage` points to a different storage account than the web app |
| Blob function never fires | Same storage account as the web app? Container name `drone-images`? (polling can take up to a minute) |
| Autoscale option greyed out | Plan is Free/Basic; scale up to Standard S1 |
