# Weather Risk Intelligence Agent

An AI agent that helps logistics analysts decide which US distribution hubs are most exposed to weather
disruption — ranking, comparing and explaining hub risk using public weather/hazard data (Open-Meteo,
FEMA National Risk Index), a **deterministic, explainable scoring model**, and an LLM agent that answers in
natural language (with follow-ups and voice) through a JSON-schema-enforced, grounding-checked contract.

## Quick start
```powershell
# 1. One-time: store a free Groq key (https://console.groq.com) outside the repo
dotnet user-secrets set "Llm:Providers:Groq:ApiKey" "<your-groq-key>" --project src/WeatherRisk.Api
# 2. Run (creates the SQLite DB and downloads weather/FEMA data in the background on first start, ~1 min)
dotnet run --project src/WeatherRisk.Api --launch-profile http
# 3. Open http://localhost:5050  (chat UI)  ·  http://localhost:5050/swagger  (API)
```

## Deliverables
| | |
|---|---|
| Design & architecture (components, repo structure, data storage, decisions, assumptions, requirements traceability) | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| Evaluation set, runner and latest results (9/9 model checks, 13/13 agent cases) | [src/WeatherRisk.Eval](src/WeatherRisk.Eval) · [docs/evaluation/eval-report.md](docs/evaluation/eval-report.md) |
| Full AI session (plan, discussion, every review round) | [docs/session/session-transcript.md](docs/session/session-transcript.md) (+ raw log) |
| Task plan and status | [docs/PLAN.md](docs/PLAN.md) |
| Engineering standards the AI agent followed | [CLAUDE.md](CLAUDE.md) · [.claude/skills](.claude/skills) |

## Prerequisites
- .NET 10 SDK (`dotnet --list-sdks` should show 10.0.x)
- A free Groq API key (https://console.groq.com) — needed for the chat agent only

## LLM setup (once)
Store the key in .NET user-secrets (kept in your user profile, **outside the repo**; loaded in Development):
```powershell
dotnet user-secrets set "Llm:Providers:Groq:ApiKey" "<your-groq-key>" --project src/WeatherRisk.Api
# Optional extra fallback provider (free key at https://aistudio.google.com):
dotnet user-secrets set "Llm:Providers:Gemini:ApiKey" "<your-gemini-key>" --project src/WeatherRisk.Api
```
Outside Development use environment variables instead, e.g. `Llm__Providers__Groq__ApiKey`.
Without any key the app still runs; only `/api/chat` returns 503.

## Running

### Command line
```powershell
dotnet run --project src/WeatherRisk.Api --launch-profile http
```
Then open:
| URL | What |
|---|---|
| **http://localhost:5050/** | **Chat UI** — ask questions by typing or by voice 🎤 (Chrome/Edge) |
| http://localhost:5050/?q=Why%20is%20the%20Dallas%20hub%27s%20risk%20high%3F | Deep link: asks the question on load |
| http://localhost:5050/swagger | Swagger UI — try every endpoint live |
| http://localhost:5050/api/risk/rankings?region=Midwest&hazard=Winter | Midwest hubs most exposed to winter |
| http://localhost:5050/api/risk/compare?hubs=miami,houston&hazards=Hurricane&hazards=Flood | Miami vs Houston, hurricane and flood |
| http://localhost:5050/api/hubs/denver/weather | Denver last full year: snow days, % of days, … |
| http://localhost:5050/api/hubs/dallas/risk | Dallas risk breakdown: drivers, contributions, evidence |
| http://localhost:5050/api/risk/model | How scores are computed |
| http://localhost:5050/openapi/v1.json | OpenAPI document |
| http://localhost:5050/api/system/health | Health check |
| http://localhost:5050/api/system/metrics | Request metrics (counts, failures, rate-limited, latency) |
| http://localhost:5050/api/system/schema | Database schema (tables, columns, keys, comments) |

### Ask the agent
In Swagger (`POST /api/chat`) or from PowerShell:
```powershell
$r = Invoke-RestMethod http://localhost:5050/api/chat -Method Post -ContentType application/json `
      -Body '{"message":"Why is the Dallas hub''s weather disruption risk high?"}'
$r.answer; $r.findings; $r.toolCalls
# Follow-up in the same conversation:
Invoke-RestMethod http://localhost:5050/api/chat -Method Post -ContentType application/json `
      -Body (@{ message = "How does it compare with Houston?"; conversationId = $r.conversationId } | ConvertTo-Json)
```

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
The browser opens the chat UI.

### Chat UI
![Chat UI](docs/images/chat-answer.png)
- Type a question or tap an example; **🎤** dictates (Chrome/Edge — the browser asks for microphone permission);
  **🔊** reads answers aloud; **New chat** starts a fresh conversation (follow-ups keep context until then).
- Each answer shows the numbers it cites with **✓ verified** against the deterministic tools, key factors,
  caveats, follow-up suggestions, and **How I got this** (tools called, model, timing).

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
| `Scoring` *(optional)* | e.g. `WorstHazardBlend`, `Heat:CompositeWeight`, `Winter:SnowDaysPerYearCap` | see `/api/risk/model` | Override any risk-model parameter; validated at startup |
| `WeatherThresholds` *(optional)* | e.g. `SnowDayCm`, `HeavyRainMm`, `ExtremeHeatC` | 1 / 50 / 38 | Disruption-day thresholds |
| `Llm` | `ProviderOrder`, `Providers:{name}:BaseUrl / Models / ReasoningEffort / MaxTokens` | Groq (gpt-oss-120b → gpt-oss-20b → qwen3.8-27b), Gemini | LLM fallback chain; keys via user-secrets |
| `Agent` | `MaxSteps` / `HistoryTurns` / `TimeoutSeconds` / `MaxMessageLength` | 8 / 6 / 60 / 2000 | Agent loop limits |
| `Conversations` | `MaxConversations` / `MaxTurns` / `IdleMinutes` | 1000 / 10 / 60 | In-memory conversation memory |
| `RateLimiting:Chat` | `PermitLimit` / `WindowSeconds` | 10 / 60 | Per-client limit for `/api/chat` |
| `RateLimiting:Global` | `PermitLimit` / `WindowSeconds` / `SegmentsPerWindow` | 120 / 60 / 6 | Per-client sliding window for `/api/*` (except `/api/system/*`) |

## Evaluation
With the API running (`dotnet run --project src/WeatherRisk.Api --launch-profile http`), in a second terminal:
```powershell
dotnet run --project src/WeatherRisk.Eval                  # 9 model checks + 13 agent cases (~6 min, paced for free LLM quotas)
dotnet run --project src/WeatherRisk.Eval -- --skip-agent  # model checks only, seconds, no API key needed
dotnet run --project src/WeatherRisk.Eval -- --only A1,A4  # selected cases
```
Reports are written to `eval-results/eval-report.md` and `.json`. Cases live in
`src/WeatherRisk.Eval/eval-cases.json`. Latest committed run: [docs/evaluation/eval-report.md](docs/evaluation/eval-report.md)
(**9/9 model checks, 13/13 agent cases, 47/47 checks**). Options: `--base-url`, `--cases`, `--out`, `--delay`.

## Testing
```powershell
dotnet test
```
Runs unit tests, repository tests (in-memory SQLite with the real migrations) and endpoint tests (the full
HTTP pipeline in memory with a temporary database) — no network access needed.

UI logic (Markdown renderer incl. XSS safety, formatting helpers) — requires Node.js 22+, no packages:
```powershell
node --test "tests/ui/*.test.mjs"
```

## Exporting the AI session
```powershell
node tools/export-session.mjs   # → docs/session/session-transcript.md + session-raw.jsonl
```
Reads the latest Claude Code session for this folder from `~/.claude/projects`, masks anything key-shaped.

## Scope notes
Not included (see [ARCHITECTURE §1, §13](docs/ARCHITECTURE.md#13-future-work)): the bonus scheduled
risk-change alerts (designed, not built), deployment to a public URL, authentication, and calibration of the
model weights (no company disruption data was available).
