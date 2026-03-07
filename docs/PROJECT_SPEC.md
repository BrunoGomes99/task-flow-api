# TaskFlow API — Project Specification

This document describes the approved architecture and scope for the **TaskFlow API** backend. The project is for study purposes, applying modern .NET architecture and best practices with a simple domain and mature structure.

---

## 1. Project Goal

Build a RESTful Web API using **.NET 10** with:

- Clean Architecture
- CQRS (Command Query Responsibility Segregation) via MediatR
- MediatR for mediator pattern and pipeline behaviors
- FluentValidation for input validation in the Application layer
- SOLID principles and Clean Code practices
- JWT authentication
- MongoDB (NoSQL)
- RabbitMQ and Redis in a later phase
- Docker containerization
- GitHub Actions CI/CD in a later phase
- Unit testing with xUnit
- SonarLint for code quality

The system must be simple in domain complexity but architecturally mature.

---

## 2. Scope Rules (All Phases)

- **PageSize:** Maximum **20** for paginated list endpoints.
- **Health checks:** Expose a `/health` (or equivalent) endpoint for Docker and orchestration.
- **Validation:** Implement in the **Application** layer (e.g. FluentValidation); keep controllers thin.

---

## 3. Architecture

### 3.1 Clean Architecture Layout

- **Domain** — Entities, value objects, domain rules. No dependencies on other layers.
- **Application** — Use cases (MediatR handlers), interfaces (repositories, services), DTOs, Command/Query Responsibility Segregation (CQRS), FluentValidation validators, pipeline behaviors.
- **Infrastructure** — Implementations of external concerns: MongoDB, Redis, RabbitMQ.
- **API** — Controllers, middleware, filters, and configuration. Controllers send Commands/Queries via `IMediator`.
- **Tests** — Unit tests for Domain and Application.

### 3.2 Guidelines

- Domain must not depend on any other layer.
- Application holds use cases and interfaces; Infrastructure implements them.
- Strict Dependency Inversion: depend on abstractions (interfaces).
- Use interfaces for repositories and external services.

### 3.3 CQRS and MediatR

- **CQRS** — Commands (writes) and Queries (reads) are separated. Each use case has a request type (e.g. `CreateTaskCommand`, `ListTasksQuery`) and a handler.
- **MediatR** — Mediator pattern: controllers send requests via `IMediator.Send()`; handlers execute the use case logic. No direct handler injection in controllers.
- **Pipeline behaviors** — ValidationBehavior runs FluentValidation before the handler; validation failures throw `ValidationException` (API maps to 400 Bad Request).

---

## 4. Domain

### 4.1 Entities

**User**

- Id
- Name
- Email
- PasswordHash
- CreatedAt

**Task (Aggregate Root)**

- Id
- UserId (multi-tenancy)
- Title
- Description
- Status (Pending, InProgress, Completed)
- DueDate
- CreatedAt
- UpdatedAt
- **Domain validation:** Title required (not null/empty, min and max length); Description optional with max length. Enforced in the entity constructor.
- **Status transitions:** Allowed: Pending ↔ InProgress, Pending → Completed, InProgress → Completed. Once a task is Completed, its status cannot be changed back. Status is changed via explicit methods: SetPending(), SetInProgress(), SetCompleted(). The frontend may show a warning that completing a task cannot be undone.

**NotificationLog** (Phase 1: entity only; Phase 2: full implementation)

- Id
- TaskId
- EventType
- ProcessedAt

---

## 5. Authentication

- Implement JWT manually; do **not** use ASP.NET Identity.
- Hash passwords (e.g. BCrypt).
- JWT must include UserId as claim (`sub`).
- All Task endpoints require authentication.
- Multi-tenancy: each user accesses only their own tasks (by UserId).
- UserId must always come from the JWT, never from request body or query.

---

## 6. Functional Requirements

### User

1. Register user
2. Login (returns JWT)
3. Get authenticated user profile

### Task

4. Create task
5. Get task by Id
6. List tasks (paginated, filtered, ordered)
7. Update task
8. Update task status (PATCH)
9. Delete task

---

## 7. Non-Functional Requirements

- JWT required for all task endpoints.
- Multi-tenancy enforced in the application layer.
- **Pagination:** PageNumber, PageSize (max **20**).
- **Filtering:** title contains, description contains, status.
- **Ordering:** DueDate ASC/DESC.
- Global exception-handling middleware.
- Clear separation of concerns and logging.
- Appropriate HTTP status codes.

---

## 8. Database (MongoDB)

- Separate collections: Users, Tasks, NotificationLogs.
- Indexing:
  - Index on UserId
  - Compound index (UserId + DueDate)
- Use the Repository pattern for data access.

---

## 9. Testing

- Use **xUnit**.
- Cover domain rules and application use cases; mock external dependencies.
- Do not rely on infrastructure in unit tests.
- **Phase 1:** Start with Domain and Application tests; coverage can grow progressively (no need to cover every feature immediately).

---

## 10. Containerization

- **Phase 1:** Dockerfile for the API and docker-compose with API and MongoDB; health checks for Docker.
- **Later:** docker-compose can add RabbitMQ and Redis (Phase 2).

---

## 11. Phased Scope

### Phase 1 — MVP

- Clean Architecture (Domain, Application, Infrastructure, API, Tests).
- User: register, login (JWT), get profile.
- Task: full CRUD, paginated list (PageSize max 20), filters (title, description, status), ordering by DueDate.
- Stack: .NET 10, MediatR (CQRS), FluentValidation, JWT (no Identity), BCrypt, MongoDB.
- Docker: Dockerfile + docker-compose (API + MongoDB); health checks.
- NotificationLog: **entity only** in Domain; no repository or endpoints.
- Tests: xUnit for Domain and Application (mocks); progressive coverage.
- Quality: SonarLint, global exception middleware, logging, multi-tenancy by UserId from JWT.

### Phase 2 — Cache and Messaging

- **Redis:** Cache Aside for GET task by id.
- **RabbitMQ:** Events (e.g. TaskCreated, TaskCompleted).
- **NotificationLog:** Repository, persistence, and use in event processing.

### Phase 3 — CI/CD

- **GitHub Actions:** CI/CD pipeline.
- Build and publish Docker image.

---

## 12. Risks and Considerations

- Define MongoDB repositories and indexes (UserId, UserId + DueDate) early in Phase 1.
- JWT is manual; if refresh or revocation is added later, extend the design without breaking existing contracts.

---

## 13. Document History

- Initial specification based on TaskFlow project plan and approved scope refinements (phases, PageSize 20, health checks, validation in Application layer).
- Added MediatR, CQRS (Commands/Queries), and FluentValidation to architecture and Phase 1 stack.
