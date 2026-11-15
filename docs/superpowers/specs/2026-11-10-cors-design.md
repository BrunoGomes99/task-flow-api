# TaskFlow — Browser CORS Design

Date: 2026-11-10  
Status: **Implemented** on `feature/cors`. Plan: [../plans/2026-11-10-cors.md](../plans/2026-11-10-cors.md).

The Angular app lives in another repository and will be published later on Cloudflare Pages. This design makes the API callable from a browser. TLS for the deployed API is a separate, later change: [2026-11-10-alb-https-design.md](2026-11-10-alb-https-design.md).

## Goal

Let a browser on an allowed origin call the existing API with `Authorization: Bearer`. Local Angular on `http://localhost:4200` works as soon as this branch is merged. The Cloudflare Pages origin is configuration, added when that hostname exists, without another code change.

## Non-goals

- Refresh tokens, cookie sessions, or `Access-Control-Allow-Credentials`.
- Changing login, register, task routes, status codes, or `ProblemDetails`.
- HTTPS on the ALB, an ACM certificate, or a custom domain. Those belong to the ALB HTTPS design.
- Hosting the Angular app in this repository.
- CORS on the scheduler or the notifications worker. They have no browser clients.
- Rejecting a disallowed origin with a new HTTP status. The browser enforces CORS. curl, Swagger on the API host, and the ALB health checks keep working with no `Origin` header.
- A hardcoded Pages hostname in source or in committed tfvars.

## Decisions (Approved)

### D1 — Explicit origins from configuration

The section name is `Cors`. The only setting is `AllowedOrigins`, a list of absolute origins.

An origin is `scheme://host` or `scheme://host:port`. No path, query, fragment, or trailing slash. Comparison is ordinal and case-sensitive, which matches the browser `Origin` header.

| Environment | Value |
|---|---|
| `appsettings.json` | `[]` |
| `appsettings.Development.json` | `["http://localhost:4200"]` |
| ECS API task | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … from Terraform `cors_allowed_origins` |

An empty list is valid. The API starts, and no cross-origin browser call is allowed. Production stays empty until `cors_allowed_origins` is set in `terraform.tfvars` (gitignored). The example file shows the shape with a placeholder, commented out.

The app trims whitespace and one trailing slash before building the policy, so a local appsettings typo still matches the browser header. Terraform rejects an origin that has a path, a query, or a trailing slash, so the deployed value is exact.

### D2 — One named policy

Policy name: `TaskFlow`.

When the list is non-empty, the policy uses those origins only. It never calls `AllowAnyOrigin`.

Allowed methods: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`. The CORS middleware answers the preflight `OPTIONS` itself. The controllers do not gain an `OPTIONS` action.

Allowed request headers: `Authorization`, `Content-Type`, `Accept`. Angular sends these on authenticated JSON calls. Any other requested header fails the preflight until it is added here.

`SupportsCredentials` stays false. The access token travels in the `Authorization` header, not in a cookie.

### D3 — Middleware order

`UseCors("TaskFlow")` runs after `UseHttpsRedirection` and before `UseAuthentication` and `UseAuthorization`.

A preflight from an allowed origin receives `204` and `Access-Control-Allow-Origin` set to that origin, with no JWT. A preflight from any other origin does not receive `Access-Control-Allow-Origin`. The response status of a rejected preflight is not part of the contract.

### D4 — ECS wiring is API-only and defaults to empty

Root variable `cors_allowed_origins` is `list(string)`, default `[]`. It is passed only into the API container environment. Scheduler and notifications tasks do not receive it.

Merging this branch does not require a Pages URL and does not change an existing `terraform apply` that omits the variable.

## Success criteria

- [x] `Cors:AllowedOrigins` in Development is `http://localhost:4200`. The base appsettings list is empty.
- [x] The policy name is `TaskFlow`. It does not allow any origin, and it does not support credentials.
- [x] Allowed methods are `GET`, `POST`, `PUT`, `PATCH`, and `DELETE`. Allowed headers are `Authorization`, `Content-Type`, and `Accept`.
- [x] An empty list builds a policy with no origins and does not throw at startup.
- [x] A trailing slash in configuration is stored without the slash.
- [x] `UseCors` is registered before authentication.
- [x] ECS sets `Cors__AllowedOrigins__N` only on the API task, from `cors_allowed_origins`, default empty.
- [x] A manual preflight from `http://localhost:4200` to `PATCH` returns `204` and echoes that origin. A different origin does not receive `Access-Control-Allow-Origin`.
