---
name: code-documentation
description: Mandatory documentation standard for all C# code in this repo. Use whenever adding or editing any class, record, interface, method, property, endpoint, or non-obvious logic.
---

# Code Documentation Standard

Every code change must leave the code self-explanatory for a reviewer who has never seen it.

## Rules

1. **Public / protected / internal members** — every type, constructor, method, property, event,
   and enum value gets an XML doc comment (`///`).
   - `<summary>`: what it does and *why it exists*, in 1–3 sentences. Not a restatement of the name.
   - `<param>` for every parameter, `<returns>` for non-void, `<exception>` for anything thrown deliberately.
   - `<remarks>` for units, ranges, assumptions, thread-safety, or performance notes when relevant
     (e.g. "Score is 0–100, higher = more exposed").
2. **Private members** — document when the purpose isn't obvious from the name and type.
3. **Inline comments** — explain *why*, not *what*. Required for: formulas and weights, thresholds,
   magic numbers (prefer named constants), workarounds, and anything a reader would pause on.
4. **API endpoints** — documented both in XML docs and in OpenAPI metadata
   (see the `api-swagger` skill): summary, description, parameters, response codes, examples.
5. **Assumptions** — any domain assumption (e.g. "snow day = snowfall ≥ 1 cm") is documented at the
   constant/definition site AND listed in `docs/ARCHITECTURE.md` → *Assumptions & Uncertainty*.

## Project setup

- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is enabled in `Directory.Build.props`
  so missing docs show up as build warnings (CS1591) — treat them as errors to fix, not to suppress.
- Test projects are exempt from CS1591, but test names must describe the behaviour
  (`Method_Condition_ExpectedResult`).

## Example

```csharp
/// <summary>
/// Computes the winter-disruption sub-score for a hub from historical daily weather.
/// </summary>
/// <param name="metrics">Aggregated weather metrics for the hub's look-back window.</param>
/// <returns>A score from 0 (no exposure) to 100 (extreme exposure).</returns>
/// <remarks>Pure and thread-safe; allocation-free.</remarks>
public double Score(HazardMetrics metrics)
{
    // Snow days dominate winter disruption for road freight, so they carry 60% of the weight.
    ...
}
```

## Checklist before finishing a task
- [ ] Build has zero CS1591 warnings in non-test projects.
- [ ] Every new constant/threshold has a comment explaining its value.
- [ ] `docs/ARCHITECTURE.md` updated for any new component or assumption.
