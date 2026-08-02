# TaskFlow — Result + Error Contract Design (API ⇄ Application)

Date: 2026-04-17

## Goal

Standardize **expected outcomes** across the API so we do **not** use exceptions as control-flow for normal user scenarios (e.g. not found, business conflicts, invalid credentials), while keeping a consistent and telemetry-friendly HTTP error contract.

This design applies to:

- Handlers in `TaskFlow.Application` (queries/commands)
- Controllers in `TaskFlow.Api`
- Error payload mapping (`ProblemDetails`, `ValidationProblemDetails`)

## Non-goals

- Replace FluentValidation behavior (validation still maps to HTTP 400).
- Change routing conventions or add refresh-token flows.
- Add full telemetry stack (only prepare contract fields such as `traceId`).

## Decisions (Approved)

### D1 — Result-based flow (no exceptions for expected outcomes)

- Application handlers return explicit results:
  - **Queries** return `Result<T>`
  - **Commands** return `Result` (no payload)
- Expected outcomes must **not** be represented as exceptions:
  - Not found / multi-tenancy hiding
  - Business conflicts (domain/application rules)
  - Invalid credentials

Exceptions remain for:

- Unexpected bugs / impossible states
- Infrastructure failures
- Misconfiguration (missing env/appsettings)

### D2 — API translation via helper + base controller

- A single helper/extension in `TaskFlow.Api` translates `Result` / `Result<T>` into HTTP responses.
- Controllers inherit from a project base controller (e.g. `TaskFlowControllerBase`) exposing:
  - `protected IActionResult FromResult(Result result)`
  - `protected IActionResult FromResult<T>(Result<T> result)`

### D3 — HTTP error contract includes telemetry-friendly extensions

For all expected errors returned from `Result`, API returns `ProblemDetails` with:

- `extensions.code` (stable `ErrorCode`)
- `extensions.resource` (string)
- `extensions.id` (optional, when applicable)
- `extensions.traceId` (always)

FluentValidation failures return `ValidationProblemDetails` (HTTP 400) and also include:

- `extensions.code = request.validation_failed`
- `extensions.traceId` always

## Result Model (Application.Common)

### Outcomes

`Result`/`Result<T>` must represent:

- `Ok` (success with payload — for `Result<T>`)
- `NoContent` (success with no payload — for `Result`)
- `NotFound`
- `Conflict`
- `Unauthorized`
- `BadRequest` (expected client mistake not modeled as FluentValidation failures)

Notes:

- Success carries semantics (`Ok` vs `NoContent`) so controllers don't decide status codes ad-hoc.
- Create endpoints (POST) remain special-cased in controllers to use `CreatedAtAction(...)` for success.

### Multi-tenancy / Forbidden

The system does **not** expose 403 for cross-user access. Instead:

- Resource exists but belongs to another user → return `NotFound` (404).

## ErrorCode Conventions

### Format

- `<domain>.<reason>` (lowercase, stable)
- Not tied to endpoint names

### Initial catalog

- `auth.invalid_credentials` (401)
- `auth.missing_or_invalid_sub` (401)
- `user.email_already_in_use` (409)
- `user.not_found` (404)
- `task.not_found` (404)
- `task.status_transition_invalid` (409)
- `request.validation_failed` (400 — FluentValidation)
- `request.invalid_argument` (400)
- `server.unexpected_error` (500)

## HTTP Mapping

### Result → HTTP (expected outcomes)

- `Ok<T>` → 200 with body
- `NoContent` → 204
- `NotFound` → 404 + `ProblemDetails` (+ extensions)
- `Conflict` → 409 + `ProblemDetails` (+ extensions)
- `Unauthorized` → 401 + `ProblemDetails` (+ extensions)
- `BadRequest` → 400 + `ProblemDetails` (+ extensions)

### FluentValidation

- `ValidationException` → 400 + `ValidationProblemDetails`
  - Add `extensions.code = request.validation_failed`
  - Add `extensions.traceId`

## Implementation Plan (high level, no code)

1. Add `Result` / `Result<T>` types + primitives in `TaskFlow.Application.Common`.
2. Add API helper/extension that converts `Result`/`Result<T>` to `IActionResult` with `ProblemDetails` contract.
3. Add `TaskFlowControllerBase` using the helper.
4. Migrate endpoints incrementally:
   - Tasks first (covers 404/409/204 patterns)
   - Users/Auth (login unauthorized + conflicts)
5. Remove legacy application exceptions used for expected control flow (e.g. `NotFoundException`) now that `Result`/`Result<T>` covers these cases end-to-end.

## Acceptance Criteria

- No handler throws exceptions for expected outcomes (not found / conflict / invalid credentials).
- All 400/401/404/409 responses include `ProblemDetails` (or `ValidationProblemDetails` for FluentValidation) with `code`, `resource`, and `traceId` (and `id` when applicable).
- Validation errors return 400 `ValidationProblemDetails` with `code=request.validation_failed` and `traceId`.
- Controllers remain thin and consistent (`FromResult(...)` used everywhere except POST success paths using `CreatedAtAction`).

