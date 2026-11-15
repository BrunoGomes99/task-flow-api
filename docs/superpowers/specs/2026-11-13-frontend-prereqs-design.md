# TaskFlow — Frontend Prerequisites Design

Date: 2026-11-13  
Status: **Approved**, in progress on `feature/frontend-prereqs`.

The Angular app ([task-flow-web](https://github.com/BrunoGomes99/task-flow-web)) lists seven backend dependencies in section 7 of its `PROJECT_SPEC.md`. This design closes them before frontend development starts.

## Goal

Ship every API change the frontend needs, with the contracts below frozen so the frontend can generate its OpenAPI types once.

## Non-goals

- Changing the due date to a date-only type. The API keeps a UTC instant. The frontend sends the chosen local date as 23:59:59 local time converted to UTC (task-flow-web ADR 0005), so "today" is accepted without the API knowing the user's time zone.
- Changing the reminder window. A reminder is not sent when a new due date is less than 23 hours away, and with the end-of-day convention the reminder arrives the evening before the due date.
- Caching the summary.
- Hosting the frontend or a CDN for e-mail assets.

## Decisions (Approved)

### D1 — Summary endpoint

`GET /api/tasks/summary` returns the count of the caller's tasks per status, ignoring any filter:

```json
{ "pending": 3, "inProgress": 2, "completed": 5, "total": 10 }
```

All three statuses are always present, zero when empty. The count is a MongoDB `$group` on `Status` after `$match` on `UserId`. No cache.

### D2 — Create no longer accepts a status

`POST /api/tasks` body is `{ title, description?, dueDate? }`. Tasks are always created as `pending`. A `status` sent by an old client is ignored by the JSON binder.

### D3 — Update replaces the due date

`PUT /api/tasks/{id}` body is `{ title, description?, dueDate? }`. `PUT` is a full replacement: an omitted or `null` `dueDate` clears the date. A client that only wants to rename a task must send the current `dueDate` back.

### D4 — Completed tasks are locked

`PUT /api/tasks/{id}` on a `completed` task returns `409` with code `task.locked`. The entity exposes `CanBeEdited()`; the handler translates `false` into the conflict. Delete is still allowed.

Order of checks: `404 task.not_found`, then `409 task.locked`, then the due date rule (D5).

### D5 — A past due date is rejected only when it is set or changed

The rule is `dueDate > UtcNow`.

- **Create:** checked by `CreateTaskCommandValidator` when `dueDate` is present.
- **Update:** checked by the handler, only when the new value differs from the stored value. The task stores its due date through `UtcInstant.Normalize` (UTC, truncated to milliseconds, as MongoDB stores it), and the handler compares against that. `DueInstant` delegates to the same method. An overdue task whose date is sent back unchanged is saved. Clearing the date is always allowed.

On create, a violation returns `400 request.validation_failed` with `errors.DueDate`, the same shape as any other field error. On update, the handler returns `Result.BadRequest`: `400` with code `task.due_date_in_past` and the message in `detail`. Handlers do not throw. The clock is the injected `TimeProvider`.

### D6 — Tasks without a due date are last

`GET /api/tasks` sorts with an aggregation: `hasDueDate` descending, then `DueDate` in the requested order, then `CreatedAt` descending, then `_id` ascending. The two tie-breakers make pagination stable for tasks that share a due date, which the board's infinite scroll relies on.

### D7 — Enums are camelCase strings in OpenAPI

The JSON already uses `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. The OpenAPI document must describe the same values (`pending`, `inProgress`, `completed`, `ascending`, `descending`), so the frontend's generated types match the wire format. A test locks this.

### D8 — HTML reminder e-mail

- The template is an embedded resource in Infrastructure. Every placeholder value is HTML-encoded. The task title comes from the user.
- Brevo receives `htmlContent` and `textContent`.
- The due date is shown as `dd/MM/yyyy` in `America/Sao_Paulo`. `2026-11-21T02:59:59Z` is shown as `20/11/2026`.
- The button links to `{ReminderEmail:WebBaseUrl}/tasks/{taskId}`. Development uses `http://localhost:4200`.
- The logo is a public PNG referenced by URL (`ReminderEmail:LogoUrl`). Inline CID images and data URIs are not used: Brevo has no reliable inline attachment, and Gmail blocks data URIs.

### D9 — Logo in a public S3 bucket

Terraform module `email-assets` creates a dedicated bucket. Only objects under `email/*` are publicly readable through a bucket policy. The PNG is versioned in `infra/assets/email/` and uploaded by Terraform. The notifications task receives `ReminderEmail__LogoUrl` from the module output and `ReminderEmail__WebBaseUrl` from the root variable `web_base_url`.

The legibility of the logo on the purple e-mail header (`#5211A5`) must be checked before the first deploy. A light variant is a brand decision.

## Breaking changes

- `POST /api/tasks` ignores `status`.
- `PUT /api/tasks/{id}` clears the due date when `dueDate` is omitted.
- `PUT /api/tasks/{id}` on a completed task returns `409`.

## Success criteria

- [x] `GET /api/tasks/summary` returns the three statuses and the total, zero when empty.
- [x] `POST /api/tasks` creates `pending` tasks and rejects a past `dueDate` with `errors.DueDate`.
- [x] `PUT /api/tasks/{id}` changes and clears `dueDate`; an unchanged overdue date is accepted; a changed past date is rejected.
- [x] `PUT /api/tasks/{id}` on a completed task returns `409 task.locked`.
- [x] Tasks without a due date are last in both orders, and pagination is stable.
- [ ] OpenAPI enums are camelCase strings.
- [ ] The reminder e-mail is HTML, shows the date in `America/Sao_Paulo`, encodes the title, and links to the task.
- [ ] Terraform publishes the logo under `email/*` and passes its URL to the notifications task.
