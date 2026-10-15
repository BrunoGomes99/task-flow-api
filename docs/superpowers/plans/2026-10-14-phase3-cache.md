# Phase 3 Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Cache Aside for `GetTaskById` on Redis, with a 60-second absolute TTL and three delete attempts after a successful task update or delete.

**Architecture:** MongoDB stays the source of truth. Application owns `ITaskCache` and the retry policy (`TaskCacheInvalidator`). Infrastructure talks to Redis and turns a read or write error into a miss or a failed delete. Update, status change, and delete drop the key only after the MongoDB write succeeds. A failed delete is logged and does not change the HTTP result.

**Tech Stack:** .NET 10, MediatR, xUnit, StackExchange.Redis, Docker Compose Redis 7.4 (no published port).

## Global Constraints

- Cache Aside on `GetTaskById` only. Do not cache list, create, or other reads.
- Cache key contains UserId and TaskId.
- Absolute TTL of 60 seconds. Reading the key must not extend it. No sliding expiration.
- Update and delete persist MongoDB first, then delete the key, up to 3 attempts, with a short pause between them.
- After three failed deletes, log UserId and TaskId, keep the MongoDB result, and do not return HTTP 500 for the cache failure.
- A Redis error on GET is a cache miss. `GetTaskById` still reads MongoDB and responds.
- A cache outage must not fail `GetTaskById`, including a failed cache write after a MongoDB read.
- Do not roll back a MongoDB write when Redis is unavailable.
- Do not add an outbox, retry queue, or RabbitMQ message to repair the cache.
- Do not add versions or locks for the known get/set race. The 60 second TTL bounds it.
- Redis runs in local `docker-compose.yml` and `infra/compose/docker-compose.aws.yml`.
- Do not publish port `6379`.
- EC2 Redis uses `maxmemory` of 64mb and `allkeys-lru`. Persistence is off.
- Do not enlarge the EC2 instance. The study host stays `t3.micro`.
- Unit tests mock the cache. No real Redis in unit tests.
- UserId still comes from the JWT only. Another user's task stays a 404.
- Messaging (`TaskCreated`, `TaskCompleted`, NotificationLog) is out of scope.

---

### Task 1: Cache Aside on GetTaskById

**Files:**
- Create: `src/TaskFlow.Application/Interfaces/ITaskCache.cs`
- Modify: `src/TaskFlow.Application/UseCases/Tasks/GetTaskById/GetTaskByIdQueryHandler.cs`
- Modify: `tests/TaskFlow.Application.Tests/UseCases/TaskUseCasesTests.cs`
- Test: `tests/TaskFlow.Application.Tests/UseCases/TaskCacheAsideTests.cs`

**Interfaces:**
- Produces: `ITaskCache.GetAsync(Guid userId, Guid taskId, CancellationToken) -> Task<TaskDto?>`
- Produces: `ITaskCache.SetAsync(Guid userId, Guid taskId, TaskDto task, CancellationToken) -> Task`
- Produces: `ITaskCache.RemoveAsync(Guid userId, Guid taskId, CancellationToken) -> Task<bool>`

- [x] **Step 1: Write failing tests** for hit (repository not called, key is UserId + TaskId), miss (load Mongo and store the DTO), not found (do not store), and a null get (miss) that still returns the Mongo task when set throws.

- [x] **Step 2: Run** `dotnet test tests/TaskFlow.Application.Tests/TaskFlow.Application.Tests.csproj --filter TaskCacheAsideTests` and confirm the new tests fail because the cache type or behavior is missing.

- [x] **Step 3: Implement** `ITaskCache` and the handler. On a hit, return the cached DTO. On a miss, load the repository. Store only when the task exists. Do not cache a not-found result.

- [x] **Step 4: Re-run the filter and confirm pass.**

### Task 2: Invalidate after a successful write

**Files:**
- Create: `src/TaskFlow.Application/Interfaces/ITaskCacheInvalidator.cs`
- Create: `src/TaskFlow.Application/Caching/TaskCacheInvalidator.cs`
- Modify: `src/TaskFlow.Application/TaskFlow.Application.csproj` (logging abstractions)
- Modify: `src/TaskFlow.Application/Extensions/ServiceCollectionExtensions.cs`
- Modify: update, update-status, and delete handlers
- Test: `tests/TaskFlow.Application.Tests/Caching/TaskCacheInvalidatorTests.cs`
- Test: `tests/TaskFlow.Application.Tests/UseCases/TaskCacheInvalidationTests.cs`

**Interfaces:**
- Consumes: `ITaskCache.RemoveAsync`
- Produces: `ITaskCacheInvalidator.InvalidateAsync(Guid userId, Guid taskId, CancellationToken) -> Task`
- Produces: `TaskCacheInvalidator` with `MaxAttempts = 3` and a 50ms pause between attempts

- [x] **Step 1: Write failing tests.** Three failed removes still complete `InvalidateAsync` and log both ids. The second attempt can succeed. Update, status change, and delete call invalidate only after a successful Mongo write. Not-found and invalid status transition do not.

- [x] **Step 2: Run the new filters and confirm failure.**

- [x] **Step 3: Implement the invalidator and call it from the three handlers after the write.** Do not throw when every delete fails.

- [x] **Step 4: Re-run and confirm pass.** Register `ITaskCacheInvalidator` in the Application DI extension.

### Task 3: Redis implementation

**Files:**
- Create: `src/TaskFlow.Infrastructure/Configuration/RedisSettings.cs`
- Create: `src/TaskFlow.Infrastructure/Caching/IRedisStringCache.cs`
- Create: `src/TaskFlow.Infrastructure/Caching/StackExchangeRedisStringCache.cs`
- Create: `src/TaskFlow.Infrastructure/Caching/RedisTaskCache.cs`
- Modify: `src/TaskFlow.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj`
- Modify: `src/TaskFlow.Api/appsettings.json`
- Modify: `tests/TaskFlow.Infrastructure.Tests/Configuration/InfrastructureRegistrationTests.cs`
- Test: `tests/TaskFlow.Infrastructure.Tests/Caching/RedisTaskCacheTests.cs`
- Test: `tests/TaskFlow.Infrastructure.Tests/Configuration/RedisConfigurationTests.cs`

**Interfaces:**
- Consumes: `ITaskCache`
- Produces: key `task:{userId:D}:{taskId:D}`, absolute TTL `TimeSpan.FromSeconds(60)` passed only to set
- Produces: `Redis:ConnectionString` required at startup; `AbortOnConnectFail = false`

- [x] **Step 1: Write failing tests.** Round-trip stores that key and a 60 second TTL and a later get does not write again. Get/set/delete exceptions do not escape (`Get` returns null, `Set` completes, `Remove` returns false). Missing `Redis:ConnectionString` throws. DI registers `RedisTaskCache` without opening a connection.

- [x] **Step 2: Run the new infrastructure filters and confirm failure.**

- [x] **Step 3: Implement** the string-cache seam, JSON cache, StackExchange adapter (`StringSetAsync` expiry only, no expire-on-read), and DI.

- [x] **Step 4: Re-run and confirm pass.**

### Task 4: Compose and docs

**Files:**
- Modify: `docker-compose.yml`
- Modify: `infra/compose/docker-compose.aws.yml`
- Modify: `README.md`
- Modify: `infra/README.md`
- Modify: `docs/ENGINEERING_GUIDELINES.md`
- Modify: `docs/superpowers/specs/2026-10-14-phase3-cache-design.md`
- Modify: `docs/PROJECT_SPEC.md` history only if the implementation note needs a status line

- [x] **Step 1: Add Redis 7.4 Alpine to both Compose files.** No host port. `redis:6379,abortConnect=false` on the API. Healthcheck `redis-cli ping`. Disable RDB and AOF. AWS service also sets `--maxmemory 64mb` and `--maxmemory-policy allkeys-lru`. API `depends_on` Redis healthy.

- [x] **Step 2: Mark the Redis checklist items done and point the README at this plan.** Leave RabbitMQ and NotificationLog unchecked.

- [x] **Step 3: Run** `dotnet test TaskFlow.slnx` and confirm the suite passes.
