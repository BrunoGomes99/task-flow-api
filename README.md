# TaskFlow API

A RESTful task-management API built with **.NET 10** and **Clean Architecture**. Multi-tenant by design (JWT + user-scoped tasks), with MongoDB, optional Redis cache and RabbitMQ messaging, and Docker support. Built for learning modern backend architecture and best practices.

## Features

- **User** — Register, login (JWT), get profile
- **Tasks** — Full CRUD, paginated list (filters: title, description, status; sort by due date)
- **Auth** — JWT (no ASP.NET Identity), BCrypt password hashing, multi-tenancy via `UserId` from token
- **Planned** — Redis cache (Cache Aside), RabbitMQ events, GitHub Actions CI/CD

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
dotnet test
```

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
  TaskFlow.Infrastructure/   # MongoDB, JWT, (Redis, RabbitMQ in Phase 2)
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
