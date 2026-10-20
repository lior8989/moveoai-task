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

### Visual Studio 2026
Open `WeatherRisk.slnx`, set **WeatherRisk.Api** as the startup project, choose the **http** profile, press F5.
The browser opens Swagger.

## Configuration
`src/WeatherRisk.Api/appsettings.json`:
| Section | Setting | Default | Meaning |
|---|---|---|---|
| `Monitoring` | `WindowSeconds` | 60 | Window for "requests in window" metric |
| | `LatencySampleSize` | 512 | Recent requests per endpoint used for avg/p95/max latency |
| `RateLimiting:Global` | `PermitLimit` / `WindowSeconds` / `SegmentsPerWindow` | 120 / 60 / 6 | Per-client sliding window for `/api/*` (except `/api/system/*`) |

## Testing
```powershell
dotnet test
```
Runs unit tests and endpoint tests (the full HTTP pipeline in memory — no network access needed).
