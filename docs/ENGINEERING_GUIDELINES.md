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
- [x] **Dependency Inversion** — All external concerns (repositories, auth, cache, messaging) are defined as interfaces in Application and implemented in Infrastructure. (`ITaskRepository` in place; `IUserRepository`, `IJwtService` pending.)
- [x] **Application cross-cutting errors** — Shared application exceptions where appropriate; e.g. `NotFoundException` in `Common/Exceptions` (resource name + optional id), used by Task handlers when a task is missing or not visible to the user.
- [ ] **No business logic in controllers** — Controllers only map HTTP to use-case calls and return responses; validation and rules live in Application.

### Naming and Structure

- [x] **Use cases named by intent** — e.g. `CreateTaskCommand`, `ListTasksQuery`; one command/query per operation (CQRS via MediatR).
- [x] **Repository interfaces in Application** — e.g. `IUserRepository`, `ITaskRepository`; implementations in Infrastructure. (ITaskRepository done.)
- [x] **DTOs vs use cases in Application** — Response DTOs live under `DTOs/` (e.g. `DTOs/TaskDto.cs` → namespace `TaskFlow.Application.DTOs`). Commands, queries, validators, and MediatR handlers live under `UseCases/<Area>/<UseCaseName>/` (e.g. `UseCases/Tasks/CreateTask/` → `TaskFlow.Application.UseCases.Tasks.CreateTask`). API maps HTTP to commands/queries and maps DTOs to responses.
- [x] **Consistent namespaces** — Match folder structure; e.g. `TaskFlow.Application.UseCases.Tasks.CreateTask`, `TaskFlow.Application.DTOs`, `TaskFlow.Application.Behaviors`.

### Validation and Security

- [x] **Validation in Application layer** — FluentValidation validators for commands/queries; ValidationBehavior in MediatR pipeline runs validators before handlers.
- [ ] **UserId from JWT only** — Never read UserId from body, query, or route for authorization; always from `sub` (or equivalent) claim.
- [x] **Multi-tenancy in use cases (Task)** — Every Task command/query carries `UserId`; `ITaskRepository` is scoped by user (e.g. `GetByIdAsync(userId, taskId, …)`). **API layer (pending):** controllers must pass `UserId` from JWT `sub` only, not from the client body.

### API and HTTP

- [x] **PageSize cap enforced** — `ListTasksQueryValidator` caps `PageSize` at **20**; API list endpoint must honor the same when exposed.
- [ ] **Sensible HTTP status codes** — 200/201 for success, 400 for validation, 401 for unauthenticated, 403 for forbidden, 404 for not found, 500 for unexpected errors.
- [ ] **Global exception middleware** — Unhandled exceptions are caught and returned as a consistent error response; no stack traces in non-development.

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
- [ ] **NotificationLog entity** — Id, TaskId, EventType, ProcessedAt (entity only; no repository or use cases in Phase 1).
- [x] **Domain logic only in Domain (Task)** — Task invariants and status transitions live in `TaskFlow.Domain` (e.g. `Entities/Task.cs`).

### Application Layer

- [ ] **User use cases** — RegisterUser, LoginUser (returns JWT), GetCurrentUserProfile; each with clear request/response or DTO.
- [x] **Task use cases** — Implemented with MediatR under `UseCases/Tasks/<UseCase>/`: CreateTask, GetTaskById, ListTasks (paginated, filtered, ordered by due date), UpdateTask, UpdateTaskStatus, DeleteTask; each with command/query, FluentValidation validator, and handler.
- [x] **Interfaces** — IUserRepository, ITaskRepository, IJwtService (or IAuthService), plus any other external dependency as interface. (ITaskRepository done.)
- [x] **Validation** — FluentValidation validators for commands/queries (e.g. CreateTaskCommandValidator); ValidationBehavior in MediatR pipeline; validators registered via AddApplicationValidation.
- [x] **Pagination** — `ListTasksQuery` / `ListTasksQueryValidator` in `UseCases/Tasks/ListTasks/`: PageNumber, PageSize (max 20), optional filters (title, description, status), DueDate order (ASC/DESC).

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
- [x] **DI registration** — All Application and Infrastructure services registered in `Program.cs`/startup; no business logic in composition root. (MediatR, AddApplicationValidation registered; Infrastructure pending.)

### Testing (Phase 1)

- [x] **Domain tests** — Unit tests for domain entities/values and any domain rules. *(Task aggregate; add User/Email/NotificationLog when present on branch.)*
- [x] **Application tests (Task)** — `TaskUseCasesTests` exercises Task handlers with an in-memory `ITaskRepository`; progressive coverage for other domains later.
- [x] **No infrastructure in unit tests** — Task application tests use in-memory/mocked repository; no real MongoDB or HTTP.
- [x] **xUnit** — `TaskFlow.Application.Tests` and `TaskFlow.Domain.Tests` use xUnit.

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
