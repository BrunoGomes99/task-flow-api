# TaskFlow — ECS Migration Design

Date: 2026-11-04  
Status: **Approved.**  
Plans: [../plans/2026-11-04-ecs-platform.md](../plans/2026-11-04-ecs-platform.md) (`feature/ecs-migration`), then [../plans/2026-11-04-ecs-cd.md](../plans/2026-11-04-ecs-cd.md) (`feature/ecs-cd` after the platform plan is merged).

Supersedes the open ECS choices in [2026-08-02-ci-cd-ec2-design.md](2026-08-02-ci-cd-ec2-design.md) (D8) and [2026-10-27-phase4-observability-design.md](2026-10-27-phase4-observability-design.md) (D9). Those files keep their history. This spec is the source of truth for the migration.

## Goal

Replace the EC2 Compose host with a production-shaped ECS Fargate environment that can be destroyed when idle. The API, scheduler, and notifications images stay the ones GitHub Actions already builds from the three Dockerfiles and pushes to ECR. MongoDB moves to an operator-owned Atlas cluster. Redis and RabbitMQ become ECS services. The ALB is the only public entry, and it reaches only the API.

## Non-goals

- HTTPS, a custom domain, or an ACM certificate. The ALB listener is HTTP on port 80.
- A second NAT Gateway. One NAT is a documented availability limit.
- DocumentDB, ElastiCache, or Amazon MQ.
- Copying the MongoDB data that lives on the EC2 disk. Atlas starts empty.
- Desired count above 1, autoscaling, or task-level high availability.
- Changing reminder ack, requeue, or dead-letter rules. Changing cache behavior.
- Turning local Compose into ECS. `docker-compose.yml` stays the local stack.
- Building API, scheduler, or notifications images from a Compose file. `docker build` in `.github/workflows/ci.yml` remains the build.

## Current state

Terraform under `infra/` creates one VPC (`10.0.0.0/16`), one public subnet (`10.0.1.0/24`), three ECR repositories, and one EC2 instance. User-data writes `infra/compose/docker-compose.aws.yml` to the instance. That file runs prebuilt images. It has no `build:` block. The workflow's `build_push` steps build `src/TaskFlow.Api/Dockerfile`, `src/TaskFlow.Scheduler/Dockerfile`, and `src/TaskFlow.Notifications/Dockerfile`, tag them with `github.sha` and `latest`, and push to ECR. A later SSM job copies the Compose file to the instance and runs `docker compose pull` and `up`. MongoDB data is a volume on the instance disk.

## Decisions (Approved)

### D1 — Phase 5, two plans

This migration is Phase 5. Plan 1 creates the platform and removes the EC2 path, including the SSM job. Plan 2 teaches the workflow to deploy. Plan 2 starts only after plan 1 is merged, because it updates services that plan 1 creates.

### D2 — Data plane

- **MongoDB** is an Atlas M0 cluster created in the Atlas UI. The operator chooses the M0 region closest to `us-east-1`. Terraform does not create the cluster, and `terraform destroy` does not delete it. The database starts empty. `MongoIndexesInitializer` creates the existing indexes when a host starts. The database name stays `TaskFlowDb`, passed as the plain variable `MongoDb__DatabaseName`. The EC2 variables `mongo_root_username` and `mongo_root_password` leave the AWS stack with the compute module. Local Compose keeps its own MongoDB credentials.
- **Redis** is an ECS service. Image `redis:7.4-alpine`. Command: `redis-server --save "" --appendonly no --maxmemory 64mb --maxmemory-policy allkeys-lru`. No password. The task security group is the network boundary. No host port is published. The task is 0.25 vCPU and 512 MB. Desired count 1. Data is ephemeral. A lost cache is a miss, bounded by the 60-second TTL.
- **RabbitMQ** is an ECS service. Image `rabbitmq:4.1-alpine` (no management plugin). Port 5672 only. The local Compose file keeps `rabbitmq:4.1-management-alpine` and its UI on port 15672. The task is 0.25 vCPU and 512 MB. Desired count 1. The queue is ephemeral. The scheduler republishes a due reminder from `NotificationLog` when the broker has lost a message. The user name stays a plain variable. The password is a secret.

### D3 — Network

The VPC CIDR stays `10.0.0.0/16`. The network module uses two availability zones from `data.aws_availability_zones.available`.

| Subnet | CIDR | Zone |
|---|---|---|
| Public (existing) | `10.0.1.0/24` | first AZ |
| Public (new) | `10.0.2.0/24` | second AZ |
| Private (new) | `10.0.11.0/24` | first AZ |
| Private (new) | `10.0.12.0/24` | second AZ |

Public route tables use the Internet Gateway. One NAT Gateway and its Elastic IP live in `10.0.1.0/24`. Both private route tables send `0.0.0.0/0` to that NAT. Tasks have no public IP. The NAT Elastic IP is an output, and it is the only address on the Atlas IP allowlist (`/32`).

The ALB spans both public subnets and listens on port 80. `allowed_api_cidr` restricts that listener (default `0.0.0.0/0`, same study default as today). The target group forwards to the API on port 8080. Its health check is `GET /health/ready`, success code 200.

Security groups:

- ALB: ingress 80 from `allowed_api_cidr`. Egress to the API security group on 8080.
- API: ingress 8080 from the ALB security group only.
- Redis: ingress 6379 from the API security group only.
- RabbitMQ: ingress 5672 from the scheduler and notifications security groups only.
- Scheduler and notifications: no ingress.
- Every task egresses `0.0.0.0/0`, because Atlas, ECR, Brevo, Grafana Cloud, and the public ADOT image do not have stable addresses inside the VPC.

`allowed_ssh_cidr` leaves with the EC2 module. There is no SSH.

### D4 — Cluster and Service Connect

One Fargate cluster, name prefix `taskflow`. Five services, each with desired count 1, placed on both private subnets:

| Service | Containers | Reached by |
|---|---|---|
| `taskflow-api` | API + ADOT | ALB |
| `taskflow-scheduler` | scheduler + ADOT | nobody from outside the task |
| `taskflow-notifications` | notifications + ADOT | nobody from outside the task |
| `taskflow-redis` | Redis | API |
| `taskflow-rabbitmq` | RabbitMQ | scheduler, notifications |

ECS Service Connect namespace `taskflow` provides the hostnames the app already uses. The API client alias is `redis` on port 6379, so `Redis__ConnectionString` stays `redis:6379,abortConnect=false`. The scheduler and notifications client alias is `rabbitmq` on port 5672, so `RabbitMq__Host` stays `rabbitmq` and `RabbitMq__Port` stays `5672`.

Each .NET task is 0.25 vCPU and 1024 MB. That budget covers the application process and the ADOT sidecar.

The three .NET services use an ECS deployment circuit breaker with rollback. Redis and RabbitMQ do not. Their images are pinned upstream tags owned by Terraform.

`EnableSwagger` stays `true` on the API task. The AWS Compose file already turns it on for study, until a frontend exists.

### D5 — What replaces `docker-compose.aws.yml`

Fargate has no Compose daemon. The task definition takes the job that file had: image, environment, secrets, port, and health check. Plan 1 deletes `infra/compose/docker-compose.aws.yml`, `infra/templates/user-data.sh.tpl`, and `infra/modules/compute`. The three Dockerfiles and the workflow `docker build` stay. Local `docker-compose.yml` stays.

ECR repositories `taskflow-api`, `taskflow-scheduler`, and `taskflow-notifications` stay.

### D6 — Secrets

Terraform writes Secrets Manager secrets from gitignored tfvars, the same place JWT and Mongo credentials live today. The execution role reads them when a task starts. The task role has no AWS permissions. A missing secret prevents the container from starting. Values are not plain environment entries in the task definition.

| Secret | Injected into |
|---|---|
| Atlas connection string | API, scheduler, notifications as `MongoDb__ConnectionString` |
| JWT signing secret | API as `Jwt__Secret` |
| RabbitMQ password | scheduler and notifications as `RabbitMq__Password`; RabbitMQ container as `RABBITMQ_DEFAULT_PASS` |
| Brevo API key | notifications as `Brevo__ApiKey` |
| Brevo sender email | notifications as `Brevo__SenderEmail` |
| Grafana OTLP auth header | ADOT sidecar only, as `GRAFANA_OTLP_HEADERS`. The collector config references `${env:GRAFANA_OTLP_HEADERS}` and does not contain the token. |

Plain environment, not secrets: `Jwt__Issuer`, `Jwt__Audience`, `MongoDb__DatabaseName`, `RabbitMq__User`, `RABBITMQ_DEFAULT_USER` (the same user value), `Brevo__SenderName` (default `TaskFlow`), `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`, `EnableSwagger=true`.

### D7 — ADOT sidecar

The collector is the AWS Distro for OpenTelemetry, image `public.ecr.aws/aws-observability/aws-otel-collector`, pinned to a release tag in Terraform (not `latest`). Grafana Alloy is not used.

The sidecar runs on the API, scheduler, and notifications tasks. Redis and RabbitMQ do not have one. `essential` is false, so a dead collector does not stop the application container.

The application already exports OTLP over HTTP protobuf and appends `/v1/traces`, `/v1/metrics`, and `/v1/logs` to `OpenTelemetry:OtlpEndpoint`. On these tasks:

- `OpenTelemetry__Enabled=true`
- `OpenTelemetry__OtlpEndpoint=http://localhost:4318`
- `OpenTelemetry__Headers` is empty. The app does not see the Grafana token.
- `OpenTelemetry__Environment=aws`
- `SERVICE_VERSION` is the running image tag. The app already copies that variable to `service.version`.

The committed collector config (no token in the file) is passed in `AOT_CONFIG_CONTENT`. It receives OTLP HTTP on port 4318, runs the `resourcedetection` processor with the ECS detector, sets `deployment.environment=aws` and `service.version` from `SERVICE_VERSION`, and exports OTLP HTTP to the Grafana Cloud gateway. The gateway URL is a Terraform variable. The auth header comes from the Grafana secret.

Task CPU and memory stay in the ECS CloudWatch metrics that Fargate already publishes. Container Insights is not enabled.

### D8 — Logs

Each task uses the `awslogs` driver. One log group per service, retention 7 days. This is how a task that dies before export can still be read. Application logs that the OpenTelemetry SDK exports continue to Grafana Cloud. Passwords, tokens, API keys, email addresses, and email bodies stay out of logs, as Phase 4 already requires.

### D9 — Health

`GET /health` is removed from the API, the scheduler, and the notifications host. `GET /health/live` remains the liveness check: HTTP 200, body `{ "status": "healthy" }`, no dependency calls. `GET /health/ready` stays as Phase 4 defined it.

Callers after this migration:

| Caller | Path |
|---|---|
| ECS container health check on all three .NET containers | `GET /health/live` |
| ALB target group | `GET /health/ready` |
| Local `docker-compose.yml` healthchecks | `GET /health/live` |
| Plan 2 smoke check against the ALB | `GET /health/ready` |

The container health check is the probe that causes ECS to restart a container. It is liveness on purpose. A failed readiness check does not restart a worker. This narrows the Phase 4 sentence "scheduler and notifications task health uses readiness": readiness remains the dependency report, and it is not the restart probe. A dependency blip must not restart the container.

Container health check: interval 30 seconds, timeout 5 seconds, retries 3, start period 60 seconds.

The only `GET /health` curl in CI today is inside the SSM script. Plan 1 deletes that job, so that curl goes away with it.

### D10 — Cutover

Operator order for the first apply:

1. Create the Atlas M0 cluster and put its connection string, the JWT secret, the RabbitMQ password, the Brevo values, the Grafana gateway URL, and the Grafana header into gitignored tfvars.
2. Publish the three images to ECR (the existing production gate, or a local push). Plan 1 still accepts an image tag variable. The default remains `latest` for the first apply only.
3. `terraform apply`. This destroys the EC2 instance and the MongoDB volume on its disk. The NAT Elastic IP appears as an output.
4. Add that Elastic IP `/32` to the Atlas network access list. Until Atlas accepts it, `GET /health/ready` fails and the ALB does not route to the API. `GET /health/live` still passes, so ECS leaves the task running. The next target-group check succeeds after Atlas accepts the address. No restart loop.
5. Open `http://<alb-dns>/health/ready`.

From plan 1 until plan 2 is merged, a new image is deployed by changing the Terraform image-tag variable and running `terraform apply`. The workflow still builds and pushes. It does not deploy. `update-service` from the CLI is not the gap procedure: until plan 2, Terraform owns the application image and a later apply would overwrite a revision that the CLI had registered.

`terraform destroy` removes the NAT Gateway, its Elastic IP, the ALB, the cluster, the services, the log groups, the secrets, and the ECR repositories. It does not remove Atlas, the Grafana Cloud stack, the state bucket, or `terraform-deploy-role`. While the stack is applied, the NAT Gateway is the hourly charge that did not exist on the EC2 design. Destroy stops it. A destroy that leaves the Elastic IP allocated keeps the public IPv4 hourly charge until that address is released.

### D11 — Continuous delivery (plan 2)

The job that SSM occupied registers a new revision of each .NET task definition and calls `ecs update-service` on `taskflow-api`, `taskflow-scheduler`, and `taskflow-notifications`. It copies the current task definition, replaces only the application image and `SERVICE_VERSION` with `github.sha`, and keeps the sidecar, secrets, and environment. It waits until the three services are stable, then calls `GET /health/ready` on the ALB. Redis and RabbitMQ are not deployed by CI.

Terraform uses `lifecycle.ignore_changes` on the application container image of those three task definitions, so a later apply does not reset a deployed `github.sha` back to the tag variable. Terraform keeps ownership of the ADOT image tag and of the Redis and RabbitMQ task definitions.

The GitHub Environment `production` stays the single approval gate, before publish. `workflow_dispatch` stays. The OIDC role is operator-owned, as it is today. Plan 2's README change replaces the SSM and `ec2:DescribeInstances` redeploy permissions with `ecs:DescribeServices`, `ecs:DescribeTaskDefinition`, `ecs:RegisterTaskDefinition`, `ecs:UpdateService`, and `iam:PassRole` on the task execution role. Repository variables gain the cluster name and the three service names. `EC2_INSTANCE_ID` is dropped.

### D12 — Failure behavior

| Failure | Result |
|---|---|
| Atlas unreachable, or the NAT IP missing from the allowlist | API `GET /health/ready` is HTTP 503 and the ALB drops the target. `GET /health/live` stays 200, so ECS does not restart-loop the task. Workers keep running. The next scheduler tick records the existing scan failure. |
| Redis down | API readiness is Degraded with HTTP 200. The ALB keeps the target. Get-by-id reads MongoDB. |
| RabbitMQ down | Worker readiness is Unhealthy. The restart probe is liveness, so ECS leaves the task up. If the process exits, ECS starts another task. Delivery resumes when the broker accepts connections. Ack, requeue, and dead-letter rules are unchanged. |
| Brevo down | Not a health check. The existing requeue path handles a refused send. |
| ADOT down | The application keeps serving. Grafana stops receiving telemetry. Stdout remains in CloudWatch Logs. |
| NAT availability zone down | Both private subnets lose egress. Atlas, Brevo, Grafana, and image pulls fail together. |
| A new .NET image never becomes healthy | The deployment circuit breaker rolls the service back to the previous task definition revision. |
| A secret is missing at start | ECS does not start the container. |

## Testing

- Unit tests do not call AWS. Tests that request `GET /health` expect `GET /health/live` and the absence of `GET /health`.
- `terraform validate` passes for the stack.
- After the first apply, a manual check hits `GET /health/live` and `GET /health/ready` on the ALB, then registers a user, logs in, creates a task, and reads it by id.
- Plan 2 is proven with `workflow_dispatch`: the three services reach the new `github.sha` and the ALB readiness check passes.

## Documentation (plan 1)

Update these so they describe the ECS runtime instead of the EC2 host:

- `docs/PROJECT_SPEC.md` — add Phase 5 and point the Phase 2 and Phase 4 ECS notes at this spec.
- `docs/ENGINEERING_GUIDELINES.md` — same. Plan 1 checks the Phase 4 sidecar checkbox when the ADOT sidecar is in the three task definitions.
- `infra/README.md` — apply order, Atlas allowlist, NAT cost, destroy list, and the ALB health URL.
- `README.md` — health path and the AWS runtime summary.

Add a one-line pointer from Phase 2 D8 and Phase 4 D9 to this spec.

## Success criteria

- [ ] `terraform apply` produces two public subnets, two private subnets, one NAT Gateway, an ALB, and five Fargate services. No EC2 instance remains.
- [ ] The ALB answers `GET /health/ready` on port 80. Only the API is registered in the target group. `allowed_api_cidr` is the listener ingress.
- [ ] API tasks reach Redis at `redis:6379` and Atlas through the NAT. Scheduler and notifications reach RabbitMQ at `rabbitmq:5672` and Atlas through the NAT. Notifications reach Brevo through the NAT.
- [ ] The Atlas allowlist contains the NAT Elastic IP and no task public IP.
- [ ] Redis and RabbitMQ have no public ingress. RabbitMQ on ECS does not run the management plugin.
- [ ] JWT, the Atlas URI, the RabbitMQ password, the Brevo API key, the Brevo sender email, and the Grafana header are Secrets Manager secrets. The Grafana header is present only on the ADOT container.
- [ ] With the sidecar running, the three .NET services export OTLP to `localhost:4318` and the collector forwards to Grafana Cloud with `deployment.environment=aws` and `service.version` equal to the image tag.
- [ ] Stopping the ADOT container leaves the application container running.
- [ ] `GET /health` is gone. Container health checks call `GET /health/live`. The API target group calls `GET /health/ready`. Local Compose healthchecks call `GET /health/live`.
- [ ] `infra/compose/docker-compose.aws.yml`, the user-data template, and the compute module are deleted. The workflow no longer calls SSM.
- [ ] `terraform destroy` removes the NAT Gateway and its Elastic IP.
- [ ] Plan 2, once implemented, deploys `github.sha` to the three .NET services, ignores that image on later Terraform applies, and rolls back a bad deployment through the circuit breaker.
