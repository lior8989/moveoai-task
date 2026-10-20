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
*TBD (task 2).*

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
*TBD (task 3).*

## 7. Agent design & structured output
*TBD (task 7).*

## 8. Monitoring, rate limiting & resilience
*TBD (tasks 2, 5, 7).*

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

## 11. Assumptions & uncertainty
*TBD — collected throughout tasks, finalized in task 11.*
