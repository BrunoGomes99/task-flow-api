# TaskFlow infrastructure

Terraform scaffold for a **low-cost, destroyable** AWS study environment:

| Module | What it creates |
|--------|-----------------|
| `modules/network` | VPC, public subnet, Internet Gateway (no NAT) |
| `modules/ecr` | ECR repository for the API image (`taskflow-api` by default) |
| `modules/compute` | EC2 (Amazon Linux 2023), security group, IAM instance profile, user-data |

On first boot, user-data installs Docker + Compose, writes `infra/compose/docker-compose.aws.yml` and a `.env` under `/opt/taskflow`, authenticates to ECR, and runs **API + MongoDB** on the same host. MongoDB port **27017 is not** opened on the security group.

This stack is a learning scaffold. **Do not treat it as production HA.** Prefer `terraform destroy` when idle.

## Prerequisites

- [Terraform](https://developer.hashicorp.com/terraform/install) `>= 1.10` (S3 native state locking via `use_lockfile`)
- [AWS CLI](https://aws.amazon.com/cli/) with base credentials (SSO profile or keys) that can `sts:AssumeRole` on `terraform-deploy-role`
- An IAM role **`terraform-deploy-role`** (or equivalent) with least-privilege deploy permissions for this stack
- An **S3 bucket for Terraform state**, created **outside** this root (versioning on, block public access, encryption). This stack does not manage the state bucket.
- Optional: GitHub Environment **`production`** + OIDC role for the publish job (see [GitHub Actions → ECR](#github-actions--ecr-publish))

## Auth (`assume_role`)

The AWS provider assumes your deploy role. Copy the example tfvars and set the real ARN and secrets:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars:
#   terraform_deploy_role_arn = "arn:aws:iam::ACCOUNT_ID:role/terraform-deploy-role"
#   jwt_secret                = "..."   # >= 32 characters
#   mongo_root_password       = "..."   # >= 12 characters
```

`terraform.tfvars` is gitignored. Do not commit real account IDs or secrets.

You can also pass sensitive values via environment variables:

```powershell
$env:TF_VAR_jwt_secret = "..."
$env:TF_VAR_mongo_root_password = "..."
$env:TF_VAR_terraform_deploy_role_arn = "arn:aws:iam::ACCOUNT_ID:role/terraform-deploy-role"
```

### Important variables

| Variable | Required | Notes |
|----------|----------|--------|
| `terraform_deploy_role_arn` | yes | Role Terraform assumes for all AWS API calls |
| `jwt_secret` | yes | Sensitive; min 32 characters |
| `mongo_root_password` | yes | Sensitive; min 12 characters |
| `aws_region` | no | Default `us-east-1` |
| `allowed_api_cidr` | no | Default `0.0.0.0/0` (restrict to your IP when possible) |
| `allowed_ssh_cidr` | no | Empty = SSH disabled |
| `instance_type` | no | Default `t3.micro` |
| `api_image_tag` | no | Default `latest` (must exist in ECR for a healthy first boot) |
| `ecr_repository_name` | no | Default `taskflow-api` |

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

## Plan / apply / destroy

```powershell
# From infra/
terraform init -backend-config=backend.hcl
terraform validate
terraform plan
terraform apply
```

Useful outputs after apply:

- `ecr_repository_url` — target for `docker push` / GitHub publish job
- `public_ip` — EC2 public IPv4
- `health_url` — `http://<public_ip>:8080/health`

Tear down when idle (avoids ongoing EC2/EBS/ECR storage cost):

```powershell
terraform destroy
```

The **state bucket** and **`terraform-deploy-role`** are operator-owned and are **not** destroyed by this stack.

### Cost notes

- Smallest practical instance class (`t3.micro` default); no NAT Gateway, no ALB, no DocumentDB.
- ECR charges for storage of images; delete unused images or destroy the stack when finished studying.
- Always destroy the EC2 environment when you are not using it.

### Validate without a remote bucket

```powershell
terraform init -backend=false
terraform validate
```

## First boot and image availability

User-data pulls `${ecr_repository_url}:${api_image_tag}` (default tag `latest`).

Recommended order:

1. `terraform apply` (creates ECR + EC2).
2. Publish an image to ECR (GitHub **production** gate or local `docker push`).
3. If the instance booted before the image existed, SSH (if enabled) or replace the instance / re-run compose:

```bash
cd /opt/taskflow
aws ecr get-login-password --region <region> | docker login --username AWS --password-stdin <registry>
docker compose pull
docker compose up -d
```

Publishing a new image to ECR **does not** automatically redeploy a running EC2. That is expected in this scaffold; ECS (below) is the path toward managed rolling deploys.

## GitHub Actions → ECR publish

The workflow [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) is a **single pipeline**:

1. **CI** — restore / build / test on PRs and pushes.
2. **Publish** — only on push to `main` (or `workflow_dispatch`), after CI succeeds, behind the GitHub Environment **`production`** (manual Approve).
3. Build uses `src/TaskFlow.Api/Dockerfile`, tags with `github.sha` and `latest`, pushes to ECR via **OIDC** (no static AWS keys in YAML).

### One-time GitHub setup

1. Create Environment **`production`** (Settings → Environments) and add **Required reviewers** so the publish job waits for approval.
2. Create an IAM role trusted by GitHub’s OIDC provider for this repository (same account as ECR, or a sibling of `terraform-deploy-role`) with ECR push permissions on `taskflow-api`.
3. Set repository **Variables** (not committed):

| Variable | Example |
|----------|---------|
| `AWS_REGION` | `us-east-1` |
| `AWS_ROLE_TO_ASSUME` | `arn:aws:iam::ACCOUNT_ID:role/github-ecr-publish` |
| `ECR_REPOSITORY` | `taskflow-api` |

Until those variables and the OIDC trust exist, the CI job still runs; the publish job will fail at AWS auth — that is expected.

## Security reminders

- Do not open MongoDB to the internet.
- Prefer restricting `allowed_api_cidr` (and SSH) to your IP.
- Never commit `terraform.tfvars`, `backend.hcl`, or real secrets.
- EC2 uses an instance profile for ECR **pull** only; GitHub uses OIDC for ECR **push**.

## Future evolution: ECS

Keep building the **same** API image into ECR. Later:

- Replace the EC2 Compose host with an ECS task definition + **Fargate** service behind an **ALB**.
- Move MongoDB off the app host to a managed store (or a dedicated data host).
- Wire the GitHub `production` gate to an ECS service update instead of (or in addition to) image push.

No ECS resources are required in this phase.

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
  modules/network|ecr|compute/
  templates/user-data.sh.tpl
  compose/docker-compose.aws.yml
```
