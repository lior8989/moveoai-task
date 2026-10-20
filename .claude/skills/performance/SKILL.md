---
name: performance
description: Mandatory performance standard (runtime, memory, CPU, I/O) for all code in this repo, balanced against clean design. Use whenever writing or editing code, especially I/O, loops, data access, and caching.
---

# Performance Standard

The system runs on a laptop and calls rate-limited external APIs — efficiency is a feature.

## Rules

### I/O & async
- All I/O is `async` end-to-end with `CancellationToken` passed through. Never `.Result` / `.Wait()`.
- `HttpClient` only via `IHttpClientFactory` / typed clients — never `new HttpClient()` per call.
- Independent external calls run concurrently (`Task.WhenAll`), bounded where needed (`SemaphoreSlim`)
  to respect upstream rate limits.
- Stream/deserialize JSON with `System.Text.Json` (source-generated contexts for hot models where useful).

### Caching
- External data (weather history, FEMA risk) changes slowly → cache in SQLite with a TTL; serve from
  cache first. Computed scores cached and recomputed only when inputs change.
- In-memory caching (`IMemoryCache`) only for small, hot, read-mostly data (e.g. hub list), with size limits.

### Data access (EF Core)
- `AsNoTracking()` for read-only queries; project to DTOs with `Select` — don't load full graphs.
- No N+1: batch with `Include`/joins or a single query; add indexes for lookup columns.
- Bulk-insert history rows in one `SaveChangesAsync`.

### Memory & CPU
- Avoid LINQ chains that materialise repeatedly on large sequences; compute aggregates in one pass.
- Prefer `readonly record struct` / spans for small hot-path value types only when measured to matter.
- No unbounded collections in memory (conversation history is capped/trimmed; caches have limits).

### LLM usage
- LLM calls are the slowest and costliest step: minimise round-trips, send compact tool results
  (aggregates, not raw daily rows), cap conversation context, set timeouts.

## Balance rule (shared with `solid-design-patterns` skill)
- Clean design first; optimise where it matters: I/O, external calls, DB queries, hot loops.
- Don't introduce complex patterns that add allocations/indirection on hot paths without benefit.
- Don't write unreadable "clever" code for unmeasured gains. If an optimisation reduces clarity,
  justify it in a comment.

## Checklist before finishing a task
- [ ] No sync-over-async, no per-call `HttpClient`, tokens flow through.
- [ ] External data cached with TTL; repeated questions don't re-hit external APIs.
- [ ] Read queries are no-tracking and projected; no N+1.
- [ ] Collections and caches are bounded.
