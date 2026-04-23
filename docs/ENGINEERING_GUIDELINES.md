# TaskFlow API — Engineering Guidelines

Implementation standards and architectural enforcement for the TaskFlow API. Use the checkboxes to track progress as you implement each phase. Aligned with [PROJECT_SPEC.md](PROJECT_SPEC.md).

---

## Cross-Phase Standards (All Phases)

These rules apply in every phase. Verify them during implementation and code review.

### Architecture Enforcement

- [x] **Domain has zero project references** — Domain project does not reference Application, Infrastructure, or API.
- [x] **Application references only Domain** — No Infrastructure or API references in Application.
- [x] **Infrastructure references Application (and Domain via it)** — Implements interfaces defined in Application.
- [x] **API references Application and Infrastructure** — For DI registration and startup only; no business logic in API layer.
- [x] **Dependency Inversion** — All external concerns (repositories, auth, cache, messaging) are defined as interfaces in Application and implemented in Infrastructure. (MongoDB repositories, `IJwtService`, and `IPasswordHasher` are implemented in `TaskFlow.Infrastructure`.)
- [x] **Application expected outcomes without exceptions** — “Not found”, “conflict”, “unauthorized” flows are modeled via `Result`/`Result<T>` (`TaskFlow.Application.Common.Results`) and translated to HTTP by the API layer (`ProblemDetails` + stable `ErrorCode`).
- [x] **No business logic in controllers** — Controllers only map HTTP to use-case calls and return responses; validation and rules live in Application.

### Naming and Structure

- [x] **Use cases named by intent** — e.g. `CreateTaskCommand`, `ListTasksQuery`; one command/query per operation (CQRS via MediatR).
- [x] **Repository interfaces in Application** — e.g. `IUserRepository`, `ITaskRepository`; implementations in Infrastructure. (MongoDB repositories now implemented in `TaskFlow.Infrastructure`.)
- [x] **DTOs vs use cases in Application** — Response DTOs live under `DTOs/` (e.g. `DTOs/TaskDto.cs` → namespace `TaskFlow.Application.DTOs`). Commands, queries, validators, and MediatR handlers live under `UseCases/<Area>/<UseCaseName>/` (e.g. `UseCases/Tasks/CreateTask/` → `TaskFlow.Application.UseCases.Tasks.CreateTask`). API maps HTTP to commands/queries and maps DTOs to responses.
- [x] **Consistent namespaces** — Match folder structure; e.g. `TaskFlow.Application.UseCases.Tasks.CreateTask`, `TaskFlow.Application.DTOs`, `TaskFlow.Application.Behaviors`.

### Validation and Security

- [x] **Validation in Application layer** — FluentValidation validators for commands/queries; ValidationBehavior in MediatR pipeline runs validators before handlers.
- [x] **UserId from JWT only** — Never read UserId from body, query, or route for authorization; always from `sub` claim. API reads it via `ClaimsPrincipal.GetUserId()` which expects a valid GUID in `sub` (falls back to name identifier only when applicable).
- [x] **Multi-tenancy in use cases (Task)** — Every Task command/query carries `UserId`; `ITaskRepository` is scoped by user (e.g. `GetByIdAsync(userId, taskId, …)`). **API layer:** controllers pass `UserId` from JWT `sub` only, not from the client body.

### API and HTTP

- [x] **PageSize cap enforced** — `ListTasksQueryValidator` caps `PageSize` at **20**; API list endpoint must honor the same when exposed.
- [x] **Sensible HTTP status codes** — 200/201 for success, 204 for successful updates/deletes, 400 for validation/argument errors, 401 for unauthenticated, 404 for not found, 409 for conflicts/business rules, 500 for unexpected errors.
- [x] **Global exception handler** — Exceptions are mapped to `ProblemDetails`/`ValidationProblemDetails` via a single global handler (`IExceptionHandler` + `UseExceptionHandler()`); no stack traces in non-development.

### Result-based Flow (No Exceptions for Expected Outcomes)

Expected outcomes (not found, conflicts, invalid credentials) must NOT be represented as exceptions in Application handlers. Handlers must return explicit results and API translates them into HTTP responses.

- **Handlers return `Result`/`Result<T>`** — defined in `TaskFlow.Application.Common`.
  - **Queries**: `Result<T>`
  - **Commands**: `Result` (no payload)
- **Success carries semantics** — success is not a single boolean; it must express whether the API should return a payload or no content (the API translates semantics into HTTP).
- **POST create** — controllers keep `CreatedAtAction(...)` for successful creation (Location header + discoverability). Non-success cases still use `FromResult(...)`.
- **No 403 for multi-tenancy** — for resources owned by another user, return **404** (treat as not found) to avoid leaking existence.
- **BadRequest in Application results** — use `Result.BadRequest(...)` / `Result<T>.BadRequest(...)` for expected client mistakes that are not covered by FluentValidation (maps to HTTP **400** with `ProblemDetails`).

#### Error Contract (HTTP payload)

All expected errors returned from `Result` must be translated into `ProblemDetails` with consistent extensions:

- **`extensions.code`**: stable `ErrorCode` (e.g. `task.not_found`, `auth.invalid_credentials`)
- **`extensions.resource`**: domain resource identifier (e.g. `task`, `user`, `auth`)
- **`extensions.id`**: optional identifier when applicable (e.g. route `taskId`)
- **`extensions.traceId`**: always included for telemetry correlation

FluentValidation failures must translate to **400** using `ValidationProblemDetails` and must include:

- **`extensions.code = request.validation_failed`**
- **`extensions.traceId`** always

### API Host Conventions (TaskFlow.Api)

- [x] **JWT Bearer configuration** — `AddAuthentication().AddJwtBearer(...)` with `MapInboundClaims = false`, `ClockSkew = 0`, issuer/audience validation, and `NameClaimType = sub`.
- [x] **Swagger security** — OpenAPI defines the `Bearer` scheme; `BearerSecurityDocumentFilter` ensures `security` is emitted so Swagger UI sends `Authorization` (paste the raw JWT from `POST /api/users/login`, without a `Bearer ` prefix).
- [x] **Enum serialization** — Enums are serialized as `camelCase` strings.
- [x] **Uniform error responses** — `TaskFlowControllerBase` + `FromResult(...)` map `Result`/`Result<T>` failures to `ProblemDetails` with `code`, `resource`, optional `id`, and `traceId`.

### Quality and Tooling

- [ ] **SonarLint enabled** — No critical/blocker issues; address warnings as per team agreement.
- [ ] **Logging** — Structured logging for request/response or key operations; no sensitive data (passwords, tokens) in logs.

---

## Phase 1 — MVP

### Solution and Projects
Domain, Application, Infrastructure, API, and Test projects (e.g. `TaskFlow.Domain`, `TaskFlow.Application`, `TaskFlow.Infrastructure`, `TaskFlow.API`, `TaskFlow.Tests` or per-layer test projects).
- [x] **Solution contains:** Domain, Application, Infrastructure, API, and Test projects (e.g. `TaskFlow.Domain`, `TaskFlow.Application`, `TaskFlow.Infrastructure`, `TaskFlow.API`, `TaskFlow.Tests` or per-layer test projects).
- [x] **Project references follow Clean Architecture** — Dependency graph: API → Infrastructure → Application → Domain; Tests → Application, Domain (and mocks for Infrastructure).

### Domain Layer

- [ ] **User entity** — Id, Name, Email, PasswordHash, CreatedAt; no dependencies on other projects.
- [x] **Task entity (aggregate root)** — Id, UserId, Title, Description, Status (enum: Pending, InProgress, Completed), DueDate, CreatedAt, UpdatedAt. Domain validation in constructor (title/description length). Status transitions via `SetPending()`, `SetInProgress()`, `SetCompleted()` with rules as specified; `Completed` is final.
- [x] **NotificationLog entity** — Id, TaskId, EventType, ProcessedAt (entity only; no repository or use cases in Phase 1).
- [ ] **Domain logic only in Domain** — Any invariant or rule that belongs to the domain lives in Domain (e.g. status transitions if needed).

### Application Layer

- [x] **User use cases** — RegisterUser, LoginUser (returns JWT + ExpiresIn), GetCurrentUserProfile under `UseCases/User/<UseCase>/` (command/query, validator, handler); API wiring implemented.
- [x] **Task use cases** — Implemented with MediatR under `UseCases/Tasks/<UseCase>/`: CreateTask, GetTaskById, ListTasks (paginated, filtered, ordered by due date), UpdateTask, UpdateTaskStatus, DeleteTask; each with command/query, FluentValidation validator, and handler.
- [x] **Interfaces** — IUserRepository, ITaskRepository, IJwtService, IPasswordHasher, plus any other external dependency as interface. (MongoDB persistence, JWT, and BCrypt are implemented in Infrastructure.)
- [x] **Validation** — FluentValidation validators for commands/queries (e.g. CreateTaskCommandValidator); ValidationBehavior in MediatR pipeline; validators registered via AddApplicationValidation.
- [x] **Pagination** — `ListTasksQuery` / `ListTasksQueryValidator` in `UseCases/Tasks/ListTasks/`: PageNumber, PageSize (max 20), optional filters (title, description, status), DueDate order (ASC/DESC).

### Infrastructure Layer

- [x] **MongoDB** — Separate collections for Users, Tasks; NotificationLog collection optional in Phase 1 (can be added in Phase 2).
- [x] **Repositories** — UserRepository and TaskRepository implement Application interfaces; use official MongoDB driver or agreed abstraction.
- [x] **Indexes** — Index on UserId for Tasks; compound index (UserId + DueDate) for Tasks; index on UserId (or Email for login) for Users as needed.
- [x] **JWT** — `JwtService` issues JWT access tokens with UserId in `sub` and returns `ExpiresInSeconds`; no ASP.NET Identity.
- [x] **Password hashing** — `BCryptPasswordHasher` used when registering and validating login; work factor is configuration-driven and validated.

### API Layer

- [x] **Controllers** — Thin controllers: map request to application DTO/command/query, call use case via `IMediator`, map result to HTTP response.
- [x] **Auth** — JWT bearer authentication; all Task endpoints require `[Authorize]`; UserId read from claims (`sub`) and passed into use cases.
- [x] **Health check** — `/health` (or equivalent) endpoint for Docker/orchestration; returns 200 when the app is ready.
- [x] **Exception handler** — Catches exceptions, returns consistent `ProblemDetails` payloads, appropriate status codes, and no stack traces in non-development.
- [x] **DI registration** — Application services and infrastructure registration are wired in `Program.cs`/startup; no business logic in composition root (MongoDB + indexes initializer, JWT, BCrypt, MediatR, Validation, Swagger, `AddProblemDetails()` + `IExceptionHandler`, `UseExceptionHandler()`).

---

## Current Notes / Known Deviations (Keep Honest)

- **Use-case vs controller responsibility for 404**: Some controllers currently return `NotFound()` when a use case returns `null` (e.g. `GetTaskById`, `GetCurrentUserProfile`). Prefer a single approach: either (a) Application throws `NotFoundException` and API relies on global mapping, or (b) Application returns `null` and controllers handle it. Avoid mixing styles within the same bounded context.
- **Docker / HTTPS**: `docker-compose.override.yml` runs the API container on **HTTP only** (no HTTPS listener inside the container). Production-style TLS is expected to terminate at a reverse proxy or Ingress (see project discussions). `UseHttpsRedirection()` is skipped in **Development** so local container runs do not require a dev certificate inside the image.

### Testing (Phase 1)

- [x] **Domain tests** — Unit tests for domain entities/values and any domain rules.
- [x] **Application tests** — Unit tests for use cases with mocked IUserRepository, ITaskRepository, IJwtService, IPasswordHasher, etc.; progressive coverage (no need to cover every use case on day one).
- [x] **No infrastructure in unit tests** — Repositories and external services are mocks; no real MongoDB or HTTP in unit tests.
- [x] **xUnit** — Test project uses xUnit; tests are deterministic and fast.

### Docker and Environment (Phase 1)

- [x] **Dockerfile** — Multi-stage build for the API; final image runs as non-root; `curl` installed for compose healthchecks; comments in English.
- [x] **docker-compose** — API + MongoDB; `depends_on` with `service_healthy` on Mongo; API healthcheck hits `GET /health`; pinned image tags; optional **mongo-express** for local DB UI (not required by spec).
- [x] **Configuration** — `MongoDb__ConnectionString` (and Mongo credentials) supplied via compose / `.env` (see `.env.example`); JWT remains in configuration (`appsettings` / User Secrets on host). **`.env` is gitignored** — do not commit real secrets.

---

## Phase 2 — Cache and Messaging

### Redis (Cache)

- [ ] **Cache interface in Application** — e.g. `ITaskCache` or `ICacheService` with Get/Set (or GetOrSet) for task by id.
- [ ] **Implementation in Infrastructure** — Redis-backed implementation; connection and serialization configured in Infrastructure.
- [ ] **Cache Aside for GetTaskById** — Use case (or application service) checks cache first; on miss, load from repository and populate cache; cache key includes UserId and TaskId for multi-tenancy safety.
- [ ] **Invalidation** — Update and Delete task invalidate or remove the cached entry for that task.
- [ ] **docker-compose** — Redis service added; API configured to use Redis when available.

### RabbitMQ (Messaging)

- [ ] **Event contracts in Application** — Events such as TaskCreated, TaskCompleted defined (DTOs or messages) in Application; no RabbitMQ types in Application.
- [ ] **Publisher interface in Application** — e.g. `IMessagePublisher` or `IEventPublisher` with Publish method; implemented in Infrastructure.
- [ ] **Publish on Create/Update status** — When a task is created or status set to Completed, publish corresponding event from the use case (or domain event handler) via the interface.
- [ ] **Consumer in Infrastructure** — Consumer(s) that handle TaskCreated/TaskCompleted and persist NotificationLog (or trigger side effects); wiring in Infrastructure.
- [ ] **docker-compose** — RabbitMQ service added; API and consumer configured to connect to RabbitMQ.

### NotificationLog (Phase 2)

- [ ] **INotificationLogRepository in Application** — Interface for persisting NotificationLog entries.
- [ ] **Implementation in Infrastructure** — MongoDB collection for NotificationLogs; repository implementation.
- [ ] **Consumer writes NotificationLog** — When processing TaskCreated/TaskCompleted (or equivalent), consumer (or application handler) creates NotificationLog records with TaskId, EventType, ProcessedAt.
- [ ] **Indexes** — Index on TaskId and/or ProcessedAt if needed for queries.

### Testing (Phase 2)

- [ ] **Cache and messaging mocked in unit tests** — New use-case tests still mock cache and publisher; no real Redis/RabbitMQ in unit tests.
- [ ] **Integration tests (optional)** — If you add integration tests for cache or messaging, they are clearly separated from unit tests and optional for local runs.

---

## Phase 3 — CI/CD

### GitHub Actions

- [ ] **CI workflow** — On push/PR: restore, build, run tests; fail if build or tests fail.
- [ ] **No secrets in workflows** — Use GitHub secrets or environment for any credentials; no hardcoded secrets.
- [ ] **CD / Publish (optional)** — Workflow or separate workflow to build Docker image and push to a registry (e.g. GitHub Container Registry or Docker Hub); trigger on tag or main branch as agreed.
- [ ] **Docker image** — Image is built from the same Dockerfile used locally; tagged with commit SHA or version.

### Documentation and Hygiene

- [ ] **README** — How to build, run tests, run with Docker; link to PROJECT_SPEC.md and this file.
- [x] **.gitignore** — Covers build outputs, user-specific files, and secrets (including local `.env`); no committed secrets or credentials.

---

## Checklist Summary

| Phase   | Focus                        | Use this section for |
|---------|-----------------------------|-----------------------|
| All     | Cross-Phase Standards       | Every PR / milestone  |
| Phase 1 | MVP                         | First release         |
| Phase 2 | Cache and Messaging         | Redis, RabbitMQ, NotificationLog |
| Phase 3 | CI/CD                       | GitHub Actions, Docker publish   |

Mark items with `[x]` when done (e.g. `- [x] Domain has zero project references`). Re-check Cross-Phase Standards when adding new code in Phase 2 and Phase 3.
