# TaskFlow — Phase 4 Observability Design

Date: 2026-10-27  
Status: **Approved**, not implemented. Plan: [../plans/2026-10-27-phase4-observability.md](../plans/2026-10-27-phase4-observability.md).

## Goal

Make the API, the scheduler, and the notifications worker explain themselves from the outside: a failed request can be found in the logs by the `traceId` returned to the client, dependency health is distinct from "the process is up", and the due-reminder path can be counted and, when a developer turns export on, followed as one trace in Grafana Cloud.

The application records telemetry with the .NET APIs (`Microsoft.Extensions.Logging`, `System.Diagnostics.Metrics`, and later the OpenTelemetry SDK). Grafana Cloud stores and displays it. Those are two stages of the same path.

## Non-goals

- Deploying any collector, agent, or observability backend on the current EC2 host, or adding one to `infra/compose/docker-compose.aws.yml`.
- Running Loki, Tempo, Mimir, Prometheus, Alloy, or Grafana in local Compose. The backends are Grafana Cloud.
- Changing ack, requeue, and dead-letter decisions of the due reminder. New result values describe the decision that already exists.
- Changing `ITaskCache`: a miss and a Redis error both still return null to `GetTaskById`.
- Caching, messaging, or API behavior changes beyond the log, metric, and health signals in this design.
- Datadog, or any vendor-specific tracer inside the application.
- ECS task definitions, sidecars, or Secrets Manager. The ECS shape is recorded below so the later migration does not invent a second telemetry design.
- Alert rules, paging, and dashboards as committed JSON. A developer builds those in the Grafana Cloud account after export works.
- Logging every cache hit, every MediatR use case, or HTTP bodies and headers.

## Decisions (Approved)

### D1 — Three waves, one pipeline

1. **Logs and health.** JSON console logs, a request log correlated with `extensions.traceId`, and real readiness checks. This wave is useful with `docker logs` and no external account.
2. **Metrics.** The same facts worth counting are recorded with `System.Diagnostics.Metrics`. `dotnet-counters` can read them in-process. Nothing is exported yet.
3. **Export.** The OpenTelemetry SDK listens to those meters and to traces, and sends OTLP to Grafana Cloud when export is enabled. The meters and log statements from waves 1 and 2 stay as they are.

### D2 — Vendor is Grafana Cloud

Grafana Cloud is the vendor. Checked 2026-10-27, its free tier includes 10 thousand active metric series, 50 GB of logs, and 50 GB of traces per month, with 14-day retention, and does not require a credit card.

Datadog was considered and rejected for this study path. Its APM is billed per host and, on Fargate, per concurrent task. Learning locally would put the agent on the developer machine. The TaskFlow process only speaks OpenTelemetry, so a later Datadog trial is an exporter change, not an application change.

### D3 — What each signal is for

| Signal | Question | Where it is recorded |
|---|---|---|
| Log | What happened to this request or this task? | `ILogger` message templates |
| Metric | How often, and how long? | `System.Diagnostics.Metrics` |
| Trace | Which process and which call spent the time? | OpenTelemetry spans, wave 3 |

`TaskId` and `UserId` belong on logs and on span attributes. They are not metric tags. Metric tags in this phase are only the closed sets in D7. A `TaskId` label would exhaust the 10 thousand series cap.

### D4 — Data that must not be logged

Logs, span attributes, and metric tags must not contain passwords, password hashes, JWTs, the `Authorization` header, the Brevo API key, the OTLP `Authorization` header, email addresses, or email bodies. Query strings are not logged. `UserId` and `TaskId` are allowed.

### D5 — `traceId` is one value in the response and in the log

`ProblemDetails.extensions.traceId` stays the correlation field defined by the [error-contract design](2026-04-17-result-error-contract-design.md). Phase 4 chooses its value in one helper used by the request log, the exception log, and `ApplyTraceId`:

- When `Activity.Current` has a real W3C trace id, that id is `traceId`.
- Otherwise `traceId` is `HttpContext.TraceIdentifier`.

Wave 1 hits the second branch, because nothing listens for activities yet. Wave 3 turns on ASP.NET Core instrumentation, so the id in the JSON error is the id to paste into Grafana. The helper does not add a second id to `ProblemDetails`.

The unexpected-exception branch of `GlobalExceptionHandler` logs the exception at Error with that same `TraceId`. Validation failures, bad arguments, and unauthorized results are already HTTP 400 or 401 and are not logged at Error.

Each API request logs one Information line: HTTP method, route template (falling back to the path only when there is no template), status code, elapsed milliseconds, and `TraceId`. The log skips `/health`, `/health/live`, and `/health/ready`. Headers and bodies are not logged.

### D6 — Liveness is `GET /health` until ECS

`GET /health` and `GET /health/live` are the same liveness check: the process is up, the body is `{ "status": "healthy" }`, and the status is HTTP 200. Neither calls MongoDB, Redis, or RabbitMQ. A dependency blip must not restart the container.

Phase 4 does not move existing callers. Docker Compose, the EC2 Compose file, the GitHub Actions health curl, and the SSM redeploy curl keep calling `GET /health`.

`GET /health/live` is the name those callers adopt when the ECS migration edits them. That retarget is part of D9, not of this phase. After the callers move, `GET /health` is removed so liveness has one URL.

`GET /health/ready` runs dependency checks and returns the ASP.NET Core health report.

| Host | Ready checks | Failure status |
|---|---|---|
| API | MongoDB ping | Unhealthy (HTTP 503) |
| API | Redis ping | **Degraded** (HTTP 200). A Redis outage is already a cache miss for `GetTaskById`. |
| Scheduler | MongoDB ping, RabbitMQ connection | Unhealthy (HTTP 503) |
| Notifications | MongoDB ping, RabbitMQ connection | Unhealthy (HTTP 503) |

Brevo is not a readiness check. An invalid key must not make Compose restart the notifications worker. Redis is not a readiness check on the workers. The API does not check RabbitMQ.

Health checks live in Infrastructure next to the clients they ping. They use `IMongoClient`, `IConnectionMultiplexer`, and `RabbitMqSettings`, which are already registered. Unit tests cover the status mapping with fakes. They do not start MongoDB, Redis, or RabbitMQ.

### D7 — Business signals

Cache hit, miss, and Redis error are already distinct inside `RedisTaskCache`. `GetTaskById` cannot see the difference, because a miss and an error both return null. The cache metric is recorded there, without changing `ITaskCache`.

| Instrument | Meter | Type | Tags |
|---|---|---|---|
| `taskflow.cache.lookups` | `TaskFlow.Cache` | Counter | `result`: `hit`, `miss`, `error` |
| `taskflow.reminders.published` | `TaskFlow.Reminders` | Counter | none |
| `taskflow.reminders.scan.duration` | `TaskFlow.Reminders` | Histogram (seconds) | `result`: `success`, `failure` |
| `taskflow.reminders.delivery` | `TaskFlow.Reminders` | Counter | `result`: `already_sent`, `skipped`, `sent`, `requeue`, `dead_letter`, `discarded` |

Per-request cache results are metrics only. The existing warning when a Redis read fails, and the existing warning when cache invalidation exhausts three attempts, stay. There is no Information log per hit or miss.

Reminder logs, all structured, and none of them include the recipient address:

- One Information line per completed scan: `{Published}`, `{Skipped}`, `{ElapsedMilliseconds}`. A scan that throws still logs the existing Error and records `result=failure` on the histogram.
- One Information line per delivery that finishes inside `DueSoonReminderDelivery`, with `{TaskId}`, `{DueDate}`, and `{Result}`.
- The existing Error logs for an unreadable body, an empty identity, a failed delivery, a dead letter, and a failed publish stay. The dead-letter and discarded paths increment `taskflow.reminders.delivery` with `dead_letter` or `discarded`.

`DueSoonReminderPublish.PublishAsync` returns `DueSoonPublishSummary(int Published, int Skipped)`. A publish exception still propagates. The summary counts only a scan that finishes. Skipped means a receipt already exists. That count is a log field, not its own metric series.

`DueSoonReminderDelivery.DeliverAsync` returns `DueSoonDeliveryResult(DueSoonDeliveryOutcome Outcome, string Reason)`. `Outcome` is still only `Acknowledge` or `Requeue`, and the consumer keeps today's ack, republish, and dead-letter rules. `Reason` is one of `already_sent`, `skipped`, `sent`, `requeue`. `skipped` covers the current `CanRemind` rejections (missing task, wrong user, completed, no due date, or a different due instant). A missing user or a refused email stays `requeue`.

### D8 — Export is opt-in OpenTelemetry

Wave 3 adds the OpenTelemetry SDK in Infrastructure, registered once and called from the three hosts. The application does not reference Grafana, Alloy, or a Datadog tracer.

Configuration section `OpenTelemetry`:

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `false` | When false, the SDK and the exporter are not registered. |
| `OtlpEndpoint` | empty | Grafana Cloud OTLP gateway. Required when `Enabled` is true; startup throws if it is missing, same style as `MongoDb:ConnectionString`. |
| `Headers` | empty | Exporter headers (Grafana Cloud basic auth). Never logged. |
| `ServiceName` | per host, below | Resource attribute `service.name`. |
| `Environment` | `local` | Resource attribute `deployment.environment`. |

Service names: `taskflow-api`, `taskflow-scheduler`, `taskflow-notifications`. `service.version` is set only when the environment variable `SERVICE_VERSION` is non-empty. Local runs leave it empty. The future ECS task sets it to `github.sha`.

Instrumentation in wave 3:

- ASP.NET Core requests, on the API (`OpenTelemetry.Instrumentation.AspNetCore`).
- `HttpClient`, which covers the Brevo call (`OpenTelemetry.Instrumentation.Http`).
- A MediatR pipeline behavior that opens one span named after the request type. It does not log the request.
- MongoDB commands by subscribing to the driver's built-in `ActivitySource` (`MongoDB.Driver`, `MongoTelemetry.ActivitySourceName`). `MongoDB.Driver` 3.7 already emits those spans. Do not set `TracingOptions.QueryTextMaxLength`. Command text can contain task titles and other user data.
- Redis command traces are out of this phase. `OpenTelemetry.Instrumentation.StackExchangeRedis` is still a beta package. The cache signal is the `taskflow.cache.lookups` counter.
- W3C `traceparent` on the `DueSoon` AMQP message. The publisher injects it. The consumer extracts it and continues that trace. The header name is `traceparent`.

HTTP duration, MongoDB commands, and the Brevo HTTP call come from this instrumentation. They are not duplicated with a hand-written histogram.

Local `docker-compose.yml` passes `OpenTelemetry__*` through for the three app services, defaulting `Enabled` to false. `.env.example` documents `OTEL_ENABLED`, `OTEL_EXPORTER_OTLP_ENDPOINT`, and `OTEL_EXPORTER_OTLP_HEADERS` empty. The token stays in `.env`, which is gitignored.

`infra/compose/docker-compose.aws.yml` does not gain these variables, a collector, or a Grafana service. With the default `Enabled=false`, the EC2 stack emits no OTLP traffic.

### D9 — ECS later, same application

When the documented ECS evolution lands (Fargate, ALB, Mongo off the EC2 host, SSM redeploy removed), telemetry is part of that migration:

- The images stay free of a vendor SDK. Each task definition runs an ADOT or Grafana Alloy sidecar. The app sends OTLP to `localhost`. The sidecar reads the Grafana Cloud token from Secrets Manager, adds the cluster and task attributes, sets `service.version` to `github.sha`, sets `deployment.environment` to `aws`, and forwards to the same Grafana Cloud stack.
- Liveness callers move from `GET /health` to `GET /health/live`, and `GET /health` is removed. The same edit covers every curl and healthcheck that still uses the old path: `docker-compose.yml`, `infra/compose/docker-compose.aws.yml`, `.github/workflows/ci.yml`, the SSM redeploy curl, and `health_url` in `infra/README.md`. Container health checks and the ECS task health check use `GET /health/live`. The API target group uses `GET /health/ready`. Scheduler and notifications are not behind the ALB. Their task health uses readiness. The scheduler stays at desired count 1.
- ECS CPU and memory stay in CloudWatch. Application traces, logs, and the business metrics in D7 stay in Grafana Cloud.

No EC2 instance, Compose file, or Terraform module changes for this in Phase 4.

### D10 — Where the code lives

Domain does not gain a logging, metrics, or health reference.

Application owns reminder summaries, delivery reasons, reminder meters, and the MediatR span behavior. It already references `Microsoft.Extensions.Logging.Abstractions`.

Infrastructure owns JSON-unrelated health checks, cache meters, the OpenTelemetry registration, AMQP `traceparent` propagation, and the Redis lookup counter.

Each host's `Program.cs` turns on the JSON console, maps `/health/live` and `/health/ready`, and passes its `OpenTelemetry:ServiceName`. The API also logs requests and uses the trace helper from `GlobalExceptionHandler`.

## Success criteria

- [ ] API, scheduler, and notifications write JSON logs to stdout. A normal API request produces one Information line with method, route template, status, duration, and `TraceId`. Health routes do not.
- [ ] An unexpected exception is logged at Error with the same `TraceId` that `ProblemDetails.extensions.traceId` returns. Passwords, tokens, API keys, email addresses, and email bodies do not appear in logs.
- [ ] `GET /health` and `GET /health/live` return HTTP 200 and `{ "status": "healthy" }` without calling MongoDB, Redis, or RabbitMQ. Compose and CI still call `GET /health`. Moving those callers to `GET /health/live`, and then removing `GET /health`, happens with the ECS migration (D9).
- [ ] `GET /health/ready` on the API is Unhealthy when MongoDB does not respond and Degraded (HTTP 200) when only Redis does not. Scheduler and notifications readiness is Unhealthy when MongoDB or RabbitMQ does not respond. Brevo is not checked.
- [ ] A finished scan logs published and skipped counts. A delivery logs `already_sent`, `skipped`, `sent`, or `requeue` without changing ack and requeue behavior.
- [ ] The four instruments in D7 exist. Cache lookups are counted inside `RedisTaskCache`. Metric tags are only the sets in D7. `dotnet-counters` can see the meters without Grafana.
- [ ] With `OpenTelemetry:Enabled=false` (the default), no OTLP exporter is registered and the EC2 Compose file is unchanged.
- [ ] With export enabled, a `DueSoon` message carries `traceparent`, and the notifications span is a child of the scheduler span. `service.name` and `deployment.environment` are set. `SERVICE_VERSION`, when present, becomes `service.version`.
