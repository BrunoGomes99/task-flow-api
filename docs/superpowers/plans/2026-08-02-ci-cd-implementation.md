# CI/CD Implementation (EC2) Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add GitHub Actions CI and a low-cost AWS CD scaffold (ECR + EC2 + Docker Compose with Mongo on the same host), after documenting the Phase 2/3 swap.

**Architecture:** CI validates every PR with restore/build/test. CD is Terraform under `infra/` provisioning a minimal public VPC, ECR, and one EC2 that pulls the API image and runs Compose (API + Mongo). After the network module, wire **AWS provider `assume_role`** to the operator’s `terraform-deploy-role` and an **S3 remote backend** with native lockfile (no DynamoDB). ECS is documented as a future evolution only; no `terraform apply` is required to finish the coding tasks.

**Tech Stack:** GitHub Actions, .NET 10, Docker (existing Dockerfile), Terraform (AWS provider), Amazon S3 (remote state + `use_lockfile`), Amazon ECR, EC2 (Amazon Linux 2023), Docker Compose.

**Spec:** [docs/superpowers/specs/2026-08-02-ci-cd-ec2-design.md](../specs/2026-08-02-ci-cd-ec2-design.md)

**Branch:** `feature/ci-cd-implementation`

## Global Constraints

- Phase order: Phase 2 = CI/CD; Phase 3 = Redis/RabbitMQ/NotificationLog (do not implement Phase 3 here).
- No secrets in git or workflow YAML; use GitHub Secrets / `TF_VAR_` only when needed.
- Reuse `src/TaskFlow.Api/Dockerfile` for any image build.
- Mongo must not be published to the internet Security Group; only API (and optional SSH from operator IP).
- Prefer smallest instance class (`t4g.nano` or `t3.micro`); no NAT Gateway, no ALB, no DocumentDB.
- Documentation language for new infra docs: English (match existing project docs).
- Do not run `terraform apply` unless the human explicitly asks.
- Terraform AWS provider must `assume_role` to the deploy role (ARN via variable / tfvars; role name expected: `terraform-deploy-role`). Do not hardcode account-specific ARNs in committed examples beyond placeholders.
- Remote state: S3 backend only with `use_lockfile = true` (no DynamoDB). Bucket is bootstrapped **outside** this stack (manual or one-off); never commit real `backend.hcl` / `*.tfvars` with account secrets.

---



## File map


| Path                             | Role                                                                 |
| -------------------------------- | -------------------------------------------------------------------- |
| `.github/workflows/ci.yml`       | CI + ECR publish behind Environment `production` (OIDC)              |
| `infra/**`                       | Terraform modules + compose for AWS host                             |
| `infra/backend.tf`               | S3 remote state + `use_lockfile` (no DynamoDB)                       |
| `infra/backend.hcl.example`      | Example partial backend config (bucket/key/region)                   |
| `infra/terraform.tfvars.example` | Example vars including `terraform_deploy_role_arn`                   |
| `docs/ENGINEERING_GUIDELINES.md` | Phase checklists (already reordered in docs-only pass)               |
| `docs/PROJECT_SPEC.md`           | Phased scope (already reordered in docs-only pass)                   |
| `README.md`                      | Link CI/CD docs and branch guidance                                  |


---



### Task 0: Docs-only baseline (completed before coding)

**Files:**

- Create: `docs/superpowers/specs/2026-08-02-ci-cd-ec2-design.md`
- Create: `docs/superpowers/plans/2026-08-02-ci-cd-implementation.md`
- Modify: `docs/ENGINEERING_GUIDELINES.md`, `docs/PROJECT_SPEC.md`, `README.md`

- [x] **Step 1: Record approved design and phase swap in docs** (this documentation pass)
- [x] **Step 2: Create working branch when implementation starts**

```bash
git checkout main
git pull
git checkout -b feature/ci-cd-implementation
```

Expected: branch `feature/ci-cd-implementation` checked out.

---



### Task 1: GitHub Actions CI workflow

**Files:**

- Create: `.github/workflows/ci.yml`
- Modify: `README.md` (CI badge optional; document how CI runs)

**Interfaces:**

- Consumes: `TaskFlow.slnx` (or solution file at repo root), test projects under `tests/`
- Produces: green check on push/PR when restore/build/test succeed

- [x] **Step 1: Add CI workflow file**

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main, "dev"]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore TaskFlow.slnx

      - name: Build
        run: dotnet build TaskFlow.slnx --no-restore -c Release --verbosity minimal

      - name: Test
        run: dotnet test TaskFlow.slnx --no-build -c Release --verbosity normal
```

- [x] **Step 2: Verify locally that the same commands pass**

```bash
dotnet restore TaskFlow.slnx
dotnet build TaskFlow.slnx --no-restore -c Release
dotnet test TaskFlow.slnx --no-build -c Release
```

Expected: all projects build; all tests pass (same as CI).

- [x] **Step 3: Mark Engineering Guidelines CI checkbox when workflow is merged/green**

In `docs/ENGINEERING_GUIDELINES.md` Phase 2 → GitHub Actions, check **CI workflow** after the first green run on GitHub.

- [x] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml README.md docs/ENGINEERING_GUIDELINES.md
git commit -m "$(cat <<'EOF'
ci: add GitHub Actions workflow for restore, build, and test

EOF
)"
```

---



### Task 2: Terraform root and network module

**Files:**

- Create: `infra/versions.tf`, `infra/providers.tf`, `infra/variables.tf`, `infra/outputs.tf`, `infra/main.tf`
- Create: `infra/modules/network/*.tf`
- Create: `infra/README.md` (skeleton; expand in Task 5)

**Interfaces:**

- Produces: VPC id, public subnet id (consumed by compute module)

- [x] **Step 1: Create provider/version pins**

`infra/versions.tf`:

```hcl
terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}
```

`infra/providers.tf` (initial; Task 3 adds `assume_role`):

```hcl
provider "aws" {
  region = var.aws_region
}
```

- [x] **Step 2: Add network module (VPC + public subnet + IGW)**

Module inputs: `name_prefix`, `vpc_cidr`, `public_subnet_cidr`, `az`.  
Module outputs: `vpc_id`, `public_subnet_id`.

Constraints: **no NAT Gateway**. Single public subnet is enough for study.

- [x] **Step 3: Wire module from** `infra/main.tf` **and add variables**

Variables at minimum: `aws_region`, `name_prefix`, `vpc_cidr`, `public_subnet_cidr`, `allowed_ssh_cidr` (default empty / disabled SSH).

- [x] **Step 4: Validate**

```bash
cd infra
terraform init
terraform validate
```

Expected: `Success! The configuration is valid.`

- [x] **Step 5: Commit**

```bash
git add infra/
git commit -m "$(cat <<'EOF'
infra: add Terraform root and minimal public VPC module

EOF
)"
```

---



### Task 3: Provider assume_role + S3 remote backend

**Files:**

- Modify: `infra/providers.tf`, `infra/variables.tf`, `infra/versions.tf` (required_version note if needed)
- Create: `infra/backend.tf`, `infra/backend.hcl.example`, `infra/terraform.tfvars.example`
- Modify: `.gitignore` (ensure `backend.hcl`, `*.tfvars` except `*.tfvars.example`, `.terraform/` remain ignored)
- Modify: `infra/README.md` (short auth + backend bootstrap notes; full docs still expand in Task 6)

**Interfaces:**

- Consumes: operator base credentials (SSO/profile/keys) that can `sts:AssumeRole` on `terraform-deploy-role`
- Consumes: pre-existing S3 bucket for state (created **outside** this stack)
- Produces: provider sessions via assumed role; remote state config ready for `terraform init -backend-config=backend.hcl`

- [x] **Step 1: Add `terraform_deploy_role_arn` and wire `assume_role`**

In `infra/variables.tf`:

```hcl
variable "terraform_deploy_role_arn" {
  description = "ARN of the IAM role Terraform assumes for all AWS API calls (e.g. terraform-deploy-role)."
  type        = string
}
```

In `infra/providers.tf`:

```hcl
provider "aws" {
  region = var.aws_region

  assume_role {
    role_arn     = var.terraform_deploy_role_arn
    session_name = "taskflow-terraform"
  }
}
```

Commit an example only (no real account id):

`infra/terraform.tfvars.example`:

```hcl
aws_region                 = "us-east-1"
terraform_deploy_role_arn  = "arn:aws:iam::123456789012:role/terraform-deploy-role"
```

Operator copies to `terraform.tfvars` (gitignored) and replaces the account id / ARN.

- [x] **Step 2: Add S3 backend with native lockfile (no DynamoDB)**

`infra/backend.tf`:

```hcl
terraform {
  backend "s3" {
    # bucket, key, region supplied via -backend-config=backend.hcl (see backend.hcl.example)
    encrypt      = true
    use_lockfile = true
  }
}
```

`infra/backend.hcl.example`:

```hcl
bucket = "your-taskflow-tfstate-bucket"
key    = "taskflow/infra/terraform.tfstate"
region = "us-east-1"
```

Document bootstrap in `infra/README.md` (English):

1. Create an S3 bucket manually (versioning on, block public access, encryption).
2. Ensure the **base** identity (and/or `terraform-deploy-role`) can read/write the state prefix and lock object.
3. Copy `backend.hcl.example` → `backend.hcl` and `terraform.tfvars.example` → `terraform.tfvars`; fill real values.
4. `terraform init -backend-config=backend.hcl` (use `-migrate-state` if local state already exists).

Do **not** manage the state bucket inside this same root (chicken-and-egg). Do **not** add DynamoDB.

- [x] **Step 3: Validate**

With `terraform.tfvars` present locally (not committed), or by passing `-var` for the role ARN:

```bash
cd infra
terraform init -backend-config=backend.hcl
terraform validate
```

If the human has not created the bucket yet, `validate` after a local/temporary init is acceptable for the coding task; document that full remote init requires the bucket. Prefer real `init -backend-config=...` when the bucket exists.

Expected: `Success! The configuration is valid.`

Still **do not** `terraform apply` unless the human explicitly asks.

- [x] **Step 4: Commit**

```bash
git add infra/providers.tf infra/variables.tf infra/backend.tf infra/backend.hcl.example infra/terraform.tfvars.example infra/README.md .gitignore
git commit -m "$(cat <<'EOF'
infra: assume terraform-deploy-role and use S3 remote state with lockfile

EOF
)"
```

---



### Task 4: ECR module

**Files:**

- Create: `infra/modules/ecr/*.tf`
- Modify: `infra/main.tf`, `infra/outputs.tf`

**Interfaces:**

- Produces: `repository_url`, `repository_name` (consumed by compute user-data and CD workflow)

- [x] **Step 1: Create ECR repository resource**

Repository name default: `taskflow-api`. Enable image scan on push if cheap/default; keep lifecycle simple (optional: expire untagged after N days).

- [x] **Step 2: Export outputs**

```hcl
output "ecr_repository_url" {
  value = module.ecr.repository_url
}
```

- [x] **Step 3: Validate and commit**

```bash
cd infra && terraform validate
git add infra/
git commit -m "$(cat <<'EOF'
infra: add ECR repository module for the API image

EOF
)"
```

---



### Task 5: Compute module (EC2 + IAM + SG + user-data) and AWS Compose

**Files:**

- Create: `infra/modules/compute/*.tf`
- Create: `infra/templates/user-data.sh.tpl`
- Create: `infra/compose/docker-compose.aws.yml`
- Modify: `infra/main.tf`, `infra/variables.tf`, `infra/outputs.tf`

**Interfaces:**

- Consumes: `vpc_id`, `public_subnet_id`, `ecr_repository_url`
- Produces: `public_ip`, health URL hint

- [x] **Step 1: Security group**

Inbound:

- TCP 8080 (or 80) from `0.0.0.0/0` **or** a configurable CIDR (prefer variable `allowed_api_cidr`)
- TCP 22 only if `allowed_ssh_cidr` is non-empty

Outbound: allow all (needed for ECR pull and Mongo image pull).

Do **not** open Mongo `27017` to the world.

- [x] **Step 2: IAM instance profile**

Permissions: `ecr:GetAuthorizationToken` plus pull actions on the specific repository (`ecr:BatchGetImage`, `ecr:GetDownloadUrlForLayer`, `ecr:BatchCheckLayerAvailability`).

- [x] **Step 3:** `docker-compose.aws.yml`

Services: `taskflow.api` (image from ECR URL + tag variable) and `mongo` (pinned tag, e.g. `mongo:8.0`), shared bridge network, named volume for Mongo data. API env for `MongoDb__ConnectionString` pointing at service name `mongo`. Map host `8080:8080`.

- [x] **Step 4: user-data template**

Install Docker + Compose plugin on Amazon Linux 2023, authenticate to ECR, write compose file and `.env` (JWT + Mongo passwords from Terraform sensitive vars), `docker compose up -d`.

- [x] **Step 5: EC2 instance**

AMI: Amazon Linux 2023 (data source). Instance type variable default `t3.micro` (or `t4g.nano` if ARM image/Dockerfile verified). Associate public IP. Attach instance profile and SG.

- [x] **Step 6: Validate and commit**

```bash
cd infra && terraform validate
git add infra/
git commit -m "$(cat <<'EOF'
infra: add EC2 compute module with Docker Compose API and Mongo

EOF
)"
```

Follow-up commit: normalize user-data to LF on Windows checkouts.

---



### Task 6: Infra README + CI/CD publish gate (format A)

**Decision (2026-08-16):** One workflow on `main` — CI then Environment **`production`** approval → ECR push (OIDC). No separate `cd.yml`. `workflow_dispatch` kept as escape hatch. Publish = image to ECR only; EC2 does not auto-redeploy.

**Files:**

- Modify: `infra/README.md`
- Modify: `.github/workflows/ci.yml` (add `publish-ecr` job; no separate `cd.yml`)
- Modify: `docs/ENGINEERING_GUIDELINES.md` (checkboxes as items land)
- Modify: `README.md` (link to `infra/README.md` and design spec)
- Modify: design spec + this plan (D3 / status)

- [x] **Step 1: Document apply/destroy and cost**

`infra/README.md` must include:

- Prerequisites (AWS CLI, Terraform, base credentials that can assume `terraform-deploy-role`)
- S3 state bucket bootstrap + `backend.hcl` / `terraform.tfvars` setup (`use_lockfile`, no DynamoDB)
- `terraform init -backend-config=backend.hcl` / `plan` / `apply` / `destroy` examples
- Required variables (`terraform_deploy_role_arn`, `jwt_secret`, etc.) via `TF_VAR_` or `terraform.tfvars` (**gitignored**)
- Reminder to destroy when idle
- Future ECS evolution paragraph (same ECR image → Fargate + ALB; Mongo off-box)
- GitHub OIDC variables + Environment `production` setup

- [x] **Step 2: Add publish job to CI workflow (format A)**

Extend `.github/workflows/ci.yml`: after `build-and-test`, job `publish-ecr` runs on push to `main` or `workflow_dispatch`, `environment: production`, build using `src/TaskFlow.Api/Dockerfile`, tag with `github.sha` + `latest`, push to ECR via OIDC (`vars.AWS_ROLE_TO_ASSUME`, `AWS_REGION`, `ECR_REPOSITORY`). Do not hardcode keys.

- [x] **Step 3: Update root README Features/Docs**

Point to Phase 2 CI/CD, design spec, and infra README. Note Cache/Messaging is Phase 3.

- [x] **Step 4: Commit**

```bash
git add infra/README.md .github/workflows/ci.yml README.md docs/ENGINEERING_GUIDELINES.md docs/superpowers/
git commit -m "$(cat <<'EOF'
docs: document AWS infra usage and production-gated ECR publish

EOF
)"
```

---



### Task 7: Verification gate (before claiming Phase 2 coding done)

- [ ] **Step 1: CI green on GitHub** for a PR from `feature/ci-cd-implementation`

- [ ] **Step 2:** `terraform validate` **succeeds** under `infra/` (with backend/role examples in place)

- [ ] **Step 3: Confirm Phase 3 items untouched** (no Redis/RabbitMQ code)

- [ ] **Step 4: Spec self-check** — every Decision D1–D9 in the design has a corresponding task or explicit “docs only” note

Only after Steps 1–3: mark Phase 2 CI/CD checklist items in `ENGINEERING_GUIDELINES.md` and open PR.

---



## Execution notes

- **Status:** Tasks 0–6 complete. **Next:** Task 7 (verification gate / PR).
- **Do not** `terraform apply` unless explicitly requested.
- Prefer small commits per task above.
- State bucket and IAM role `terraform-deploy-role` are **operator-owned prerequisites** for Task 3; the repo only wires Terraform to them.
- GitHub Environment `production` + OIDC role/variables are **operator-owned prerequisites** for the publish job; CI still runs without them.

