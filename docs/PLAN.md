# Task Plan

Each task is developed on its own branch, reviewed by the owner, merged to `main` via PR, then the next task starts.

**Status legend:** ⬜ Not started · 🟡 In progress / in review · ✅ Merged · ⏭️ Skipped

| # | Status | Branch | Scope | Tests |
|---|---|---|---|---|
| 1 | ✅ | `chore/claude-skills-and-docs` | Claude skills (documentation, swagger, SOLID & patterns, performance, testing, observability & resilience), `CLAUDE.md`, skeletons of `docs/ARCHITECTURE.md`, `docs/PLAN.md`, `README.md` | — |
| 2 | ✅ | `chore/solution-skeleton` | Solution + projects (Api, Core, Data) and Api test project — Eval project and Core/Data test projects are created with their first code (tasks 3, 4, 9); `Directory.Build.props`; Swagger; health endpoint; **request metrics middleware + `/api/system/metrics`; rate limiting** | Smoke + endpoint tests (health, metrics, 429) |
| 3 | ✅ | `feature/domain-scoring` | Core domain: Hub, weather statistics, per-hazard scoring strategies (winter, hurricane, flood, severe storm, heat) blending observed weather + FEMA NRI, composite 0–100 with attribution/drivers/confidence, ranking & comparison. Pure logic | Unit (golden values) |
| 4 | ✅ | `feature/persistence` | EF Core + SQLite (migrations, WAL), repository ports in Core + implementations in Data, seed of 15 hubs, weather/baseline cache tables, score history; `/api/system/schema` + ER diagram; DB health check | Repository tests (SQLite in-memory) |
| 5 | ✅ | `feature/weather-data-sources` | Open-Meteo + FEMA NRI clients (NRI frequency percentiles), cache-first provider with bounded parallel refresh and startup warm-up; **HTTP resilience (retry/timeout/circuit breaker), live → stale-cache fallback, dependency metrics + passive health checks**; hurricane model correction from real-data validation | Unit (stubbed HTTP), failure paths, pipeline endpoint tests |
| 6 | ✅ | `feature/hub-risk-api` | `HubRiskService` (application layer shared with the agent); REST endpoints: hubs, risk breakdown, weather stats, rankings, comparison, model description; domain exceptions → ProblemDetails; options-bound scoring config; full Swagger docs | Unit + endpoint tests |
| 7 | ✅ | `feature/llm-agent` | Own OpenAI-compatible LLM client (Groq / Gemini / Ollama via config) with **model × provider fallback chain**; 6 tools over `HubRiskService`; JSON-schema-enforced `submit_answer` (schema from C# record, strict parse, repair loop) + **grounding check**; in-memory conversation memory; `/api/chat` with its own rate limit | Unit + endpoint (fake LLM) + live validation |
| 8 | ✅ | `feature/chat-ui` | Static HTML/CSS/JS chat page at `/` (no framework/CDN): structured answer rendering with verified-numbers table, follow-up chips, audit panel; voice input + read-aloud (Web Speech API); safe Markdown; CSP; `?q=` deep links | Endpoint (static files, headers) + Node UI unit tests + screenshot check |
| 9 | ✅ | `feature/evaluation` | Black-box eval runner (HTTP) + `eval-cases.json`: 9 deterministic model checks + 13 agent cases (brief questions, follow-up, false premise, out-of-scope, unknown hub, …) with ground truth resolved live; Markdown/JSON report; `AnswerCleaner` from eval findings | Unit (evaluators) + live eval: 9/9, 13/13 |
| 10 | ⏭️ skipped | `feature/risk-alerts` *(bonus)* — not built for time; design in ARCHITECTURE §13 | Scheduled score recompute (`BackgroundService`) recording snapshots, change detection (Observer), log + webhook notifiers, `/api/alerts` and score-history endpoints | Unit + endpoint |
| 11 | 🟡 | `docs/final-and-session` | Final architecture doc (requirements traceability, assumptions/uncertainty/limitations, future work), README quick start; session export tool + transcript in `docs/session/`; full end-to-end verification | Full verification |
| — | ⬜ | *(optional)* | Deploy to a public URL | — |

**Priority (24h budget):** 1–9 and 11 required; 10 and optional items are bonus.

## Hubs (15)
| Region | Hubs |
|---|---|
| Midwest | Chicago, Minneapolis, Detroit, Indianapolis, Kansas City |
| Gulf / South | Miami, Houston, Dallas, New Orleans, Atlanta |
| Mountain / West | Denver, Phoenix, Seattle, Los Angeles |
| Northeast | New York |
