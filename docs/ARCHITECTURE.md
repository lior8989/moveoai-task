# Weather Risk Intelligence Agent — Design & Architecture

> Living document: updated in every task branch. Sections marked *TBD* are filled in by the task noted.

## 1. Problem & scope
A logistics company runs regional distribution hubs across the US. Severe weather shuts hubs down and
costs money; each year a handful of hubs get resilience investment. This system is an AI agent that helps
analysts **rank, compare, and explain hub exposure to weather disruption**, conversationally.

**In scope:** 15 US hubs, four hazard categories (winter, hurricane/wind, flood, heat), historical weather
statistics, a deterministic risk score, an LLM agent that uses that score as tools, chat UI with voice,
evaluation set, monitoring, and (bonus) risk-change alerts.

**Out of scope:** real company hub data, financial loss modelling, forecasting, authentication / multi-tenant.

## 2. System architecture
```
 Browser (static HTML/JS chat + voice)
        │  HTTP/JSON
        ▼
 ┌──────────────────────── WeatherRisk.Api (ASP.NET Core, single process) ────────────────────────┐
 │  Middleware: metrics · rate limiting · ProblemDetails          Swagger UI (/swagger)            │
 │  Endpoints: /api/chat · /api/hubs · /api/scores · /api/system/* · /api/alerts                   │
 │                                                                                                 │
 │  Agent ── LLM provider chain (Groq → Gemini) ── JSON-schema validation                          │
 │    │ tool calls                                                                                 │
 │    ▼                                                                                            │
 │  Core: scoring engine (per-hazard strategies → weighted composite), ranking, comparison         │
 │    │                                                                                            │
 │  Data: repositories (EF Core) ── SQLite file   ·   API clients (resilient HttpClient) + cache   │
 │  Background: score recompute scheduler → change detection → alert notifiers (bonus)             │
 └─────────────────────────────────────────────────────────────────────────────────────────────────┘
        │ HTTPS                                   │ HTTPS
        ▼                                         ▼
 Open-Meteo Historical API, FEMA NRI       LLM APIs (Groq / Gemini, OpenAI-compatible)
```
*Component details: TBD (tasks 2–10).*

## 3. Repository structure
```
WeatherRisk.slnx                 Solution (new XML format, opens in Visual Studio 2026)
Directory.Build.props            Shared settings: net10.0, nullable, XML docs (missing docs = build error)
Directory.Packages.props         Central NuGet versions
global.json                      SDK pin + Microsoft Testing Platform runner
src/
  WeatherRisk.Api/               Composition root (ASP.NET Core Minimal API)
    Program.cs                   DI registration + middleware pipeline
    Endpoints/                   Endpoint groups (one static class per resource)
    Contracts/                   Request/response records (never EF entities)
    Observability/               Request metrics middleware + collector, health checks
    RateLimiting/                Rate-limiter configuration
  WeatherRisk.Core/              Domain + scoring logic (no infrastructure dependencies)
    Domain/                      Hub, Region, HazardType
    Weather/                     DailyWeather, WeatherSummary, thresholds, statistics calculator
    Scoring/                     Engine, settings, results, IHazardScorer
      Hazards/                   One scorer per hazard + shared HazardMath
    Ranking/                     HubRanker (rank + compare)
  WeatherRisk.Data/              EF Core/SQLite persistence + external API clients
tests/
  WeatherRisk.Api.Tests/         Endpoint tests (WebApplicationFactory) + Api unit tests
  WeatherRisk.Core.Tests/        Golden-value unit tests for statistics, scorers, engine, ranking
  (WeatherRisk.Data.Tests is added with its first code in task 4)
docs/                            Architecture, plan, session transcript
.claude/skills/                  Engineering standards the AI agent must follow
```
**Dependency rule:** `Api → Core ← Data` (Api also references Data for DI wiring). Core references nothing.

## 4. Data storage
**Choice: SQLite (single file, EF Core).**
- Zero install, runs on a laptop, file can ship with the demo; real SQL with indexes and transactions.
- Data volume is small (15 hubs × a few years of daily aggregates, cached API payloads, score history).
- Swap path: EF Core makes moving to PostgreSQL a provider/connection-string change.

*Schema & ER diagram: TBD (task 4).*

## 5. Data sources
| Source | Used for | Auth |
|---|---|---|
| Open-Meteo Historical Weather API | Daily snowfall, precipitation, wind, temperature per hub | None (free) |
| FEMA National Risk Index | County-level hazard risk (hurricane, riverine/coastal flood, winter weather, heat…) | None (free) |
| Groq (primary) / Gemini (fallback) | LLM for tool routing and explanations | Free-tier API key (user-secrets) |

*Details: TBD (task 5).*

## 6. Risk scoring model (deterministic)

The score is a **transparent, rule-based risk index** — not a trained ML model. The LLM never computes scores;
it calls tools that run this model. No company disruption history was available to train or calibrate on,
so the parameters below are documented expert assumptions, all overridable via configuration
(`ScoringSettings`, `WeatherThresholds`).

### Two signals per hazard
| Signal | Source | Why |
|---|---|---|
| **Observed** | Open-Meteo daily history, last 5 full years | What actually happened recently at the hub's coordinates |
| **Baseline** | FEMA National Risk Index, hub's county (0–100 national percentile) | Decades of hazard history; captures rare events (landfalls, tornadoes) that 5 years can miss |

`hazardScore = w_obs × observed + (1 − w_obs) × baseline` — if one signal is missing, the other is used alone.

### Hazards
| Hazard | Observed: disruption day (threshold) | Cap (days/yr → 100) | NRI baseline (mean of available) | w_obs |
|---|---|---|---|---|
| Winter | 60% snow day (snowfall ≥ 1 cm) + 40% hard freeze (min ≤ −12 °C) | 40 snow / 30 freeze | Winter Weather, Ice Storm, Cold Wave | 0.60 |
| Hurricane | Gust ≥ 90 km/h (tropical-storm force) | 3 | Hurricane | 0.25 |
| Flood | Precipitation ≥ 50 mm | 6 | Riverine Flooding, Coastal Flooding | 0.50 |
| Severe storm | Gust ≥ 75 km/h | 12 | Tornado, Hail, Strong Wind | 0.40 |
| Heat | Max temperature ≥ 38 °C | 45 | Heat Wave | 0.60 |

Observed score = `min(100, daysPerYear / cap × 100)`. `w_obs` is low where daily weather is a weak proxy
(hurricanes, tornadoes/hail) and high where it measures the hazard directly (snow, heat).

### Composite
```
composite = 0.5 × worstHazard + 0.5 × Σ(wᵢ × hazardᵢ) / Σwᵢ
weights: Winter 0.25 · Hurricane 0.20 · Flood 0.20 · Severe storm 0.20 · Heat 0.15
```
The worst-hazard term prevents dilution: a hub that is extreme on one hazard (Miami/hurricanes) is
high-risk even if every other hazard is benign. **Ratings:** Low < 25 ≤ Moderate < 50 ≤ High < 75 ≤ Very High.

### Explainability & confidence
Each `HubRiskScore` carries, per hazard: score, observed and baseline parts, **evidence factors** (e.g.
`snowDaysPerYear = 18.4`, `nriHurricane = 97.2`), normalized weight and **contribution** (contributions sum
exactly to the composite), plus the **top two drivers** and data **notes**.
Confidence: **High** = full weather coverage (≥ 90% of the window) and NRI present; **Medium** = one signal
missing/incomplete; **Low** = neither.

### Components (WeatherRisk.Core)
| Component | Pattern / role |
|---|---|
| `WeatherStatisticsCalculator` | Single-pass aggregation of daily rows → `WeatherSummary` (also answers "% of snow days in Denver last year") |
| `IHazardScorer` + 5 implementations | **Strategy** — one per hazard; adding a hazard = adding a class (Open/Closed) |
| `HazardMath` | Shared normalize/blend/single-metric recipe — keeps the four single-metric scorers ~10 lines each |
| `RiskScoringEngine` | Composite, attribution, drivers, confidence; validates settings and scorer set at construction (fail fast) |
| `HubRanker` | Pure ranking (by composite or hazard, region filter, top N, deterministic tie-breaks) and side-by-side comparison |
| `ScoringSettings`, `WeatherThresholds` | Plain settings objects (Core has no dependency on `Microsoft.Extensions.Options`; the Api binds them) |

### Future calibration
With the company's historical closure/delay records, weights and caps could be fitted (e.g. regression in
ML.NET) so the index predicts real disruption days or cost — the model structure stays the same.

## 7. Agent design & structured output
*TBD (task 7).*

## 8. Monitoring, rate limiting & resilience

### Request pipeline (order matters)
```
ExceptionHandler → StatusCodePages (ProblemDetails) → Swagger → Routing → RequestMetrics → RateLimiter → Endpoint
```
Metrics run after routing (so requests are labelled by route template, e.g. `GET /api/hubs/{id}`, keeping
the number of keys bounded) and before the rate limiter (so 429 rejections are counted).

### Request metrics — `GET /api/system/metrics`
| Field | Meaning |
|---|---|
| `total`, `succeeded`, `clientErrors`, `serverErrors`, `rateLimited` | Outcome counters since startup (429 counted separately from other 4xx — it signals load, not a client bug) |
| `requestsInWindow` | Requests in the last `windowSeconds` (default 60) — load indicator |
| `avgLatencyMs`, `p95LatencyMs`, `maxLatencyMs` | Over the last 512 requests per endpoint — bottleneck indicator |

Provided per endpoint and overall (`ALL`). Unknown `/api` routes are grouped as `(unmatched)`.
- **Implementation:** `RequestMetricsMiddleware` → `RequestMetricsCollector` (singleton) → per-endpoint
  `EndpointStats` = `SlidingWindowCounter` (60 one-second buckets) + `LatencyWindow` (ring buffer).
- **Cost:** fixed memory per endpoint, one short uncontended lock per request, no allocations on record;
  sorting for p95 happens only when metrics are read.
- In-memory by design (resets on restart). ASP.NET Core's built-in `http.server.*` meters also work with
  `dotnet-counters` for deeper inspection.

### Health — `GET /api/system/health`
ASP.NET Core health checks; 200 for Healthy/Degraded, 503 for Unhealthy. Dependency checks are added by
the features that own them (task 5: data sources, task 7: LLM).

### Rate limiting
Built-in ASP.NET Core rate limiter, **sliding window per client IP** (default 120 requests / 60 s,
6 segments) on all `/api` routes **except `/api/system/*`** — health and metrics must stay reachable under
load, which is exactly when they're needed. Rejections: `429` + `Retry-After` + ProblemDetails. Settings in
`appsettings.json` → `RateLimiting`, validated at startup. *(Task 7 adds a stricter policy for `/api/chat`.)*

### Resilience & fallbacks
*TBD (task 5: HTTP retry/timeout/circuit breaker + cache fallback; task 7: LLM provider fallback).*

## 9. Evaluation
*TBD (task 9).*

## 10. Design decisions
| Decision | Rationale |
|---|---|
| C# / .NET 10 instead of Python | Toolchain already installed, owner's strongest language; architecture is language-agnostic. |
| Single ASP.NET Core process | Simplest to run locally and demo; no inter-service networking. |
| Free hosted LLM (Groq) over local model | Laptop has no dedicated GPU; local 7–8B models would be ~30 s per agent turn. |
| One OpenAI-compatible LLM adapter | Groq, Gemini and Ollama all expose it → provider switch is configuration only. |
| Static HTML/JS chat UI | Lightweight, served by the API, browser Web Speech API gives voice for free. |
| SQLite | See §4. |
| Minimal APIs grouped in static endpoint classes | Less ceremony than controllers; each group is one file; handlers are plain methods (unit-testable, XML docs flow into OpenAPI). |
| Built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) + Swagger UI | .NET 10's native document generator; Swashbuckle only for the UI. Swagger enabled in all environments so reviewers can test live. |
| In-process metrics instead of Prometheus/Grafana | Zero infrastructure; enough to spot load spikes, failures and slow endpoints. |
| `TimeProvider` injected | Time-dependent code (metric windows, later caching/alerts) is deterministic in tests. |
| Rule-based risk index, not ML | Required deterministic logic; no labelled disruption data to train on; fully explainable. |
| Observed + FEMA NRI blend per hazard | Recent local observations + long-term authoritative baseline; each covers the other's blind spot. |
| Composite includes worst-hazard term | Plain averages dilute single extreme hazards (coastal hubs). |
| Five small scorer classes over one generic configurable scorer | Each hazard's logic is readable on its own; shared math lives in `HazardMath`, so duplication stays minimal. |
| `HubRanker` as static pure functions | No state, no dependencies, nothing to substitute — an interface would add ceremony without benefit. |
| `DailyWeather` as `readonly record struct` | ~1,800 rows per hub are aggregated per scoring; value type avoids per-row heap allocations. |
| Rate limit partitioned by client IP | Simple and fair for a demo; assumes no reverse proxy (would need forwarded headers). |

## 11. Assumptions & uncertainty
*Collected throughout tasks, finalized in task 11.*
- Monitoring data is in-memory and per-process; it resets on restart and isn't shared across instances.
- Rate-limit client identity = remote IP address (no auth, no proxy).
- **Scoring parameters are uncalibrated expert assumptions** (thresholds, caps, weights, 50/50 worst-hazard
  blend). They produce a sensible *relative* ranking but are not a prediction of disruption days or cost.
- Daily max gust is a coarse proxy for tornadoes/hail and hurricanes, hence the heavy NRI weighting there.
- Point weather at hub coordinates stands in for the hub's operating area (roads/suppliers may differ).
- FEMA NRI is county-level and reflects national percentiles of *risk* (includes exposure/vulnerability of the
  county, not of our facility).
- 5 years of observations is short for rare hazards; the baseline compensates but uncertainty remains.
