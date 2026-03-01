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
- [ ] **Dependency Inversion** — All external concerns (repositories, auth, cache, messaging) are defined as interfaces in Application and implemented in Infrastructure.
- [ ] **No business logic in controllers** — Controllers only map HTTP to use-case calls and return responses; validation and rules live in Application.

### Naming and Structure

- [ ] **Use cases named by intent** — e.g. `RegisterUser`, `CreateTask`, `ListTasks`; one use case per operation.
- [ ] **Repository interfaces in Application** — e.g. `IUserRepository`, `ITaskRepository`; implementations in Infrastructure.
- [ ] **DTOs/requests in Application** — Commands, queries, and response DTOs live in Application; API maps to/from them.
- [ ] **Consistent namespaces** — Match folder structure; e.g. `TaskFlow.Domain`, `TaskFlow.Application.UseCases.Tasks`.

### Validation and Security

- [ ] **Validation in Application layer** — Use FluentValidation (or equivalent) in Application; controllers do not perform validation logic.
- [ ] **UserId from JWT only** — Never read UserId from body, query, or route for authorization; always from `sub` (or equivalent) claim.
- [ ] **Multi-tenancy in use cases** — Every task operation receives UserId from the authenticated context and filters/checks by it in the use case or repository.

### API and HTTP

- [ ] **PageSize cap enforced** — Paginated list endpoints enforce a maximum PageSize of **20** (validation or constant in Application).
- [ ] **Sensible HTTP status codes** — 200/201 for success, 400 for validation, 401 for unauthenticated, 403 for forbidden, 404 for not found, 500 for unexpected errors.
- [ ] **Global exception middleware** — Unhandled exceptions are caught and returned as a consistent error response; no stack traces in non-development.

### Quality and Tooling

- [ ] **SonarLint enabled** — No critical/blocker issues; address warnings as per team agreement.
- [ ] **Logging** — Structured logging for request/response or key operations; no sensitive data (passwords, tokens) in logs.

---

## Phase 1 — MVP

### Solution and Projects
cation, Infrastructure, API, and Test projects (e.g. `TaskFlow.Domain`, `TaskFlow.Application`, `TaskFlow.Infrastructure`, `TaskFlow.API`, `TaskFlow.Tests` or per-layer test projects).
- [x] **Solution contains:** Domain, Appli
- [x] **Project references follow Clean Architecture** — Dependency graph: API → Infrastructure → Application → Domain; Tests → Application, Domain (and mocks for Infrastructure).

### Domain Layer

- [ ] **User entity** — Id, Name, Email, PasswordHash, CreatedAt; no dependencies on other projects.
- [ ] **Task entity (aggregate root)** — Id, UserId, Title, Description, Status (enum: Pending, InProgress, Completed), DueDate, CreatedAt, UpdatedAt. Domain validation in constructor (e.g. Title required with min/max length, Description optional with max length). Status transition rules enforced via explicit methods SetPending(), SetInProgress(), SetCompleted(): Pending ↔ InProgress, Pending → Completed, InProgress → Completed; Completed is final (frontend may warn that completing cannot be undone).
- [ ] **NotificationLog entity** — Id, TaskId, EventType, ProcessedAt (entity only; no repository or use cases in Phase 1).
- [ ] **Domain logic only in Domain** — Any invariant or rule that belongs to the domain lives in Domain (e.g. status transitions if needed).

### Application Layer

- [ ] **User use cases** — RegisterUser, LoginUser (returns JWT), GetCurrentUserProfile; each with clear request/response or DTO.
- [ ] **Task use cases** — CreateTask, GetTaskById, ListTasks (paginated, filtered, ordered), UpdateTask, UpdateTaskStatus (PATCH), DeleteTask.
- [ ] **Interfaces** — IUserRepository, ITaskRepository, IJwtService (or IAuthService), plus any other external dependency as interface.
- [ ] **Validation** — FluentValidation validators for commands/queries (e.g. CreateTaskValidator); validators registered and executed in pipeline or use case.
- [ ] **Pagination** — ListTasks accepts PageNumber, PageSize (max 20), optional filters (title, description, status), and DueDate order (ASC/DESC).

### Infrastructure Layer

- [ ] **MongoDB** — Separate collections for Users, Tasks; NotificationLog collection optional in Phase 1 (can be added in Phase 2).
- [ ] **Repositories** — UserRepository and TaskRepository implement Application interfaces; use official MongoDB driver or agreed abstraction.
- [ ] **Indexes** — Index on UserId for Tasks; compound index (UserId + DueDate) for Tasks; index on UserId (or Email for login) for Users as needed.
- [ ] **JWT** — Service that generates and validates JWT with UserId in `sub`; no ASP.NET Identity.
- [ ] **Password hashing** — BCrypt (or equivalent) used when registering and when validating login.

### API Layer

- [ ] **Controllers** — Thin controllers: map request to application DTO, call use case, map result to HTTP response.
- [ ] **Auth** — JWT bearer authentication; all Task endpoints require `[Authorize]`; UserId read from claims and passed into use cases.
- [x] **Health check** — `/health` (or equivalent) endpoint for Docker/orchestration; returns 200 when the app is ready.
- [ ] **Exception middleware** — Catches exceptions, returns consistent error payload, appropriate status code, and logs.
- [ ] **DI registration** — All Application and Infrastructure services registered in `Program.cs`/startup; no business logic in composition root.

### Testing (Phase 1)

- [ ] **Domain tests** — Unit tests for domain entities/values and any domain rules.
- [ ] **Application tests** — Unit tests for use cases with mocked IUserRepository, ITaskRepository, IJwtService, etc.; progressive coverage (no need to cover every use case on day one).
- [ ] **No infrastructure in unit tests** — Repositories and external services are mocks; no real MongoDB or HTTP in unit tests.
- [ ] **xUnit** — Test project uses xUnit; tests are deterministic and fast.

### Docker and Environment (Phase 1)

- [ ] **Dockerfile** — Builds and runs the API project; multi-stage build if desired.
- [ ] **docker-compose** — Includes API and MongoDB; API depends on MongoDB; health check or wait strategy so API starts after MongoDB is ready.
- [ ] **Configuration** — Connection strings and JWT settings via configuration (environment variables or appsettings); no secrets in source.

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
- [ ] **.gitignore** — Covers build outputs, user-specific files, and secrets; no committed secrets or credentials.

---

## Checklist Summary

| Phase   | Focus                        | Use this section for |
|---------|-----------------------------|-----------------------|
| All     | Cross-Phase Standards       | Every PR / milestone  |
| Phase 1 | MVP                         | First release         |
| Phase 2 | Cache and Messaging         | Redis, RabbitMQ, NotificationLog |
| Phase 3 | CI/CD                       | GitHub Actions, Docker publish   |

Mark items with `[x]` when done (e.g. `- [x] Domain has zero project references`). Re-check Cross-Phase Standards when adding new code in Phase 2 and Phase 3.
