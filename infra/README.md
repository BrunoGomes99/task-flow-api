# TaskFlow infrastructure

Terraform for a destroyable AWS study environment. Fargate replaces the old EC2 Compose host. Design: [docs/superpowers/specs/2026-11-04-ecs-migration-design.md](../docs/superpowers/specs/2026-11-04-ecs-migration-design.md).

| Module | What it creates |
|--------|-----------------|
| `modules/network` | VPC `10.0.0.0/16`, two public subnets, two private subnets, one NAT Gateway |
| `modules/ecr` | ECR repositories `taskflow-api`, `taskflow-scheduler`, and `taskflow-notifications` |
| `modules/security` | Security groups for the ALB, API, scheduler, notifications, Redis, and RabbitMQ |
| `modules/secrets` | Secrets Manager entries for Atlas, JWT, RabbitMQ, Brevo, and the Grafana header |
| `modules/ecs` | Fargate cluster, Service Connect namespace `taskflow`, five services, and the API load balancer |

Tasks have no public IP. The ALB is the only public entry and it reaches only the API, on port 80. Redis is reached as `redis:6379`. RabbitMQ is reached as `rabbitmq:5672` and does not run the management plugin. MongoDB is an Atlas M0 cluster that Terraform does not create.

This stack is a learning scaffold. **Do not treat it as production HA.** One NAT Gateway is a single point of egress. Prefer `terraform destroy` when idle.

## Prerequisites

- [Terraform](https://developer.hashicorp.com/terraform/install) `>= 1.10` (S3 native state locking via `use_lockfile`)
- [AWS CLI](https://aws.amazon.com/cli/) with base credentials (SSO profile or keys) that can `sts:AssumeRole` on `terraform-deploy-role`
- An IAM role `terraform-deploy-role` (or equivalent) with least-privilege deploy permissions for this stack
- An **S3 bucket for Terraform state**, created **outside** this root (versioning on, block public access, encryption). This stack does not manage the state bucket.
- An Atlas M0 cluster in the region closest to `us-east-1`
- GitHub Environment `production` + OIDC role that can push the three ECR repositories (see [GitHub Actions → ECR](#github-actions--ecr-publish))

## Auth (`assume_role`)

The AWS provider assumes your deploy role. Copy the example tfvars and set the real ARN and secrets:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars (gitignored):
#   terraform_deploy_role_arn = "arn:aws:iam::ACCOUNT_ID:role/terraform-deploy-role"
#   atlas_connection_string   = "mongodb+srv://..."
#   jwt_secret                = "..."   # >= 32 characters
#   rabbitmq_password         = "..."
#   brevo_api_key             = "..."
#   brevo_sender_email        = "you@example.com"
#   grafana_otlp_endpoint     = "https://..."
#   grafana_otlp_headers      = "Basic <base64>"
```

`terraform.tfvars` is gitignored. Do not commit real account IDs or secrets.

### Important variables

| Variable | Required | Notes |
|----------|----------|--------|
| `terraform_deploy_role_arn` | yes | Role Terraform assumes for all AWS API calls |
| `atlas_connection_string` | yes | Sensitive; must start with `mongodb` |
| `jwt_secret` | yes | Sensitive; min 32 characters |
| `rabbitmq_password` | yes | Sensitive; letters and digits, min 12 |
| `brevo_api_key` | yes | Sensitive |
| `brevo_sender_email` | yes | Verified Brevo sender |
| `grafana_otlp_endpoint` | yes | Grafana Cloud OTLP base URL, `https://`, no signal path |
| `grafana_otlp_headers` | yes | Sensitive; `Basic <base64>`, without an `Authorization=` prefix |
| `aws_region` | no | Default `us-east-1` |
| `allowed_api_cidr` | no | Default `0.0.0.0/0` on the ALB listener (restrict to your IP when possible) |
| `api_image_tag` | no | Default `latest`. Scheduler and notifications tags default to `latest` too |

## Remote state (S3 only, no DynamoDB)

State uses the S3 backend with `encrypt = true` and `use_lockfile = true`.

1. Create the state bucket manually (outside this stack).
2. Ensure the base identity and/or `terraform-deploy-role` can read/write the state object prefix and the lock object.
3. Copy and fill backend config:

```powershell
Copy-Item backend.hcl.example backend.hcl
# Edit backend.hcl → bucket / key / region
```

4. Initialize:

```powershell
terraform init -backend-config=backend.hcl
```

If you already have local state, add `-migrate-state` when prompted.

`backend.hcl` is gitignored. Commit only `backend.hcl.example`.

## Apply order

1. Create the Atlas M0 cluster in the Atlas UI. Terraform does not create it, and `terraform destroy` does not delete it. The database starts empty. `MongoIndexesInitializer` creates indexes when a host starts. The database name stays `TaskFlowDb`.
2. Fill the gitignored tfvars listed above.
3. Publish the three images to ECR (GitHub **production** gate or a local `docker push`). The task definitions pull `api_image_tag`, `scheduler_image_tag`, and `notifications_image_tag` (default `latest`).
4. From `infra/`:

```powershell
terraform init -backend-config=backend.hcl
terraform validate
terraform plan
terraform apply
```

5. Copy the `nat_public_ip` output into the Atlas network access list as a `/32`. Do not add a task public IP. Tasks have none.
6. Open `health_url` (`http://<alb_dns_name>/health/ready`).

Readiness stays failed until Atlas accepts that address, so the ALB does not route to the API yet. Liveness can still pass: `GET /health/live` on the ALB returns 200 once a task is up, and ECS does not restart the task for a failed readiness check. After Atlas accepts the NAT address, `GET /health/ready` returns 200.

Useful outputs after apply:

- `alb_dns_name` — public DNS name of the API load balancer
- `nat_public_ip` — Elastic IP for the Atlas allowlist
- `health_url` — `http://<alb_dns_name>/health/ready`
- `cluster_name` — ECS cluster name
- `ecr_repository_url`, `ecr_scheduler_repository_url`, `ecr_notifications_repository_url` — image push targets

Publishing a new image does not deploy it. Until the CD plan, deploy with `terraform apply` and the image tag variables.

### Validate without a remote bucket

```powershell
terraform init -backend=false
terraform validate
```

## Destroy

```powershell
terraform destroy
```

`terraform destroy` removes the NAT Gateway, its Elastic IP, the ALB, the cluster, the services, the log groups, the secrets, and the ECR repositories. It does not remove Atlas, the Grafana Cloud stack, the state bucket, or `terraform-deploy-role`.

### Cost notes

The NAT Gateway and the ALB bill per hour while the stack exists. `terraform destroy` stops both charges and releases the Elastic IP. A destroy that leaves the Elastic IP allocated keeps the public IPv4 charge until that address is released. ECR storage of images is included in the destroy of the repositories. Destroy the stack when the lab is idle.

## GitHub Actions → ECR publish

The workflow [`.github/workflows/ci.yml`](../.github/workflows/ci.yml):

1. **CI** — restore / build / test on PRs and pushes.
2. **Publish** — only on push to `main` (or `workflow_dispatch`), after CI succeeds, behind the GitHub Environment `production` (manual Approve).
3. Build uses the three Dockerfiles, tags each image with `github.sha` and `latest`, and pushes to ECR via **OIDC**.

Publish does not deploy. Deploy until the CD plan is `terraform apply` with the image tag variables.

### One-time GitHub + IAM setup

1. Create Environment `production` (Settings → Environments) and add **Required reviewers** so the publish job waits for approval.
2. Create an IAM role trusted by GitHub’s OIDC provider for this repository with ECR **push** on `taskflow-api`, `taskflow-scheduler`, and `taskflow-notifications`.
3. Set repository **Variables** (not committed):

| Variable | Example | Required |
|----------|---------|----------|
| `AWS_REGION` | `us-east-1` | yes |
| `AWS_ROLE_TO_ASSUME` | `arn:aws:iam::ACCOUNT_ID:role/github-ecr-publish` | yes |
| `API_REPOSITORY` | `taskflow-api` | yes |
| `SCHEDULER_REPOSITORY` | `taskflow-scheduler` | yes |
| `NOTIFICATIONS_REPOSITORY` | `taskflow-notifications` | yes |

Until OIDC is configured, CI still runs; publish fails at AWS auth — that is expected.

## Security reminders

- Do not open MongoDB, Redis, or RabbitMQ to the internet. Atlas allowlists only the NAT Elastic IP.
- Prefer restricting `allowed_api_cidr` to your IP. The listener is HTTP on port 80.
- Never commit `terraform.tfvars`, `backend.hcl`, or real secrets.
- The Grafana header lives only on the ADOT sidecar.

## Layout

```text
infra/
  README.md
  versions.tf
  providers.tf              # assume_role → terraform-deploy-role
  backend.tf                # S3 + use_lockfile
  backend.hcl.example
  terraform.tfvars.example
  variables.tf / outputs.tf / main.tf
  adot/collector.yaml
  modules/network|ecr|security|secrets|ecs/
```
