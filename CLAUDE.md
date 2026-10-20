# CLAUDE.md — Weather Risk Intelligence Agent

AI agent that helps logistics analysts decide which US distribution hubs are most exposed to weather
disruption. C# / .NET 10 backend (ASP.NET Core Minimal API), SQLite, static HTML/JS chat UI with voice,
LLM via OpenAI-compatible API (Groq primary). See `docs/ARCHITECTURE.md` and `docs/PLAN.md`.

## Mandatory skills — apply ALL of them to every code change, component, and design decision
| Skill | Path |
|---|---|
| Documentation | `.claude/skills/code-documentation/SKILL.md` |
| Swagger / API & schema docs | `.claude/skills/api-swagger/SKILL.md` |
| SOLID & design patterns (no over-engineering) | `.claude/skills/solid-design-patterns/SKILL.md` |
| Performance | `.claude/skills/performance/SKILL.md` |
| Testing (unit + endpoint) | `.claude/skills/testing/SKILL.md` |
| Observability & resilience | `.claude/skills/observability-resilience/SKILL.md` |

Read the relevant skill files at the start of each task and run through their checklists before
reporting the task as done.

## Git workflow (strict)
- One branch per task from `docs/PLAN.md`, created from an up-to-date `main`. No parallel worktrees.
- When a task is implemented and tests pass: **stop and report** (changed files, summary, test results).
  **Do not commit or push until the user approves.**
- After approval: commit + push the branch. The user opens the PR and merges, then says to continue.
- `git` is not on PATH — use Visual Studio's bundled git:
  `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe`

## Living documentation
Every task updates, in the same branch:
- `docs/ARCHITECTURE.md` — sections touched by the task (components, schema, decisions, assumptions).
- `README.md` — run instructions for anything new.
- `docs/PLAN.md` — task status.

## Secrets
Never commit or print API keys. LLM keys live in .NET user-secrets or environment variables only.

## Commands
- Build: `dotnet build`
- Test: `dotnet test` (and UI JS: `node --test "tests/ui/*.test.mjs"`)
- Run API (serves the chat UI + Swagger): `dotnet run --project src/WeatherRisk.Api`
- Run evaluation: `dotnet run --project src/WeatherRisk.Eval`
- New migration: `dotnet tool restore; dotnet ef migrations add <Name> --project src/WeatherRisk.Data --output-dir Persistence/Migrations`
