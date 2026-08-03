# TaskFlow — CI/CD + EC2 Design

Date: 2026-08-02  
Branch (implementation): `feature/ci-cd-implementation`  
Status: **Approved** (design). Implementation tracked in the companion plan.

## Goal

Invert the previous Phase 2 / Phase 3 order so **CI/CD comes before Redis/RabbitMQ**, enabling early feedback from GitHub Actions and a low-cost, destroyable AWS study environment.

This cycle delivers:

1. **CI (to implement):** GitHub Actions restore → build → test  
2. **CD (design + Terraform scaffold; no mandatory `apply`):** ECR + EC2 + Docker Compose (API + Mongo on the same instance)  
3. **Docs:** phase reorder, README, evolution path toward ECS  

## Non-goals (this cycle)

- Redis, RabbitMQ, NotificationLog persistence  
- Mandatory AWS provisioning / spending (`terraform apply` is optional and manual)  
- App Runner, ECS Fargate, ALB, DocumentDB (ECS is a **future** evolution only)  
- Production TLS, multi-AZ, autoscaling  

## Decisions (Approved)

### D1 — Phase reorder

| New order | Focus |
|-----------|--------|
| Phase 1 | MVP (done) |
| **Phase 2** | **CI/CD** (GitHub Actions, ECR, Terraform/EC2 design) |
| **Phase 3** | Cache and Messaging (Redis, RabbitMQ, NotificationLog) |

### D2 — Branch name

`feature/ci-cd-implementation`

### D3 — CI first, CD as design + scaffold

- Implement `.github/workflows/ci.yml` so every push/PR validates the solution.  
- Author Terraform under `infra/` and CD docs; **do not require** `terraform apply` to close the phase.  
- Optional later: `workflow_dispatch` CD workflow to build/push the image to ECR.

### D4 — Compute: EC2 + Docker (not App Runner)

Chosen for learning company-style EC2 patterns and as a stepping stone to **ECS** later.

- Amazon Linux 2023 on a small instance (`t4g.nano` or `t3.micro`)  
- Docker + Compose on the host  
- Instance profile with ECR pull permissions  

### D5 — MongoDB on the same EC2

`docker-compose.aws.yml` runs **API (image from ECR) + MongoDB** on one instance.

- Mongo port **not** exposed to the internet (compose network only)  
- API port exposed via Security Group (e.g. 8080 or 80)  
- SSH (22) optional and restricted to the operator’s IP  

### D6 — Network: minimal public VPC

- 1 VPC, 1 public subnet, Internet Gateway (no NAT Gateway — cost control)  
- Easy `terraform destroy` to tear everything down  

### D7 — Secrets

- No secrets in git or workflow YAML  
- JWT / Mongo credentials via Terraform variables / generated `.env` on the instance (never committed)  
- GitHub Actions CI needs **no** cloud secrets for the default test job  

### D8 — Future evolution: ECS

Documented only in this cycle:

- Keep building the **same** API image into ECR  
- Later: ECS task definition + Fargate service + ALB  
- Move Mongo off the EC2 host to a managed store when adopting ECS  

## Architecture

```text
Developer → GitHub (PR/push)
              │
              ▼
     GitHub Actions CI
     restore / build / test
              │
              │  (later / manual CD)
              ▼
     Docker build → Amazon ECR
              │
              ▼
     EC2 (user-data)
       docker compose up
         ├─ taskflow.api  (ECR image)
         └─ mongo         (local volume)
```

## Repository layout (target)

```text
.github/workflows/
  ci.yml
  cd.yml                    # optional skeleton: build/push ECR
infra/
  README.md
  versions.tf
  providers.tf
  variables.tf
  outputs.tf
  main.tf
  modules/
    network/
    ecr/
    compute/
  templates/
    user-data.sh.tpl
  compose/
    docker-compose.aws.yml
docs/superpowers/specs/
  2026-08-02-ci-cd-ec2-design.md
docs/superpowers/plans/
  2026-08-02-ci-cd-implementation.md
```

## CI requirements

- Triggers: `push` and `pull_request` to `main` (and feature branches as needed)  
- .NET 10 SDK  
- `dotnet restore` → `dotnet build -c Release` → `dotnet test -c Release`  
- Fail the job if build or tests fail  
- NuGet cache recommended  
- No hardcoded secrets  

## CD / Terraform requirements (scaffold)

| Component | Responsibility |
|-----------|----------------|
| `modules/network` | VPC, subnet, IGW, route table |
| `modules/ecr` | ECR repository `taskflow-api` |
| `modules/compute` | SG, IAM instance profile, EC2, user-data |
| `compose/docker-compose.aws.yml` | API + Mongo on host |
| `infra/README.md` | apply/destroy, cost notes, SSH guidance |

**Outputs (examples):** ECR repository URL, public IP / DNS, suggested health URL (`http://<ip>:8080/health`).

## Success criteria

- [ ] Phase numbers updated in Engineering Guidelines and Project Spec  
- [ ] Design + implementation plan committed in `docs/superpowers/`  
- [ ] CI workflow green on PR (when implementation starts)  
- [ ] `infra/` present and documented; `terraform validate` passes locally when Terraform is installed  
- [ ] Operator can apply and destroy without leftover billable resources (documented checklist)  

## Out of scope reminders

Cache/messaging remain **Phase 3**. Do not add Redis/RabbitMQ to AWS compose in this cycle.
