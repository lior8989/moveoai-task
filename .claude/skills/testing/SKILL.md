---
name: testing
description: Mandatory testing standard — unit tests for every class/library and endpoint tests for every API covering the full request-to-response flow. Use whenever adding or editing code or endpoints.
---

# Testing Standard

No task is finished until its tests exist and `dotnet test` is green.

## Stack
- **xUnit** with its built-in `Assert` (FluentAssertions is avoided — v8+ requires a commercial license).
- **NSubstitute** for fakes of interfaces where a hand-written fake would be longer.
- **Microsoft.AspNetCore.Mvc.Testing** (`WebApplicationFactory<Program>`) for endpoint tests.
- **SQLite in-memory** (`DataSource=:memory:`) for repository tests — real SQL, no EF InMemory provider.

## Unit tests — every class with logic
- One test class per production class: `<ClassName>Tests`, in a mirrored folder of `<Project>.Tests`.
- Naming: `Method_Condition_ExpectedResult`.
- Cover: happy path, boundaries (0, max, empty, null), invalid input, and each branch of scoring logic.
- Deterministic scoring must have **exact expected values** for known inputs (golden tests).
- External systems are always faked: `HttpMessageHandler` stub for HTTP clients, fake `ILlmProvider`
  for the agent. Tests never call the internet.

## Endpoint tests — every API endpoint
- Run the real pipeline (routing, validation, middleware, DI, serialization) via `WebApplicationFactory`,
  replacing only external dependencies (HTTP data sources, LLM) and using a fresh SQLite DB.
- For each endpoint assert: status code, content type, response body shape/values, and
  error cases (400 validation, 404 not found, 429 rate limit where applicable) as `ProblemDetails`.
- Include at least one full flow test per feature, e.g. `POST /api/chat` → tool call → scoring → JSON answer.

## Structure
```
tests/
  WeatherRisk.Core.Tests/
  WeatherRisk.Data.Tests/
  WeatherRisk.Api.Tests/        ← endpoint tests live here
```

## Checklist before finishing a task
- [ ] Every new/changed class with logic has unit tests.
- [ ] Every new/changed endpoint has endpoint tests incl. error cases.
- [ ] `dotnet test` passes locally; result summary reported in the task review message.
