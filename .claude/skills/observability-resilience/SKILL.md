---
name: observability-resilience
description: Mandatory monitoring, rate-limiting, and fallback standard for backend services in this repo. Use whenever adding endpoints, external API calls, LLM calls, or background jobs.
---

# Observability & Resilience Standard

The backend must show how it's doing (traffic, failures, latency) and degrade gracefully when
external services fail — using built-in .NET features only, no extra infrastructure.

## Monitoring
- **Request metrics middleware** records per endpoint: total, succeeded (2xx/3xx), client errors (4xx),
  server errors (5xx), rate-limited (429), and latency (avg / p95 over a rolling window).
- **External dependency metrics**: calls, failures, retries, fallbacks used, and latency per
  dependency (Open-Meteo, FEMA, Groq, Gemini…).
- Exposed via `GET /api/system/metrics` (JSON, documented in Swagger) — and via
  `System.Diagnostics.Metrics` meters so `dotnet-counters` can read them too.
- Counters are lock-free (`Interlocked`) or `ConcurrentDictionary`-based; rolling windows are bounded.
- **Structured logging** (`ILogger` with message templates, not string interpolation); log each
  external failure, fallback, and rate-limit rejection with a correlation/trace id.
- `GET /api/system/health` reports app + dependency status.

## Rate limiting
- ASP.NET Core built-in rate limiter (`AddRateLimiter`), policies from configuration:
  - global per-client sliding window for all `/api` endpoints;
  - stricter policy on `/api/chat` (it triggers LLM calls).
- Rejections return `429` + `Retry-After` + `ProblemDetails`, and are counted in metrics.

## Resilience & fallbacks
- Every outbound `HttpClient` uses `Microsoft.Extensions.Http.Resilience`
  (`AddStandardResilienceHandler` or a tuned pipeline): timeout, retry with jittered backoff on
  transient errors (5xx/408/429), circuit breaker.
- **Fallback chains** (Chain of Responsibility):
  - LLM: primary provider → secondary provider → clear "assistant unavailable" response
    (deterministic endpoints keep working without any LLM).
  - Weather/hazard data: live API → cached data (even if stale, flagged as stale in the response).
- Responses that used a fallback or stale data say so (`dataFreshness`, `provider` fields) — part of
  communicating uncertainty.

## Balance
Keep this lightweight: in-process counters and built-in middleware. No Prometheus/Grafana/OTel
collectors unless explicitly requested.

## Checklist before finishing a task
- [ ] New endpoints are covered by metrics + rate limiting automatically (verify in a test).
- [ ] New outbound calls go through a resilient typed `HttpClient` and record dependency metrics.
- [ ] Failure path tested: dependency down → fallback used → response flags it.
