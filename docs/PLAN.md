# Task Plan

Each task is developed on its own branch, reviewed by the owner, merged to `main` via PR, then the next task starts.

**Status legend:** ⬜ Not started · 🟡 In progress / in review · ✅ Merged

| # | Status | Branch | Scope | Tests |
|---|---|---|---|---|
| 1 | ✅ | `chore/claude-skills-and-docs` | Claude skills (documentation, swagger, SOLID & patterns, performance, testing, observability & resilience), `CLAUDE.md`, skeletons of `docs/ARCHITECTURE.md`, `docs/PLAN.md`, `README.md` | — |
| 2 | ✅ | `chore/solution-skeleton` | Solution + projects (Api, Core, Data) and Api test project — Eval project and Core/Data test projects are created with their first code (tasks 3, 4, 9); `Directory.Build.props`; Swagger; health endpoint; **request metrics middleware + `/api/system/metrics`; rate limiting** | Smoke + endpoint tests (health, metrics, 429) |
| 3 | 🟡 | `feature/domain-scoring` | Core domain: Hub, weather statistics, per-hazard scoring strategies (winter, hurricane, flood, severe storm, heat) blending observed weather + FEMA NRI, composite 0–100 with attribution/drivers/confidence, ranking & comparison. Pure logic | Unit (golden values) |
| 4 | ⬜ | `feature/persistence` | EF Core + SQLite, repositories, seed of 15 hubs, score history, API cache; schema endpoint + ER diagram | Repository tests (SQLite in-memory) |
| 5 | ⬜ | `feature/weather-data-sources` | Open-Meteo + FEMA NRI clients, cache decorator, metric computation; **HTTP resilience (retry/timeout/circuit breaker), live → cache fallback, dependency metrics** | Unit (stubbed HTTP), failure paths |
| 6 | ⬜ | `feature/hub-risk-api` | REST endpoints: hubs, scores, ranking, comparison, weather stats; full Swagger docs | Endpoint tests |
| 7 | ⬜ | `feature/llm-agent` | OpenAI-compatible LLM adapter (Groq / Gemini / Ollama via config), tool-calling into scoring engine, JSON-schema-enforced output + validation, conversation memory, `/api/chat`; **provider fallback chain** | Unit + endpoint (fake LLM) |
| 8 | ⬜ | `feature/chat-ui` | Static HTML/JS chat page served by the API; voice input (Web Speech API) | Endpoint (static files) |
| 9 | ⬜ | `feature/evaluation` | Eval set (10–15 questions with expected checks) + runner console app | Eval run |
| 10 | ⬜ | `feature/risk-alerts` *(bonus)* | Scheduled score recompute (`BackgroundService`), change detection (Observer), log + webhook notifiers, `/api/alerts` | Unit + endpoint |
| 11 | ⬜ | `docs/final-and-session` | Finalize architecture doc, README, assumptions/uncertainty/scope; export AI session transcript to `docs/session/` | — |
| — | ⬜ | *(optional)* | Deploy to a public URL | — |

**Priority (24h budget):** 1–9 and 11 required; 10 and optional items are bonus.

## Hubs (15)
| Region | Hubs |
|---|---|
| Midwest | Chicago, Minneapolis, Detroit, Indianapolis, Kansas City |
| Gulf / South | Miami, Houston, Dallas, New Orleans, Atlanta |
| Mountain / West | Denver, Phoenix, Seattle, Los Angeles |
| Northeast | New York |
