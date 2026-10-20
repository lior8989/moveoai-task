---
name: solid-design-patterns
description: Mandatory design standard (SOLID + design patterns, without over-engineering) for all code and design planning in this repo. Use whenever designing a component, adding a class, or refactoring.
---

# SOLID & Design Patterns — Pragmatic

Goal: code that is easy to change, test, and explain in an interview. Patterns are tools, not goals.

## SOLID — apply all five

| Principle | How it shows up here |
|---|---|
| **S**ingle Responsibility | One reason to change per class. API clients fetch; scorers score; repositories persist; endpoints translate HTTP ↔ application calls. |
| **O**pen/Closed | Add a new hazard, LLM provider, or alert channel by adding a class — not editing a switch. |
| **L**iskov Substitution | Any `IHazardScorer` / `ILlmProvider` / `IAlertNotifier` implementation is a drop-in replacement; no type checks on implementations. |
| **I**nterface Segregation | Small, focused interfaces (`IHubRepository`, `IWeatherDataSource`) over "god" interfaces. |
| **D**ependency Inversion | Core depends on abstractions; infrastructure (HTTP, EF, LLM) implements them; wiring via built-in DI. |

## Patterns expected in this system (use where they fit)

| Pattern | Where |
|---|---|
| **Strategy** | Per-hazard scorers; LLM providers (Groq / Gemini / Ollama). |
| **Repository** | Data access for hubs, scores, cache, conversations. |
| **Observer** | Risk-score change → alert notifiers (log, webhook). |
| **Decorator** | Caching/resilience around data sources without changing them. |
| **Chain of Responsibility / Fallback** | LLM provider fallback chain; live data → cache fallback. |
| **Builder** | Composing LLM prompts/requests with tools and schema. |
| **Singleton** | Via DI lifetime (`AddSingleton`) only — never static mutable state. |
| **Options** | Configuration (`IOptions<T>`) for weights, thresholds, provider settings. |

## Anti-over-engineering rules (equally mandatory)

- **No pattern without a second use case or a testability need.** One implementation that will
  never vary? A plain class is fine — an interface is justified only if it's needed to fake it in tests.
- No generic repositories, no "manager/helper/util" layers, no abstract base classes "for the future".
- Prefer composition over inheritance; prefer records and pure functions for domain logic.
- If explaining the abstraction takes longer than explaining the code it wraps, remove it.

## Balance rule (shared with `performance` skill)

Design and performance are weighed together on every change:
- Don't add indirection layers on hot paths (per-request, per-row loops) unless they pay for themselves.
- Don't sacrifice readability for micro-optimisations that don't show up in measurements.
- When they conflict, pick the simpler option and note the trade-off in a code comment or in
  `docs/ARCHITECTURE.md` → *Design decisions*.

## Checklist before finishing a task
- [ ] Each new class has one clear responsibility (state it in its `<summary>`).
- [ ] Dependencies flow inward (Api → Core ← Data); Core references no infrastructure packages.
- [ ] Every interface has either ≥2 implementations or a test fake using it.
- [ ] Notable pattern choices recorded under *Design decisions* in `docs/ARCHITECTURE.md`.
