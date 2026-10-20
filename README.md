# Weather Risk Intelligence Agent

An AI agent that helps logistics analysts decide which US distribution hubs are most exposed to weather
disruption — ranking, comparing and explaining hub risk using public weather/hazard data, a deterministic
scoring model, and an LLM for conversation.

- **Design & architecture:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **Task plan:** [docs/PLAN.md](docs/PLAN.md)

## Prerequisites
- .NET 10 SDK (`dotnet --list-sdks` should show 10.0.x)
- A free Groq API key (https://console.groq.com) — needed for the chat agent only (task 7)

## Running

### Command line
```powershell
dotnet run --project src/WeatherRisk.Api --launch-profile http
```
Then open:
| URL | What |
|---|---|
| http://localhost:5050/swagger | Swagger UI — try every endpoint live |
| http://localhost:5050/openapi/v1.json | OpenAPI document |
| http://localhost:5050/api/system/health | Health check |
| http://localhost:5050/api/system/metrics | Request metrics (counts, failures, rate-limited, latency) |
| http://localhost:5050/api/system/schema | Database schema (tables, columns, keys, comments) |

### Database
SQLite, created and migrated automatically on first start at `src/WeatherRisk.Api/App_Data/weatherrisk.db`
(15 hubs seeded). No setup needed. To reset, stop the app and delete the `App_Data` folder.

### External data
On startup the app downloads 5 years of daily weather (Open-Meteo) and FEMA hazard baselines for all hubs in
the background (~1 minute the first time — Open-Meteo's free per-minute quota; then cached for 30 days).
Internet access is needed for that first fetch; afterwards the app works from the cache. Progress and outcome:
`/api/system/health` and `/api/system/metrics` → `dependencies`.

For schema changes (developers):
```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/WeatherRisk.Data --output-dir Persistence/Migrations
```

### Visual Studio 2026
Open `WeatherRisk.slnx`, set **WeatherRisk.Api** as the startup project, choose the **http** profile, press F5.
The browser opens Swagger.

## Configuration
`src/WeatherRisk.Api/appsettings.json`:
| Section | Setting | Default | Meaning |
|---|---|---|---|
| `ConnectionStrings` | `WeatherRisk` | `Data Source=App_Data/weatherrisk.db` | SQLite file (relative paths resolve under the Api project) |
| `Monitoring` | `WindowSeconds` | 60 | Window for "requests in window" metric |
| | `LatencySampleSize` | 512 | Recent requests per endpoint used for avg/p95/max latency |
| `HubData` | `LookbackYears` | 5 | Observed-weather window in full calendar years |
| | `WeatherCacheDays` / `BaselineCacheDays` | 30 / 30 | Cache time-to-live before refetching |
| | `MaxConcurrentFetches` | 4 | Parallel Open-Meteo requests during refresh |
| | `WarmUpOnStartup` | true | Fill the cache for all hubs in the background at startup |
| `RateLimiting:Global` | `PermitLimit` / `WindowSeconds` / `SegmentsPerWindow` | 120 / 60 / 6 | Per-client sliding window for `/api/*` (except `/api/system/*`) |

## Testing
```powershell
dotnet test
```
Runs unit tests, repository tests (in-memory SQLite with the real migrations) and endpoint tests (the full
HTTP pipeline in memory with a temporary database) — no network access needed.
