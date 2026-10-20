---
name: api-swagger
description: Mandatory OpenAPI/Swagger standard for every HTTP API and DB schema in this repo. Use whenever adding or changing an endpoint, request/response model, or database entity.
---

# API & Schema Documentation (Swagger / OpenAPI)

Anyone must be able to open Swagger UI and test every endpoint live, without reading code.

## Rules

1. **Every service exposing HTTP endpoints serves OpenAPI + Swagger UI**
   - .NET 10: `builder.Services.AddOpenApi()` + `app.MapOpenApi()` for the document,
     Swagger UI (Swashbuckle.AspNetCore.SwaggerUI) pointed at `/openapi/v1.json`.
   - UI at `/swagger`. Enabled in Development and in the demo environment.
2. **Every endpoint declares**
   - `.WithName()`, `.WithSummary()`, `.WithDescription()`, `.WithTags()` (group by resource).
   - All response types and status codes: `.Produces<T>(200)`, `.ProducesProblem(400/404/429/500)`.
   - Parameter descriptions (XML docs on request records, `[Description]` where needed).
   - At least one realistic example value for non-trivial requests (e.g. a `/chat` body).
3. **Errors** use RFC 7807 `ProblemDetails` consistently — never ad-hoc error strings.
4. **Request/response models** are records in the contract layer, documented field-by-field
   (units, ranges, nullability). Never expose EF entities directly over the API.
5. **DB schema visibility**
   - Every EF entity and column documented via XML docs and `HasComment()` in configuration.
   - A read-only `GET /api/system/schema` endpoint (tag *System*) returns tables/columns/types,
     generated from the EF model at runtime — so it can never drift from reality.
   - `docs/ARCHITECTURE.md` contains an ER diagram (Mermaid) of the schema.
6. **Versioning** — routes are prefixed `/api/...`; breaking changes need a doc note.

## Checklist before finishing a task
- [ ] New/changed endpoints visible and callable in `/swagger` with descriptions and all response codes.
- [ ] Schema endpoint and Mermaid ER diagram reflect any entity change.
- [ ] Endpoint tests exist for each new endpoint (see `testing` skill).
