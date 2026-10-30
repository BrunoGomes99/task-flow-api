# Phase 4 Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add correlated JSON logs, liveness versus readiness, reminder and cache metrics, and an opt-in OpenTelemetry export to Grafana Cloud, without putting any collector on the EC2 host.

**Architecture:** Domain stays unaware of telemetry. Application owns reminder summaries, delivery reasons, and reminder meters. Infrastructure owns health checks, the cache counter, AMQP `traceparent`, and OpenTelemetry registration. The three hosts only compose those pieces. Grafana Cloud is the backend; the process records signals with `ILogger`, `System.Diagnostics.Metrics`, and the OpenTelemetry SDK. Design: [../specs/2026-10-27-phase4-observability-design.md](../specs/2026-10-27-phase4-observability-design.md).

**Tech Stack:** .NET 10, `Microsoft.Extensions.Logging` JSON console, `Microsoft.Extensions.Diagnostics.HealthChecks`, `System.Diagnostics.Metrics`, OpenTelemetry SDK with the OTLP exporter, xUnit.

## Global Constraints

- Do not change due-reminder ack, requeue, or dead-letter decisions. New reason strings describe the decision that already exists.
- Do not change `ITaskCache`. A miss and a Redis error both return null.
- Do not log passwords, password hashes, JWTs, `Authorization`, the Brevo API key, OTLP headers, email addresses, email bodies, or query strings.
- `TaskId` and `UserId` may appear on logs and span attributes. They are not metric tags.
- `GET /health` and `GET /health/live` are the same liveness response, body `{ "status": "healthy" }`, HTTP 200, with no dependency calls. Compose, CI, and the SSM curl keep `GET /health` in this phase. Retargeting those callers to `GET /health/live` and removing `GET /health` is an ECS-migration edit, not a Phase 4 task.
- API readiness: MongoDB failure is Unhealthy (503); Redis failure is Degraded (HTTP 200). Workers check MongoDB and RabbitMQ only. Brevo is not a check.
- `OpenTelemetry:Enabled` defaults to false. The EC2 Compose file and Terraform do not gain a collector, an agent, or OTLP settings.
- Do not add Grafana, Alloy, Loki, Tempo, Mimir, or Prometheus containers.
- Metric tags are only the closed sets in the design (D7).
- Unit tests do not open MongoDB, Redis, RabbitMQ, Grafana, or the network for these signals.

## File map

| File | Responsibility |
|---|---|
| `src/TaskFlow.Api/Http/TelemetryTrace.cs` | One `traceId` string for `ProblemDetails`, the request log, and the exception log |
| `src/TaskFlow.Api/Middleware/RequestLoggingMiddleware.cs` | One Information line per API request, skipping health routes |
| `src/TaskFlow.Infrastructure/Health/*` | MongoDB, Redis, and RabbitMQ probes and the status rules |
| `src/TaskFlow.Application/UseCases/Reminders/DueSoonPublishSummary.cs` | Published and skipped counts for one finished scan |
| `src/TaskFlow.Application/UseCases/Reminders/DueSoonDeliveryResult.cs` | Broker outcome plus a stable reason string |
| `src/TaskFlow.Application/UseCases/Reminders/DueSoonDeliveryReasons.cs` | The six reason constants |
| `src/TaskFlow.Application/Observability/ReminderMetrics.cs` | Published counter, delivery counter, scan histogram |
| `src/TaskFlow.Infrastructure/Caching/CacheMetrics.cs` | Cache lookup counter |
| `src/TaskFlow.Infrastructure/Observability/OpenTelemetryRegistration.cs` | Opt-in SDK and OTLP exporter |
| `src/TaskFlow.Application/Behaviors/TracingBehavior.cs` | One span per MediatR request, wave 3 |

---

### Task 1: Correlate API logs

**Files:**

- Create: `src/TaskFlow.Api/Http/TelemetryTrace.cs`
- Create: `src/TaskFlow.Api/Middleware/RequestLoggingMiddleware.cs`
- Modify: `src/TaskFlow.Api/Http/ProblemDetailsExtensions.cs`
- Modify: `src/TaskFlow.Api/Middleware/GlobalExceptionHandler.cs`
- Modify: `src/TaskFlow.Api/Program.cs`
- Modify: `src/TaskFlow.Scheduler/Program.cs`
- Modify: `src/TaskFlow.Notifications/Program.cs`

**Interfaces:**

- Produces: `TelemetryTrace.Current(HttpContext httpContext) -> string`
- Produces: request log template `HTTP {Method} {Route} responded {StatusCode} in {ElapsedMilliseconds} ms` with scope property `TraceId`

There is no API test project. Do not create one for this helper. `TelemetryTrace` stays `internal` to `TaskFlow.Api`.

- [x] **Step 1: Add the helper and use it everywhere the id is written**

`TelemetryTrace.Current` returns `Activity.Current.TraceId` when that id is not null and not all zeros. Otherwise it returns `httpContext.TraceIdentifier`.

`ProblemDetailsExtensions.ApplyTraceId` stores that string in `extensions.traceId`. `GlobalExceptionHandler` calls `ILogger.LogError` with the exception only in the default (unexpected) branch, and includes `{TraceId}` from the same helper. Validation, argument, and unauthorized branches do not log at Error.

- [x] **Step 2: Log one line per request**

`RequestLoggingMiddleware` starts a scope whose `TraceId` is `TelemetryTrace.Current`. After `next`, it logs the template above at Information. `Route` is the route pattern raw text, or `HttpContext.Request.Path` when the endpoint has no pattern. Do not append the query string. Skip the request when the path is `/health`, `/health/live`, or `/health/ready`. Register the middleware in `Program.cs` before `UseExceptionHandler` so a handled exception still has a status code when the line is written. Place it so authentication failures are also logged.

- [x] **Step 3: Turn on the JSON console in all three hosts**

In each `Program.cs`:

```csharp
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
```

Leave the existing log levels (`Default` Information, `Microsoft.AspNetCore` Warning). Do not log configuration secrets while doing this.

- [x] **Step 4: Build**

Run: `dotnet build TaskFlow.slnx`

Expected: build succeeds. `GET /health` is still the controller that returns `{ "status": "healthy" }`.

### Task 2: Liveness and readiness

**Files:**

- Create: `src/TaskFlow.Infrastructure/Health/TaskFlowHealthRole.cs`
- Create: `src/TaskFlow.Infrastructure/Health/DependencyHealth.cs`
- Create: `src/TaskFlow.Infrastructure/Health/MongoDbHealthCheck.cs`
- Create: `src/TaskFlow.Infrastructure/Health/RedisHealthCheck.cs`
- Create: `src/TaskFlow.Infrastructure/Health/RabbitMqHealthCheck.cs`
- Modify: `src/TaskFlow.Infrastructure/Extensions/ServiceCollectionExtensions.cs` or a new `HealthCheckServiceCollectionExtensions.cs` in that folder
- Modify: `src/TaskFlow.Api/Controllers/HealthController.cs`
- Modify: `src/TaskFlow.Api/Program.cs`
- Modify: `src/TaskFlow.Scheduler/Program.cs`
- Modify: `src/TaskFlow.Notifications/Program.cs`
- Test: `tests/TaskFlow.Infrastructure.Tests/Health/DependencyHealthTests.cs`

**Interfaces:**

- Produces: `enum TaskFlowHealthRole { Api, Scheduler, Notifications }`
- Produces: `AddTaskFlowHealthChecks(this IServiceCollection services, TaskFlowHealthRole role)`
- Produces: `DependencyHealth.Redis(Exception? error) -> HealthCheckResult` returns Healthy when `error` is null and Degraded otherwise
- Produces: `DependencyHealth.Required(Exception? error, string name) -> HealthCheckResult` returns Healthy when `error` is null and Unhealthy otherwise

- [x] **Step 1: Write failing tests for the status rules**

`DependencyHealthTests` asserts Redis plus an exception is `HealthStatus.Degraded`, Redis plus null is `Healthy`, and `Required` plus an exception is `Unhealthy`. No Docker.

Run: `dotnet test tests/TaskFlow.Infrastructure.Tests/TaskFlow.Infrastructure.Tests.csproj --filter DependencyHealthTests`

Expected: FAIL because the type does not exist.

- [x] **Step 2: Implement the probes**

`MongoDbHealthCheck` pings with `IMongoClient` (`ping: 1`) and maps failures through `DependencyHealth.Required`. `RedisHealthCheck` pings `IConnectionMultiplexer` and maps failures through `DependencyHealth.Redis`. `RabbitMqHealthCheck` opens and closes a short-lived connection from `RabbitMqSettings` and maps failures through `DependencyHealth.Required`. It does not reuse the consumer channel.

`AddTaskFlowHealthChecks` always adds the MongoDB check. `Api` also adds Redis. `Scheduler` and `Notifications` also add RabbitMQ. The API host calls it with `Api`. Each worker calls it with its own role.

- [x] **Step 3: Map the routes without changing liveness**

On the API, `HealthController` gains `GET /health/live` with the same body as `GET /health`. `Program.cs` maps `GET /health/ready` with `MapHealthChecks`. On the workers, keep `GET /health` as the anonymous `{ status: "healthy" }` result, add the same body on `GET /health/live`, and map `GET /health/ready`.

Do not point Compose healthchecks, `.github/workflows/ci.yml`, or the SSM redeploy curl at `/health/ready` or at `/health/live`. Those callers stay on `GET /health` until the ECS migration, which moves them to `GET /health/live` and removes `GET /health`.

- [x] **Step 4: Re-run the filter and build**

Expected: `DependencyHealthTests` pass and `dotnet build TaskFlow.slnx` succeeds.

### Task 3: Reminder outcomes in logs

**Files:**

- Create: `src/TaskFlow.Application/UseCases/Reminders/DueSoonPublishSummary.cs`
- Create: `src/TaskFlow.Application/UseCases/Reminders/DueSoonDeliveryResult.cs`
- Create: `src/TaskFlow.Application/UseCases/Reminders/DueSoonDeliveryReasons.cs`
- Modify: `src/TaskFlow.Application/UseCases/Reminders/PublishDueSoonReminders/DueSoonReminderPublish.cs`
- Modify: `src/TaskFlow.Application/UseCases/Reminders/DeliverDueSoonReminder/DueSoonReminderDelivery.cs`
- Modify: `src/TaskFlow.Scheduler/DueSoonScanHostedService.cs`
- Modify: `src/TaskFlow.Infrastructure/Messaging/DueSoonConsumer.cs`
- Modify: `tests/TaskFlow.Application.Tests/Reminders/DueSoonReminderTests.cs`
- Test: extend `DueSoonReminderTests` rather than adding a second fixture

**Interfaces:**

- Produces: `readonly record struct DueSoonPublishSummary(int Published, int Skipped)`
- Produces: `PublishAsync(...) -> Task<DueSoonPublishSummary>`
- Produces: `readonly record struct DueSoonDeliveryResult(DueSoonDeliveryOutcome Outcome, string Reason)`
- Produces: `DeliverAsync(...) -> Task<DueSoonDeliveryResult>`
- Produces: `DueSoonDeliveryReasons` constants `already_sent`, `skipped`, `sent`, `requeue`, `dead_letter`, `discarded`
- Consumes: existing `DueSoonDeliveryOutcome.Acknowledge` and `Requeue`

- [ ] **Step 1: Update the existing reminder tests so they fail to compile against the new results**

Assertions to lock:

- A candidate with no receipt increments `Published` and does not increment `Skipped`.
- A candidate that already has a receipt increments `Skipped` and does not publish.
- An existing receipt returns `Acknowledge` and `already_sent`, and does not send mail.
- Missing, completed, or moved due date returns `Acknowledge` and `skipped`, and does not send mail.
- A successful send returns `Acknowledge` and `sent`.
- A missing user or a refused send returns `Requeue` and `requeue`.

Run: `dotnet test tests/TaskFlow.Application.Tests/TaskFlow.Application.Tests.csproj --filter DueSoonReminderTests`

Expected: FAIL to compile or fail assertions.

- [ ] **Step 2: Return the summary and the result without changing broker decisions**

`PublishAsync` counts a skipped receipt before `continue`, and a published message after `PublishAsync` on the publisher returns. An exception from the publisher still propagates.

`DeliverAsync` maps today's branches onto the reasons above. `CanRemind` false is `skipped`. Do not send mail on `already_sent` or `skipped`.

- [ ] **Step 3: Log the summary and the reason**

`DueSoonScanHostedService` logs, at Information, when the scan returns:

`DueSoon scan published {Published} reminders and skipped {Skipped} in {ElapsedMilliseconds} ms`

The existing Error log on a thrown scan stays.

`DueSoonReminderDelivery` logs one Information line with `{TaskId}`, `{DueDate}`, and `{Result}` equal to the reason. Do not log the email address.

`DueSoonConsumer` reads `result.Outcome` the same way it read the old enum. Unreadable body and empty identity stay Error logs. They do not get a new broker path in this task.

- [ ] **Step 4: Re-run `DueSoonReminderTests` and the solution build**

Expected: pass.

### Task 4: Metrics

**Files:**

- Create: `src/TaskFlow.Application/Observability/ReminderMetrics.cs`
- Create: `src/TaskFlow.Infrastructure/Caching/CacheMetrics.cs`
- Modify: `src/TaskFlow.Infrastructure/Caching/RedisTaskCache.cs`
- Modify: `src/TaskFlow.Scheduler/DueSoonScanHostedService.cs`
- Modify: `src/TaskFlow.Application/UseCases/Reminders/DeliverDueSoonReminder/DueSoonReminderDelivery.cs`
- Modify: `src/TaskFlow.Infrastructure/Messaging/DueSoonConsumer.cs`
- Modify: `src/TaskFlow.Infrastructure/Extensions/DueReminderServiceCollectionExtensions.cs`
- Modify: `src/TaskFlow.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- Test: `tests/TaskFlow.Application.Tests/Observability/ReminderMetricsTests.cs`
- Test: `tests/TaskFlow.Infrastructure.Tests/Caching/CacheMetricsTests.cs`
- Modify: `tests/TaskFlow.Infrastructure.Tests/Caching/RedisTaskCacheTests.cs` only if the constructor gains `CacheMetrics`

**Interfaces:**

- Produces: `ReminderMetrics` with `MeterName = "TaskFlow.Reminders"`
- Produces: `RecordPublished(int count)`, `RecordDelivery(string reason)`, `RecordScan(TimeSpan elapsed, bool succeeded)`
- Produces: instruments `taskflow.reminders.published` (counter), `taskflow.reminders.delivery` (counter, tag `result`), `taskflow.reminders.scan.duration` (histogram, unit seconds, tag `result` of `success` or `failure`)
- Produces: `CacheMetrics` with `MeterName = "TaskFlow.Cache"` and counter `taskflow.cache.lookups`, tag `result` of `hit`, `miss`, or `error`
- Consumes: `DueSoonDeliveryReasons`

- [ ] **Step 1: Write failing meter tests**

Use `MeterListener` on the meter name. Assert:

- `RecordPublished(2)` adds 2 to `taskflow.reminders.published` and that instrument has no tags.
- `RecordDelivery(DueSoonDeliveryReasons.Sent)` adds 1 with tag `result=sent`.
- `RecordScan` records the elapsed seconds with `result=success` or `result=failure`.
- A cache get that returns JSON counts `hit`. An empty value counts `miss`. A store exception counts `error` and still returns null.

Run the two new filters. Expected: FAIL because the types do not exist.

- [ ] **Step 2: Implement the meters and call them**

Register `ReminderMetrics` as a singleton from both `AddDueSoonScheduler` and `AddDueSoonNotifications`. Register `CacheMetrics` as a singleton from `AddInfrastructure`.

`RedisTaskCache.GetAsync` records `hit`, `miss`, or `error` at the three branches it already has. Keep the existing `LogWarning` on a read failure. Do not add an Information log per hit or miss. `SetAsync` and `RemoveAsync` do not use this counter. Leave the three-attempt invalidation warning in `TaskCacheInvalidator` as it is.

The hosted service calls `RecordPublished(summary.Published)` and `RecordScan(..., succeeded: true)` after a finished scan, and `RecordScan(..., succeeded: false)` in the existing catch. `DueSoonReminderDelivery` calls `RecordDelivery` with its reason. The consumer calls `RecordDelivery(discarded)` when it nacks an unreadable or empty message, and `RecordDelivery(dead_letter)` on the existing dead-letter branch.

- [ ] **Step 3: Re-run the new filters, `DueSoonReminderTests`, and `RedisTaskCacheTests`**

Expected: pass. Do not add an OTLP exporter in this task.

### Task 5: Opt-in export to Grafana Cloud

**Files:**

- Create: `src/TaskFlow.Infrastructure/Configuration/OpenTelemetrySettings.cs`
- Create: `src/TaskFlow.Infrastructure/Observability/OpenTelemetryRegistration.cs`
- Create: `src/TaskFlow.Application/Behaviors/TracingBehavior.cs`
- Modify: `src/TaskFlow.Application/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/TaskFlow.Infrastructure/Messaging/DueSoonPublisher.cs`
- Modify: `src/TaskFlow.Infrastructure/Messaging/DueSoonConsumer.cs`
- Modify: `src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj`
- Modify: `src/TaskFlow.Application/TaskFlow.Application.csproj` only if the span behavior needs `OpenTelemetry.Api`
- Modify: `src/TaskFlow.Api/Program.cs`
- Modify: `src/TaskFlow.Scheduler/Program.cs`
- Modify: `src/TaskFlow.Notifications/Program.cs`
- Modify: `src/TaskFlow.Api/appsettings.json`
- Modify: `src/TaskFlow.Scheduler/appsettings.json`
- Modify: `src/TaskFlow.Notifications/appsettings.json`
- Modify: `docker-compose.yml`
- Modify: `.env.example`
- Test: `tests/TaskFlow.Infrastructure.Tests/Configuration/OpenTelemetryConfigurationTests.cs`
- Do not modify: `infra/compose/docker-compose.aws.yml`, `infra/**/*.tf`

**Interfaces:**

- Produces: `OpenTelemetrySettings` section name `OpenTelemetry`, properties `Enabled`, `OtlpEndpoint`, `Headers`, `ServiceName`, `Environment`
- Produces: `AddTaskFlowOpenTelemetry(this IServiceCollection services, IConfiguration configuration)`
- Consumes: `TelemetryTrace.Current`, which already prefers `Activity.Current.TraceId`

- [ ] **Step 1: Write failing configuration tests**

`Enabled=false` does not register an OTLP exporter (resolve the service collection and assert no `TracerProvider` / exporter registration; do not open a socket). `Enabled=true` with an empty `OtlpEndpoint` throws `InvalidOperationException` at registration. `Enabled=true` with an endpoint registers the SDK. Assert `Headers` is not written by any log in this test.

Run: `dotnet test tests/TaskFlow.Infrastructure.Tests/TaskFlow.Infrastructure.Tests.csproj --filter OpenTelemetryConfigurationTests`

Expected: FAIL because the type does not exist.

- [ ] **Step 2: Register the SDK only when enabled**

Pin the latest stable versions of these packages in `TaskFlow.Infrastructure.csproj`:

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`

Do not add `OpenTelemetry.Instrumentation.StackExchangeRedis`. It is still beta. Redis stays on the `taskflow.cache.lookups` counter from Task 4.

Subscribe to the MongoDB driver's built-in source with `AddSource(MongoTelemetry.ActivitySourceName)` (`"MongoDB.Driver"`). `MongoDB.Driver` 3.7.1 already emits those activities. Do not set `TracingOptions.QueryTextMaxLength` on the client.

`AddTaskFlowOpenTelemetry` returns immediately when `Enabled` is false. When it is true and `OtlpEndpoint` is empty, throw. Otherwise configure a resource with `service.name` from `ServiceName`, `deployment.environment` from `Environment` (default `local`), and `service.version` from the environment variable `SERVICE_VERSION` only when that variable is non-empty. Export traces, metrics, and logs over OTLP to `OtlpEndpoint` with `Headers`.

Default `appsettings.json` on each host:

```json
"OpenTelemetry": {
  "Enabled": false,
  "OtlpEndpoint": "",
  "Headers": "",
  "ServiceName": "taskflow-api",
  "Environment": "local"
}
```

Use `taskflow-scheduler` and `taskflow-notifications` on those hosts. Each `Program.cs` calls `AddTaskFlowOpenTelemetry`.

- [ ] **Step 3: Add the spans that the automatic instrumentation does not cover**

`TracingBehavior<TRequest, TResponse>` starts a span named `typeof(TRequest).Name` and is registered next to `ValidationBehavior` in `AddApplicationValidation`. It does not log.

`DueSoonPublisher` injects W3C `traceparent` into the AMQP headers of `BasicProperties` before publish. `DueSoonConsumer` extracts that header and handles the message inside that activity. A message without the header is still processed.

- [ ] **Step 4: Document the local switch and leave AWS untouched**

`.env.example` gains empty values:

```
OTEL_ENABLED=false
OTEL_EXPORTER_OTLP_ENDPOINT=
OTEL_EXPORTER_OTLP_HEADERS=
```

`docker-compose.yml` passes `OpenTelemetry__Enabled`, `OpenTelemetry__OtlpEndpoint`, and `OpenTelemetry__Headers` for `taskflow.api`, `taskflow.scheduler`, and `taskflow.notifications`, defaulting enabled to `false`. Do not add a Grafana or collector service.

Confirm `infra/compose/docker-compose.aws.yml` has no `OpenTelemetry` keys and no new service.

- [ ] **Step 5: Re-run the new filter and `dotnet test TaskFlow.slnx`**

Expected: pass. With the default configuration, no test and no host opens a connection to Grafana.

### Task 6: Mark the guidelines as the waves land

**Files:**

- Modify: `docs/ENGINEERING_GUIDELINES.md` checkboxes in the Phase 4 section

- [ ] **Step 1: Check a box only when the task that implements it is done**

Task 1 checks the JSON log and request-correlation boxes. Task 2 checks health. Task 3 checks reminder logs. Task 4 checks metrics. Task 5 checks export and the EC2 exclusion. The cross-phase Logging box is checked when Task 1 is done.

Do not check the ECS bullet. ECS is a later migration. That migration retargets liveness callers from `GET /health` to `GET /health/live` and then removes `GET /health`. Phase 4 only adds the `/health/live` alias.
