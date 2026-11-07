# ECS Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the EC2 Compose host with Fargate services behind one ALB, with Atlas for MongoDB, Redis and RabbitMQ as ECS services, and an ADOT sidecar on the three .NET tasks.

**Architecture:** The network module grows to two public and two private subnets with one NAT Gateway. New `security`, `secrets`, and `ecs` modules own the security groups, Secrets Manager entries, cluster, task definitions, Service Connect, and the ALB. The compute module, user-data, and `docker-compose.aws.yml` are deleted in the same change that wires ECS into the root module, so an apply does not leave both platforms running. Design: [../specs/2026-11-04-ecs-migration-design.md](../specs/2026-11-04-ecs-migration-design.md).

**Tech Stack:** Terraform AWS provider `~> 5.0`, ECS Fargate, ECS Service Connect, ALB, Secrets Manager, ADOT Collector `v0.51.0`, .NET 10, xUnit.

## Global Constraints

- VPC CIDR stays `10.0.0.0/16`. Subnets: public `10.0.1.0/24` and `10.0.2.0/24`, private `10.0.11.0/24` and `10.0.12.0/24`.
- One NAT Gateway, in the first public subnet. Both private route tables use it. Tasks have `assign_public_ip = false`.
- ALB listens on port 80. Ingress CIDR is `allowed_api_cidr`. Target group health check is `GET /health/ready` with matcher `200`. Container health checks call `GET /health/live`.
- Five services, desired count 1: `taskflow-api`, `taskflow-scheduler`, `taskflow-notifications`, `taskflow-redis`, `taskflow-rabbitmq`. Scheduler stays at 1.
- .NET tasks are 0.25 vCPU and 1024 MB. Redis and RabbitMQ are 0.25 vCPU and 512 MB.
- Service Connect namespace is `taskflow`. Client DNS names stay `redis` port 6379 and `rabbitmq` port 5672.
- Redis image `redis:7.4-alpine` with `--save "" --appendonly no --maxmemory 64mb --maxmemory-policy allkeys-lru`. RabbitMQ image `rabbitmq:4.1-alpine` (no management plugin).
- ADOT image is `public.ecr.aws/aws-observability/aws-otel-collector:v0.51.0`. Sidecar `essential` is false. App OTLP endpoint is `http://localhost:4318`. App headers stay empty. Grafana header is `GRAFANA_OTLP_HEADERS` on the sidecar only, value `Basic <base64>` without an `Authorization=` prefix.
- `OpenTelemetry__Enabled=true` and `OpenTelemetry__Environment=aws` on the three .NET containers. `SERVICE_VERSION` is the image tag.
- Secrets: Atlas URI, JWT secret, RabbitMQ password, Brevo API key, Brevo sender email, Grafana header. Plain env: issuer, audience, `TaskFlowDb`, RabbitMQ user, Brevo sender name `TaskFlow`, `EnableSwagger=true`.
- Do not copy EC2 MongoDB data. Do not add a second NAT, HTTPS, DocumentDB, ElastiCache, or Amazon MQ.
- Do not run `terraform apply` until Task 6. Earlier tasks stop at `terraform validate`. Apply creates a NAT Gateway and an ALB, which bill until `terraform destroy`.
- `deployment_minimum_healthy_percent = 100` and `deployment_maximum_percent = 200` on every service. Circuit breaker with rollback on the three .NET services only. Do not set `wait_for_steady_state`.
- Unit tests do not call AWS.

## File map

| File | Responsibility |
|---|---|
| `src/TaskFlow.Api/Controllers/HealthController.cs` | Liveness route `/health/live` only |
| `src/TaskFlow.Scheduler/Program.cs` | Worker liveness route |
| `src/TaskFlow.Notifications/Program.cs` | Worker liveness route |
| `docker-compose.yml` | Local healthchecks call `/health/live` |
| `tests/TaskFlow.Infrastructure.Tests/Health/LivenessRouteTests.cs` | Fails if `/health` returns as a liveness route |
| `infra/modules/network/*` | Two AZs, NAT, private routes |
| `infra/modules/security/*` | ALB, API, worker, Redis, and RabbitMQ security groups |
| `infra/modules/secrets/*` | Secrets Manager entries from tfvars |
| `infra/modules/ecs/*` | Cluster, task definitions, services, ALB |
| `infra/adot/collector.yaml` | Collector config with no token and no account URL |
| `infra/main.tf` | Wires the modules and drops compute |
| `.github/workflows/ci.yml` | Drops the SSM redeploy job |

---

### Task 1: Liveness route

**Files:**

- Modify: `src/TaskFlow.Api/Controllers/HealthController.cs`
- Modify: `src/TaskFlow.Scheduler/Program.cs`
- Modify: `src/TaskFlow.Notifications/Program.cs`
- Modify: `docker-compose.yml` (three healthcheck `test` lines)
- Test: `tests/TaskFlow.Infrastructure.Tests/Health/LivenessRouteTests.cs`

**Interfaces:**

- Consumes: existing `GET /health/live` handlers
- Produces: `GET /health` absent on all three hosts; Compose curls `http://127.0.0.1:8080/health/live`

- [x] **Step 1: Write the failing test**

Create `tests/TaskFlow.Infrastructure.Tests/Health/LivenessRouteTests.cs`:

```csharp
namespace TaskFlow.Infrastructure.Tests.Health;

public sealed class LivenessRouteTests
{
    [Fact]
    public void Hosts_ExposeLiveAndNotTheLegacyHealthRoute()
    {
        var root = RepoRoot();

        var controller = File.ReadAllText(Path.Combine(root, "src", "TaskFlow.Api", "Controllers", "HealthController.cs"));
        Assert.Contains("[Route(\"/health/live\")]", controller);
        Assert.DoesNotContain("[Route(\"/health\")]", controller);

        foreach (var relative in new[] { Path.Combine("src", "TaskFlow.Scheduler", "Program.cs"), Path.Combine("src", "TaskFlow.Notifications", "Program.cs") })
        {
            var program = File.ReadAllText(Path.Combine(root, relative));
            Assert.Contains("MapGet(\"/health/live\"", program);
            Assert.DoesNotContain("MapGet(\"/health\"", program);
        }

        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        Assert.Contains("http://127.0.0.1:8080/health/live", compose);
        Assert.DoesNotContain("http://127.0.0.1:8080/health\"", compose);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TaskFlow.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }
}
```

- [x] **Step 2: Run the test and confirm it fails**

Run: `dotnet test TaskFlow.slnx --filter LivenessRouteTests -c Release`

Expected: FAIL because `[Route("/health")]` and `MapGet("/health"` are still present.

- [x] **Step 3: Remove the legacy route**

In `HealthController`, delete the `[Route("/health")]` attribute and change the summary to "Liveness check for Docker and orchestration." Leave `[Route("/health/live")]` and the `Ok(new HealthResponse("healthy"))` body.

Delete this line from both worker `Program.cs` files:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
```

Leave `MapGet("/health/live"` and `MapHealthChecks("/health/ready")`.

In `docker-compose.yml`, replace all three healthcheck URLs:

```yaml
test: ["CMD", "curl", "-fsS", "http://127.0.0.1:8080/health/live"]
```

- [x] **Step 4: Re-run the test**

Run: `dotnet test TaskFlow.slnx --filter LivenessRouteTests -c Release`

Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add src/TaskFlow.Api/Controllers/HealthController.cs src/TaskFlow.Scheduler/Program.cs src/TaskFlow.Notifications/Program.cs docker-compose.yml tests/TaskFlow.Infrastructure.Tests/Health/LivenessRouteTests.cs
git commit -m "Remove the legacy /health liveness route"
```

---

### Task 2: Two AZs and one NAT Gateway

**Files:**

- Modify: `infra/modules/network/variables.tf`
- Modify: `infra/modules/network/main.tf`
- Modify: `infra/modules/network/outputs.tf`
- Modify: `infra/variables.tf`
- Modify: `infra/main.tf` (network module arguments only)

**Interfaces:**

- Consumes: existing VPC `10.0.0.0/16` and subnet `aws_subnet.public`
- Produces: `module.network.public_subnet_ids`, `module.network.private_subnet_ids`, `module.network.nat_public_ip`. Keeps `public_subnet_id` so the compute module still plans.

- [x] **Step 1: Replace the network variables**

`infra/modules/network/variables.tf`:

```hcl
variable "name_prefix" {
  description = "Prefix applied to network resource names and tags."
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
}

variable "azs" {
  description = "Two availability zones. Index 0 keeps the existing public subnet."
  type        = list(string)

  validation {
    condition     = length(var.azs) == 2
    error_message = "azs must contain exactly two availability zones."
  }
}

variable "public_subnet_cidrs" {
  description = "Public subnet CIDRs, one per AZ. Index 0 is the existing subnet."
  type        = list(string)

  validation {
    condition     = length(var.public_subnet_cidrs) == 2
    error_message = "public_subnet_cidrs must contain two CIDRs."
  }
}

variable "private_subnet_cidrs" {
  description = "Private subnet CIDRs, one per AZ."
  type        = list(string)

  validation {
    condition     = length(var.private_subnet_cidrs) == 2
    error_message = "private_subnet_cidrs must contain two CIDRs."
  }
}
```

Add to `infra/variables.tf` (keep `public_subnet_cidr` for now; Task 6 removes it):

```hcl
variable "public_subnet_cidrs" {
  description = "Two public subnet CIDRs. The first matches the existing public subnet."
  type        = list(string)
  default     = ["10.0.1.0/24", "10.0.2.0/24"]
}

variable "private_subnet_cidrs" {
  description = "Two private subnet CIDRs, one per availability zone."
  type        = list(string)
  default     = ["10.0.11.0/24", "10.0.12.0/24"]
}
```

- [x] **Step 2: Rewrite the network module**

Replace `infra/modules/network/main.tf` with:

```hcl
resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name = "${var.name_prefix}-vpc"
  }
}

resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.public_subnet_cidrs[0]
  availability_zone       = var.azs[0]
  map_public_ip_on_launch = true

  tags = {
    Name = "${var.name_prefix}-public-a"
  }
}

resource "aws_subnet" "public_b" {
  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.public_subnet_cidrs[1]
  availability_zone       = var.azs[1]
  map_public_ip_on_launch = true

  tags = {
    Name = "${var.name_prefix}-public-b"
  }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = {
    Name = "${var.name_prefix}-igw"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }

  tags = {
    Name = "${var.name_prefix}-public-rt"
  }
}

resource "aws_route_table_association" "public" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "public_b" {
  subnet_id      = aws_subnet.public_b.id
  route_table_id = aws_route_table.public.id
}

resource "aws_eip" "nat" {
  domain = "vpc"

  tags = {
    Name = "${var.name_prefix}-nat"
  }
}

resource "aws_nat_gateway" "this" {
  allocation_id = aws_eip.nat.id
  subnet_id     = aws_subnet.public.id

  tags = {
    Name = "${var.name_prefix}-nat"
  }

  depends_on = [aws_internet_gateway.this]
}

resource "aws_subnet" "private_a" {
  vpc_id            = aws_vpc.this.id
  cidr_block        = var.private_subnet_cidrs[0]
  availability_zone = var.azs[0]

  tags = {
    Name = "${var.name_prefix}-private-a"
  }
}

resource "aws_subnet" "private_b" {
  vpc_id            = aws_vpc.this.id
  cidr_block        = var.private_subnet_cidrs[1]
  availability_zone = var.azs[1]

  tags = {
    Name = "${var.name_prefix}-private-b"
  }
}

resource "aws_route_table" "private_a" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.this.id
  }

  tags = {
    Name = "${var.name_prefix}-private-a-rt"
  }
}

resource "aws_route_table" "private_b" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.this.id
  }

  tags = {
    Name = "${var.name_prefix}-private-b-rt"
  }
}

resource "aws_route_table_association" "private_a" {
  subnet_id      = aws_subnet.private_a.id
  route_table_id = aws_route_table.private_a.id
}

resource "aws_route_table_association" "private_b" {
  subnet_id      = aws_subnet.private_b.id
  route_table_id = aws_route_table.private_b.id
}
```

Keep the resource name `aws_subnet.public` so Terraform updates the existing subnet in place instead of destroying it.

Replace `infra/modules/network/outputs.tf` with:

```hcl
output "vpc_id" {
  description = "ID of the VPC."
  value       = aws_vpc.this.id
}

output "public_subnet_id" {
  description = "First public subnet. Kept until the EC2 module is removed."
  value       = aws_subnet.public.id
}

output "public_subnet_ids" {
  description = "Both public subnet IDs, for the ALB."
  value       = [aws_subnet.public.id, aws_subnet.public_b.id]
}

output "private_subnet_ids" {
  description = "Private subnet IDs. Depends on the default route to the NAT Gateway."
  value       = [aws_subnet.private_a.id, aws_subnet.private_b.id]
  depends_on = [
    aws_route_table_association.private_a,
    aws_route_table_association.private_b,
  ]
}

output "nat_public_ip" {
  description = "Elastic IP of the NAT Gateway. This is the Atlas allowlist entry."
  value       = aws_eip.nat.public_ip
}
```

- [x] **Step 3: Pass the new arguments from the root module**

In `infra/main.tf`, change the network module to:

```hcl
module "network" {
  source = "./modules/network"

  name_prefix          = var.name_prefix
  vpc_cidr             = var.vpc_cidr
  azs                  = slice(data.aws_availability_zones.available.names, 0, 2)
  public_subnet_cidrs  = var.public_subnet_cidrs
  private_subnet_cidrs = var.private_subnet_cidrs
}
```

Leave the compute module call unchanged.

- [x] **Step 4: Validate**

Run from `infra/`: `terraform init -backend=false` then `terraform validate`

Expected: `Success! The configuration is valid.`

- [x] **Step 5: Commit**

```bash
git add infra/modules/network infra/variables.tf infra/main.tf
git commit -m "Add a second AZ and one NAT Gateway"
```

---

### Task 3: Security groups

**Files:**

- Create: `infra/modules/security/variables.tf`
- Create: `infra/modules/security/main.tf`
- Create: `infra/modules/security/outputs.tf`
- Modify: `infra/main.tf`

**Interfaces:**

- Consumes: `module.network.vpc_id`, `var.allowed_api_cidr`
- Produces: security group IDs `alb`, `api`, `scheduler`, `notifications`, `redis`, `rabbitmq`

- [x] **Step 1: Write the module**

`infra/modules/security/variables.tf`:

```hcl
variable "name_prefix" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "allowed_api_cidr" {
  type = string
}
```

`infra/modules/security/main.tf`:

```hcl
resource "aws_security_group" "alb" {
  name        = "${var.name_prefix}-alb"
  description = "Public HTTP entry for the TaskFlow API."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-alb" }
}

resource "aws_security_group" "api" {
  name        = "${var.name_prefix}-api"
  description = "TaskFlow API tasks. Ingress is the ALB only."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-api" }
}

resource "aws_security_group" "scheduler" {
  name        = "${var.name_prefix}-scheduler"
  description = "Scheduler tasks. No ingress."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-scheduler" }
}

resource "aws_security_group" "notifications" {
  name        = "${var.name_prefix}-notifications"
  description = "Notifications tasks. No ingress."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-notifications" }
}

resource "aws_security_group" "redis" {
  name        = "${var.name_prefix}-redis"
  description = "Redis tasks. Ingress is the API only."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-redis" }
}

resource "aws_security_group" "rabbitmq" {
  name        = "${var.name_prefix}-rabbitmq"
  description = "RabbitMQ tasks. Ingress is the workers only."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-rabbitmq" }
}

resource "aws_vpc_security_group_ingress_rule" "alb_http" {
  security_group_id = aws_security_group.alb.id
  description       = "HTTP from allowed_api_cidr"
  cidr_ipv4         = var.allowed_api_cidr
  from_port         = 80
  to_port           = 80
  ip_protocol       = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "alb_to_api" {
  security_group_id            = aws_security_group.alb.id
  description                  = "API targets"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "api_from_alb" {
  security_group_id            = aws_security_group.api.id
  description                  = "ALB"
  referenced_security_group_id = aws_security_group.alb.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "redis_from_api" {
  security_group_id            = aws_security_group.redis.id
  description                  = "API"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 6379
  to_port                      = 6379
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "rabbit_from_scheduler" {
  security_group_id            = aws_security_group.rabbitmq.id
  description                  = "Scheduler"
  referenced_security_group_id = aws_security_group.scheduler.id
  from_port                    = 5672
  to_port                      = 5672
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "rabbit_from_notifications" {
  security_group_id            = aws_security_group.rabbitmq.id
  description                  = "Notifications"
  referenced_security_group_id = aws_security_group.notifications.id
  from_port                    = 5672
  to_port                      = 5672
  ip_protocol                  = "tcp"
}

locals {
  egress_groups = {
    api           = aws_security_group.api.id
    scheduler     = aws_security_group.scheduler.id
    notifications = aws_security_group.notifications.id
    redis         = aws_security_group.redis.id
    rabbitmq      = aws_security_group.rabbitmq.id
  }
}

resource "aws_vpc_security_group_egress_rule" "tasks" {
  for_each = local.egress_groups

  security_group_id = each.value
  description       = "Egress through the NAT Gateway"
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}
```

`infra/modules/security/outputs.tf`:

```hcl
output "alb_security_group_id" {
  value = aws_security_group.alb.id
}

output "api_security_group_id" {
  value = aws_security_group.api.id
}

output "scheduler_security_group_id" {
  value = aws_security_group.scheduler.id
}

output "notifications_security_group_id" {
  value = aws_security_group.notifications.id
}

output "redis_security_group_id" {
  value = aws_security_group.redis.id
}

output "rabbitmq_security_group_id" {
  value = aws_security_group.rabbitmq.id
}
```

- [x] **Step 2: Call the module from the root**

Append to `infra/main.tf`:

```hcl
module "security" {
  source = "./modules/security"

  name_prefix      = var.name_prefix
  vpc_id           = module.network.vpc_id
  allowed_api_cidr = var.allowed_api_cidr
}
```

- [x] **Step 3: Validate**

Run from `infra/`: `terraform validate`

Expected: success.

- [x] **Step 4: Commit**

```bash
git add infra/modules/security infra/main.tf
git commit -m "Add ECS security groups for the ALB and tasks"
```

---

### Task 4: Secrets Manager

**Files:**

- Create: `infra/modules/secrets/variables.tf`
- Create: `infra/modules/secrets/main.tf`
- Create: `infra/modules/secrets/outputs.tf`
- Modify: `infra/variables.tf`
- Modify: `infra/main.tf`
- Modify: `infra/terraform.tfvars.example`

**Interfaces:**

- Consumes: gitignored tfvars
- Produces: secret ARNs `atlas`, `jwt`, `rabbitmq`, `brevo_api_key`, `brevo_sender_email`, `grafana_headers`

- [x] **Step 1: Add root variables**

Append to `infra/variables.tf`. Keep `mongo_root_username` and `mongo_root_password` until Task 6.

```hcl
variable "atlas_connection_string" {
  description = "Atlas connection string, including credentials. Do not commit real values."
  type        = string
  sensitive   = true

  validation {
    condition     = startswith(var.atlas_connection_string, "mongodb")
    error_message = "atlas_connection_string must be a MongoDB URI."
  }
}

variable "grafana_otlp_endpoint" {
  description = "Grafana Cloud OTLP gateway base URL, without a signal path."
  type        = string

  validation {
    condition     = startswith(var.grafana_otlp_endpoint, "https://")
    error_message = "grafana_otlp_endpoint must be an https URL."
  }
}

variable "grafana_otlp_headers" {
  description = "Value of the Grafana Authorization header (Basic <base64>), without the Authorization= prefix."
  type        = string
  sensitive   = true

  validation {
    condition     = startswith(var.grafana_otlp_headers, "Basic ")
    error_message = "grafana_otlp_headers must start with 'Basic '."
  }
}

variable "scheduler_image_tag" {
  description = "Tag of the scheduler image in ECR. Plan 2 stops Terraform from rolling this back."
  type        = string
  default     = "latest"
}

variable "notifications_image_tag" {
  description = "Tag of the notifications image in ECR. Plan 2 stops Terraform from rolling this back."
  type        = string
  default     = "latest"
}

variable "adot_image" {
  description = "Pinned ADOT collector image."
  type        = string
  default     = "public.ecr.aws/aws-observability/aws-otel-collector:v0.51.0"
}
```

Add the three required examples to `infra/terraform.tfvars.example` under the existing secrets, using obvious fake values:

```hcl
atlas_connection_string = "mongodb+srv://taskflow:replace-this-password@cluster.example.mongodb.net/?retryWrites=true&w=majority"
grafana_otlp_endpoint   = "https://otlp-gateway-prod-us-east-0.grafana.net/otlp"
grafana_otlp_headers    = "Basic cmVwbGFjZTp0aGlz"
```

- [x] **Step 2: Write the secrets module**

`infra/modules/secrets/variables.tf`:

```hcl
variable "name_prefix" { type = string }
variable "atlas_connection_string" { type = string }
variable "jwt_secret" { type = string }
variable "rabbitmq_password" { type = string }
variable "brevo_api_key" { type = string }
variable "brevo_sender_email" { type = string }
variable "grafana_otlp_headers" { type = string }
```

`infra/modules/secrets/main.tf`:

```hcl
locals {
  secrets = {
    atlas              = var.atlas_connection_string
    jwt                = var.jwt_secret
    rabbitmq           = var.rabbitmq_password
    brevo_api_key      = var.brevo_api_key
    brevo_sender_email = var.brevo_sender_email
    grafana_headers    = var.grafana_otlp_headers
  }
}

resource "aws_secretsmanager_secret" "this" {
  for_each = local.secrets
  name     = "${var.name_prefix}/${each.key}"

  tags = { Name = "${var.name_prefix}-${each.key}" }
}

resource "aws_secretsmanager_secret_version" "this" {
  for_each      = local.secrets
  secret_id     = aws_secretsmanager_secret.this[each.key].id
  secret_string = each.value
}
```

`infra/modules/secrets/outputs.tf`:

```hcl
output "arns" {
  description = "Secret ARNs keyed by atlas, jwt, rabbitmq, brevo_api_key, brevo_sender_email, grafana_headers."
  value       = { for key, secret in aws_secretsmanager_secret.this : key => secret.arn }
}
```

- [x] **Step 3: Call the module**

Append to `infra/main.tf`:

```hcl
module "secrets" {
  source = "./modules/secrets"

  name_prefix             = var.name_prefix
  atlas_connection_string = var.atlas_connection_string
  jwt_secret              = var.jwt_secret
  rabbitmq_password       = var.rabbitmq_password
  brevo_api_key           = var.brevo_api_key
  brevo_sender_email      = var.brevo_sender_email
  grafana_otlp_headers    = var.grafana_otlp_headers
}
```

- [x] **Step 4: Validate**

Run from `infra/`: `terraform validate`

Expected: success. `terraform validate` does not require the new tfvars to be filled.

- [x] **Step 5: Commit**

```bash
git add infra/modules/secrets infra/variables.tf infra/main.tf infra/terraform.tfvars.example
git commit -m "Store ECS runtime secrets in Secrets Manager"
```

---

### Task 5: Cluster, tasks, services, and the ALB

**Files:**

- Create: `infra/adot/collector.yaml`
- Create: `infra/modules/ecs/variables.tf`
- Create: `infra/modules/ecs/iam.tf`
- Create: `infra/modules/ecs/logs.tf`
- Create: `infra/modules/ecs/cluster.tf`
- Create: `infra/modules/ecs/tasks.tf`
- Create: `infra/modules/ecs/services.tf`
- Create: `infra/modules/ecs/alb.tf`
- Create: `infra/modules/ecs/outputs.tf`
- Modify: `infra/main.tf`

**Interfaces:**

- Consumes: network subnet IDs, security group IDs, `module.secrets.arns`, ECR repository URLs, image tags, `grafana_otlp_endpoint`, `adot_image`
- Produces: `module.ecs.alb_dns_name`, `module.ecs.cluster_name`, service names `taskflow-api`, `taskflow-scheduler`, `taskflow-notifications`

- [x] **Step 1: Commit the collector config**

Create `infra/adot/collector.yaml` with LF line endings. Terraform `file()` must not interpolate this document. The collector resolves the env vars:

```yaml
receivers:
  otlp:
    protocols:
      http:
        endpoint: 0.0.0.0:4318

processors:
  resourcedetection:
    detectors: [env, ecs]
    timeout: 2s
    override: false
  resource:
    attributes:
      - key: deployment.environment
        value: aws
        action: upsert
      - key: service.version
        value: ${env:SERVICE_VERSION}
        action: upsert

exporters:
  otlphttp:
    endpoint: ${env:GRAFANA_OTLP_ENDPOINT}
    headers:
      Authorization: ${env:GRAFANA_OTLP_HEADERS}

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [resourcedetection, resource]
      exporters: [otlphttp]
    metrics:
      receivers: [otlp]
      processors: [resourcedetection, resource]
      exporters: [otlphttp]
    logs:
      receivers: [otlp]
      processors: [resourcedetection, resource]
      exporters: [otlphttp]
```

`file()` passes this document through unchanged. The collector, not Terraform, resolves `${env:...}`.

- [x] **Step 2: Write the ECS module variables and IAM**

`infra/modules/ecs/variables.tf`:

```hcl
variable "name_prefix" { type = string }
variable "aws_region" { type = string }
variable "vpc_id" { type = string }
variable "public_subnet_ids" { type = list(string) }
variable "private_subnet_ids" { type = list(string) }
variable "security_group_ids" { type = map(string) }
variable "secret_arns" { type = map(string) }
variable "api_image" { type = string }
variable "scheduler_image" { type = string }
variable "notifications_image" { type = string }
variable "api_image_tag" { type = string }
variable "scheduler_image_tag" { type = string }
variable "notifications_image_tag" { type = string }
variable "adot_image" { type = string }
variable "adot_config" { type = string }
variable "grafana_otlp_endpoint" { type = string }
variable "jwt_issuer" { type = string }
variable "jwt_audience" { type = string }
variable "rabbitmq_user" { type = string }
variable "brevo_sender_name" { type = string }
```

`infra/modules/ecs/iam.tf`:

```hcl
resource "aws_iam_role" "execution" {
  name = "${var.name_prefix}-ecs-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy_attachment" "execution" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role_policy" "secrets" {
  name = "${var.name_prefix}-ecs-secrets"
  role = aws_iam_role.execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["secretsmanager:GetSecretValue"]
      Resource = values(var.secret_arns)
    }]
  })
}
```

There is no task role.

- [x] **Step 3: Logs, cluster, and Service Connect namespace**

`infra/modules/ecs/logs.tf`:

```hcl
locals {
  log_names = toset(["api", "scheduler", "notifications", "redis", "rabbitmq"])
}

resource "aws_cloudwatch_log_group" "this" {
  for_each          = local.log_names
  name              = "/ecs/${var.name_prefix}-${each.key}"
  retention_in_days = 7
}
```

`infra/modules/ecs/cluster.tf`:

```hcl
resource "aws_service_discovery_http_namespace" "this" {
  name        = var.name_prefix
  description = "TaskFlow Service Connect"
}

resource "aws_ecs_cluster" "this" {
  name = var.name_prefix

  service_connect_defaults {
    namespace = aws_service_discovery_http_namespace.this.arn
  }

  tags = { Name = var.name_prefix }
}
```

- [x] **Step 4: Task definitions**

Create `infra/modules/ecs/tasks.tf` with the locals and resources below. CPU is `"256"` on every task. .NET memory is `"1024"`. Redis and RabbitMQ memory is `"512"`. Do not set `OpenTelemetry__Headers`.

```hcl
locals {
  dotnet_base_env = [
    { name = "ASPNETCORE_HTTP_PORTS", value = "8080" },
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "MongoDb__DatabaseName", value = "TaskFlowDb" },
    { name = "OpenTelemetry__Enabled", value = "true" },
    { name = "OpenTelemetry__OtlpEndpoint", value = "http://localhost:4318" },
    { name = "OpenTelemetry__Environment", value = "aws" },
  ]

  dotnet = {
    api = {
      container = "taskflow-api"
      image     = var.api_image
      tag       = var.api_image_tag
      environment = concat(local.dotnet_base_env, [
        { name = "EnableSwagger", value = "true" },
        { name = "Redis__ConnectionString", value = "redis:6379,abortConnect=false" },
        { name = "Jwt__Issuer", value = var.jwt_issuer },
        { name = "Jwt__Audience", value = var.jwt_audience },
        { name = "SERVICE_VERSION", value = var.api_image_tag },
      ])
      secrets = [
        { name = "MongoDb__ConnectionString", valueFrom = var.secret_arns["atlas"] },
        { name = "Jwt__Secret", valueFrom = var.secret_arns["jwt"] },
      ]
    }
    scheduler = {
      container = "taskflow-scheduler"
      image     = var.scheduler_image
      tag       = var.scheduler_image_tag
      environment = concat(local.dotnet_base_env, [
        { name = "RabbitMq__Host", value = "rabbitmq" },
        { name = "RabbitMq__Port", value = "5672" },
        { name = "RabbitMq__User", value = var.rabbitmq_user },
        { name = "SERVICE_VERSION", value = var.scheduler_image_tag },
      ])
      secrets = [
        { name = "MongoDb__ConnectionString", valueFrom = var.secret_arns["atlas"] },
        { name = "RabbitMq__Password", valueFrom = var.secret_arns["rabbitmq"] },
      ]
    }
    notifications = {
      container = "taskflow-notifications"
      image     = var.notifications_image
      tag       = var.notifications_image_tag
      environment = concat(local.dotnet_base_env, [
        { name = "RabbitMq__Host", value = "rabbitmq" },
        { name = "RabbitMq__Port", value = "5672" },
        { name = "RabbitMq__User", value = var.rabbitmq_user },
        { name = "Brevo__SenderName", value = var.brevo_sender_name },
        { name = "SERVICE_VERSION", value = var.notifications_image_tag },
      ])
      secrets = [
        { name = "MongoDb__ConnectionString", valueFrom = var.secret_arns["atlas"] },
        { name = "RabbitMq__Password", valueFrom = var.secret_arns["rabbitmq"] },
        { name = "Brevo__ApiKey", valueFrom = var.secret_arns["brevo_api_key"] },
        { name = "Brevo__SenderEmail", valueFrom = var.secret_arns["brevo_sender_email"] },
      ]
    }
  }
}

resource "aws_ecs_task_definition" "dotnet" {
  for_each                 = local.dotnet
  family                   = "${var.name_prefix}-${each.key}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.execution.arn

  container_definitions = jsonencode([
    {
      name      = each.value.container
      image     = each.value.image
      essential = true
      portMappings = [{ name = "http", containerPort = 8080, protocol = "tcp" }]
      environment = each.value.environment
      secrets     = each.value.secrets
      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://127.0.0.1:8080/health/live || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.this[each.key].name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = each.value.container
        }
      }
    },
    {
      name      = "adot"
      image     = var.adot_image
      essential = false
      environment = [
        { name = "AOT_CONFIG_CONTENT", value = var.adot_config },
        { name = "GRAFANA_OTLP_ENDPOINT", value = var.grafana_otlp_endpoint },
        { name = "SERVICE_VERSION", value = each.value.tag },
      ]
      secrets = [
        { name = "GRAFANA_OTLP_HEADERS", valueFrom = var.secret_arns["grafana_headers"] },
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.this[each.key].name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "adot"
        }
      }
    },
  ])
}
```

Redis and RabbitMQ task definitions go in the same file:

```hcl
resource "aws_ecs_task_definition" "redis" {
  family                   = "${var.name_prefix}-redis"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.execution.arn

  container_definitions = jsonencode([{
    name      = "redis"
    image     = "redis:7.4-alpine"
    essential = true
    command   = ["redis-server", "--save", "", "--appendonly", "no", "--maxmemory", "64mb", "--maxmemory-policy", "allkeys-lru"]
    portMappings = [{ name = "redis", containerPort = 6379, protocol = "tcp" }]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.this["redis"].name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "redis"
      }
    }
  }])
}

resource "aws_ecs_task_definition" "rabbitmq" {
  family                   = "${var.name_prefix}-rabbitmq"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.execution.arn

  container_definitions = jsonencode([{
    name      = "rabbitmq"
    image     = "rabbitmq:4.1-alpine"
    essential = true
    portMappings = [{ name = "rabbitmq", containerPort = 5672, protocol = "tcp" }]
    environment = [{ name = "RABBITMQ_DEFAULT_USER", value = var.rabbitmq_user }]
    secrets     = [{ name = "RABBITMQ_DEFAULT_PASS", valueFrom = var.secret_arns["rabbitmq"] }]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.this["rabbitmq"].name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "rabbitmq"
      }
    }
  }])
}
```

- [x] **Step 5: Services and ALB**

Create `infra/modules/ecs/alb.tf`:

```hcl
resource "aws_lb" "api" {
  name               = "${var.name_prefix}-api"
  load_balancer_type = "application"
  internal           = false
  security_groups    = [var.security_group_ids["alb"]]
  subnets            = var.public_subnet_ids
}

resource "aws_lb_target_group" "api" {
  name        = "${var.name_prefix}-api"
  port        = 8080
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = var.vpc_id

  health_check {
    path                = "/health/ready"
    matcher             = "200"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.api.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}
```

Create `infra/modules/ecs/services.tf`. Do not set `wait_for_steady_state`.

```hcl
resource "aws_ecs_service" "redis" {
  name            = "${var.name_prefix}-redis"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.redis.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.security_group_ids["redis"]]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_http_namespace.this.arn

    service {
      port_name      = "redis"
      discovery_name = "redis"
      client_alias {
        port     = 6379
        dns_name = "redis"
      }
    }
  }
}

resource "aws_ecs_service" "rabbitmq" {
  name            = "${var.name_prefix}-rabbitmq"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.rabbitmq.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.security_group_ids["rabbitmq"]]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_http_namespace.this.arn

    service {
      port_name      = "rabbitmq"
      discovery_name = "rabbitmq"
      client_alias {
        port     = 5672
        dns_name = "rabbitmq"
      }
    }
  }
}

resource "aws_ecs_service" "api" {
  name            = "${var.name_prefix}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.dotnet["api"].arn
  desired_count   = 1
  launch_type     = "FARGATE"

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200
  health_check_grace_period_seconds  = 60

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.security_group_ids["api"]]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "taskflow-api"
    container_port   = 8080
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_http_namespace.this.arn
  }

  depends_on = [aws_ecs_service.redis, aws_lb_listener.http]
}

resource "aws_ecs_service" "scheduler" {
  name            = "${var.name_prefix}-scheduler"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.dotnet["scheduler"].arn
  desired_count   = 1
  launch_type     = "FARGATE"

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.security_group_ids["scheduler"]]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_http_namespace.this.arn
  }

  depends_on = [aws_ecs_service.rabbitmq]
}

resource "aws_ecs_service" "notifications" {
  name            = "${var.name_prefix}-notifications"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.dotnet["notifications"].arn
  desired_count   = 1
  launch_type     = "FARGATE"

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.security_group_ids["notifications"]]
    assign_public_ip = false
  }

  service_connect_configuration {
    enabled   = true
    namespace = aws_service_discovery_http_namespace.this.arn
  }

  depends_on = [aws_ecs_service.rabbitmq]
}
```

Create `infra/modules/ecs/outputs.tf`:

```hcl
output "alb_dns_name" {
  value = aws_lb.api.dns_name
}

output "cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "api_service_name" {
  value = aws_ecs_service.api.name
}

output "scheduler_service_name" {
  value = aws_ecs_service.scheduler.name
}

output "notifications_service_name" {
  value = aws_ecs_service.notifications.name
}
```

- [x] **Step 6: Wire the module**

Append this block to `infra/main.tf`. Leave `module "compute"` in place. Do not apply.

```hcl
module "ecs" {
  source = "./modules/ecs"

  name_prefix              = var.name_prefix
  aws_region               = var.aws_region
  vpc_id                   = module.network.vpc_id
  public_subnet_ids        = module.network.public_subnet_ids
  private_subnet_ids       = module.network.private_subnet_ids
  grafana_otlp_endpoint    = var.grafana_otlp_endpoint
  adot_image               = var.adot_image
  adot_config              = file("${path.module}/adot/collector.yaml")
  jwt_issuer               = var.jwt_issuer
  jwt_audience             = var.jwt_audience
  rabbitmq_user            = var.rabbitmq_user
  brevo_sender_name        = var.brevo_sender_name
  api_image                = "${module.ecr.repository_url}:${var.api_image_tag}"
  scheduler_image          = "${module.ecr_scheduler.repository_url}:${var.scheduler_image_tag}"
  notifications_image      = "${module.ecr_notifications.repository_url}:${var.notifications_image_tag}"
  api_image_tag            = var.api_image_tag
  scheduler_image_tag      = var.scheduler_image_tag
  notifications_image_tag  = var.notifications_image_tag
  secret_arns              = module.secrets.arns

  security_group_ids = {
    alb           = module.security.alb_security_group_id
    api           = module.security.api_security_group_id
    scheduler     = module.security.scheduler_security_group_id
    notifications = module.security.notifications_security_group_id
    redis         = module.security.redis_security_group_id
    rabbitmq      = module.security.rabbitmq_security_group_id
  }
}
```

- [x] **Step 7: Validate**

Run from `infra/`: `terraform validate`

Expected: success.

- [x] **Step 8: Commit**

```bash
git add infra/adot infra/modules/ecs infra/main.tf
git commit -m "Add the Fargate cluster, services, and API load balancer"
```

---

### Task 6: Remove EC2 and point the docs at ECS

**Files:**

- Delete: `infra/modules/compute/` (all files)
- Delete: `infra/templates/user-data.sh.tpl`
- Delete: `infra/compose/docker-compose.aws.yml`
- Modify: `infra/main.tf` (remove the compute module)
- Modify: `infra/outputs.tf`
- Modify: `infra/variables.tf` (remove `instance_type`, `allowed_ssh_cidr`, `mongo_root_username`, `mongo_root_password`, `public_subnet_cidr`)
- Modify: `infra/modules/network/outputs.tf` (remove `public_subnet_id`)
- Modify: `infra/terraform.tfvars.example`
- Modify: `.github/workflows/ci.yml`
- Modify: `infra/README.md`, `README.md`, `docs/PROJECT_SPEC.md`, `docs/ENGINEERING_GUIDELINES.md`
- Modify: the D8 paragraph in `docs/superpowers/specs/2026-08-02-ci-cd-ec2-design.md` and the D9 paragraph in `docs/superpowers/specs/2026-10-27-phase4-observability-design.md` by adding one sentence that the migration spec is the source of truth

**Interfaces:**

- Consumes: `module.ecs.alb_dns_name`, `module.network.nat_public_ip`
- Produces: root outputs `alb_dns_name`, `nat_public_ip`, `health_url` = `http://<alb_dns_name>/health/ready`. No `public_ip` and no `instance_id`.

- [x] **Step 1: Drop the compute module and the old variables**

Remove `module "compute"` from `infra/main.tf`. Delete the compute module directory, `infra/templates/user-data.sh.tpl`, and `infra/compose/docker-compose.aws.yml`. Remove from `infra/variables.tf` the variables that only the compute module read: `instance_type`, `allowed_ssh_cidr`, `mongo_root_username`, `mongo_root_password`, and `public_subnet_cidr`. Remove those keys from `infra/terraform.tfvars.example`.

Replace `infra/outputs.tf` EC2 outputs with:

```hcl
output "alb_dns_name" {
  description = "Public DNS name of the API load balancer."
  value       = module.ecs.alb_dns_name
}

output "nat_public_ip" {
  description = "NAT Elastic IP to place on the Atlas network access list."
  value       = module.network.nat_public_ip
}

output "health_url" {
  description = "API readiness URL."
  value       = "http://${module.ecs.alb_dns_name}/health/ready"
}

output "cluster_name" {
  description = "ECS cluster name."
  value       = module.ecs.cluster_name
}
```

Keep the three ECR URL outputs.

- [x] **Step 2: Remove the SSM job**

Delete the `redeploy-ec2` job from `.github/workflows/ci.yml`, including its header comment that says the SSM path is temporary. The file's top comment becomes: CI on every PR and push; ECR publish of the three Dockerfiles after the `production` environment on `main`. Publish does not deploy. Deploy until the CD plan is `terraform apply` with the image tag variables.

- [x] **Step 3: Rewrite the operator docs**

`infra/README.md` describes the modules network, ECR, security, secrets, and ECS. The apply order is: create the Atlas M0 cluster, fill gitignored tfvars (`atlas_connection_string`, `jwt_secret`, `rabbitmq_password`, `brevo_api_key`, `brevo_sender_email`, `grafana_otlp_endpoint`, `grafana_otlp_headers`), publish the three images, `terraform apply`, copy `nat_public_ip` to the Atlas allowlist as a `/32`, then open `health_url`. State that readiness stays failed until Atlas accepts that IP, and that liveness can pass while the ALB does not route. Cost note: the NAT Gateway and the ALB bill per hour while the stack exists; `terraform destroy` removes both and the Elastic IP. Atlas, Grafana Cloud, the state bucket, and `terraform-deploy-role` are not destroyed. Delete the SSH, user-data, SSM, and `t3.micro` sections.

`README.md` feature line for Phase 2 names ECR plus the ECS design, and the health sentence for local Compose says `GET /health/live`. Add a Phase 5 line pointing at the migration spec.

`docs/PROJECT_SPEC.md` gains this section and the Phase 2 and Phase 4 "ECS later" sentences point at the migration spec:

```markdown
### Phase 5 — ECS

The EC2 Compose host is replaced by Fargate. Design: [superpowers/specs/2026-11-04-ecs-migration-design.md](superpowers/specs/2026-11-04-ecs-migration-design.md).

- One NAT Gateway, two public subnets, two private subnets, tasks without public IPs.
- ALB on port 80 in front of the API only. Readiness is `GET /health/ready`. Liveness is `GET /health/live`. `GET /health` is removed.
- Atlas holds MongoDB and starts empty. Redis and RabbitMQ are ECS services. RabbitMQ on ECS has no management plugin.
- ADOT sidecar on the three .NET tasks forwards OTLP to Grafana Cloud. The Grafana header stays on the sidecar.
- SSM redeploy is removed. Until the CD plan, a new image is deployed with `terraform apply` and the image tag variables.
```

`docs/ENGINEERING_GUIDELINES.md` gains a Phase 5 checklist matching the spec success criteria, unchecked except the items this plan finishes. Check the Phase 4 sidecar box only after the ADOT container is in the three task definitions in this task's diff.

Add this sentence at the end of Phase 2 D8 and Phase 4 D9: "The ECS migration is specified in [2026-11-04-ecs-migration-design.md](2026-11-04-ecs-migration-design.md)."

- [x] **Step 4: Validate and test**

Run from `infra/`: `terraform validate`

Expected: success, with no reference to `module.compute`.

Run: `dotnet test TaskFlow.slnx -c Release`

Expected: PASS, including `LivenessRouteTests`.

- [x] **Step 5: Commit**

```bash
git add infra .github/workflows/ci.yml README.md docs/PROJECT_SPEC.md docs/ENGINEERING_GUIDELINES.md docs/superpowers/specs/2026-08-02-ci-cd-ec2-design.md docs/superpowers/specs/2026-10-27-phase4-observability-design.md
git commit -m "Replace the EC2 host with the ECS platform"
```

- [x] **Step 6: Apply only when the operator is ready to pay for the NAT Gateway**

From `infra/`, with real tfvars and images already in ECR:

```bash
terraform plan
terraform apply
```

Expected outputs include `nat_public_ip` and `health_url`. Add the Elastic IP `/32` in Atlas. `GET /health/live` on the ALB returns 200 once a task is up. `GET /health/ready` returns 200 only after Atlas accepts the NAT address. Then register a user, log in, create a task, and read it by id. `terraform destroy` removes the NAT Gateway and its Elastic IP when the lab is idle.
