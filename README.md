# TaskFlow API

A RESTful task-management API built with **.NET 10** and **Clean Architecture**. Multi-tenant by design (JWT + user-scoped tasks), with MongoDB, optional Redis cache and RabbitMQ messaging, and Docker support. Built for learning modern backend architecture and best practices.

## Features

- **User** — Register, login (JWT), get profile
- **Tasks** — Full CRUD, paginated list (filters: title, description, status; sort by due date)
- **Auth** — JWT (no ASP.NET Identity), BCrypt password hashing, multi-tenancy via `UserId` from token
- **Phase 2 (in progress)** — GitHub Actions CI + ECR publish behind Environment `production`; Terraform AWS study stack (ECR + EC2 + Docker Compose)
- **Planned (Phase 3)** — Redis cache (Cache Aside), RabbitMQ events, NotificationLog persistence

## Tech Stack

| Layer        | Technologies |
|-------------|--------------|
| Runtime     | .NET 10      |
| API         | ASP.NET Core Web API |
| Use cases   | MediatR, CQRS (Command and Query Responsibility Segregation) |
| Validation  | FluentValidation (Application layer, pipeline behavior) |
| Persistence | MongoDB      |
| Auth        | JWT, BCrypt  |
| Testing     | xUnit        |
| Quality     | SonarLint    |
| Containers  | Docker, docker-compose |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started) (optional, for running with MongoDB)

## Getting Started

### Clone and build

```bash
git clone https://github.com/your-org/task-flow-api.git
cd task-flow-api
dotnet restore
dotnet build
```

### Run locally

Set MongoDB connection string and JWT settings (e.g. in `appsettings.Development.json` or environment variables), then:

```bash
dotnet run --project src/TaskFlow.Api
```

The API will listen on the configured port (e.g. `http://localhost:5000`).

### Run with Docker

```bash
docker-compose up -d
```

Starts the API and MongoDB. Health check: `GET /health`.

### Run tests

```bash
dotnet test TaskFlow.slnx
```

Infrastructure tests use [Testcontainers](https://dotnet.testcontainers.org/) (`mongo:8.0`) and require **Docker** running locally (same requirement as GitHub Actions runners).

### Continuous Integration / Delivery

GitHub Actions ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) runs a **single pipeline**:

1. **CI** — on pushes to `main` / `dev` and on pull requests to `main`: restore, build, test.
2. **Publish to ECR** — only after CI succeeds on push to `main` (or `workflow_dispatch`), paused on the GitHub Environment **`production`** until someone clicks Approve. Uses OIDC (no long-lived AWS keys in YAML). Tags: `github.sha` and `latest`.
3. **Redeploy on EC2 (temporary)** — SSM Run Command pins Compose to `github.sha` and runs `docker compose pull/up`. Replaced later by ECS `update-service` (see [infra/README.md](infra/README.md)).

Local equivalent of the CI job:

```bash
dotnet restore TaskFlow.slnx
dotnet build TaskFlow.slnx --no-restore -c Release
dotnet test TaskFlow.slnx --no-build -c Release
```

AWS apply/destroy, OIDC variables, and EC2 notes: [infra/README.md](infra/README.md). Cache/Messaging remains **Phase 3**.

## Project Structure

Clean Architecture layout with CQRS and MediatR:

```
src/
  TaskFlow.Domain/           # Entities, domain rules — no external dependencies
  TaskFlow.Application/      # Use cases (MediatR handlers), interfaces, DTOs, validation
    DTOs/Common/             # Shared DTOs
    DTOs/                    # Response DTOs (e.g. TaskDto.cs)
    UseCases/Tasks/          # Per use-case folder: command/query, validator, handler
      CreateTask/
      UpdateTask/
      ListTasks/
      ...
    Interfaces/              # ITaskRepository, IUserRepository, etc.
    Behaviors/               # ValidationBehavior (FluentValidation pipeline)
    Extensions/              # AddApplicationValidation, DI registration
  TaskFlow.Infrastructure/   # MongoDB, JWT, (Redis, RabbitMQ in Phase 3)
  TaskFlow.Api/              # Controllers, middleware, configuration
tests/
  TaskFlow.Domain.Tests/
  TaskFlow.Application.Tests/
```

- **Domain** → no references to other projects  
- **Application** → references Domain only; Commands/Queries (CQRS), FluentValidation, MediatR handlers  
- **Infrastructure** → implements Application interfaces  
- **API** → references Application and Infrastructure; thin controllers send requests via `IMediator`  

## Documentation

- [Project Specification](docs/PROJECT_SPEC.md) — Scope, domain, auth, API contract, phased plan  
- [Engineering Guidelines](docs/ENGINEERING_GUIDELINES.md) — Implementation standards and checklists per phase  
- [CI/CD + EC2 design](docs/superpowers/specs/2026-08-02-ci-cd-ec2-design.md) — Approved Phase 2 design (CI first; ECR + EC2 CD scaffold)  
- [CI/CD implementation plan](docs/superpowers/plans/2026-08-02-ci-cd-implementation.md) — Task breakdown for branch `feature/ci-cd-implementation`  
- [Infrastructure (Terraform)](infra/README.md) — Auth, S3 state, apply/destroy, ECR publish gate, ECS evolution notes  

