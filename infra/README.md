# TaskFlow infrastructure

Terraform configuration for the TaskFlow AWS study environment (minimal public VPC for now; ECR and EC2 follow in later tasks).

## Prerequisites

- [Terraform](https://developer.hashicorp.com/terraform/install) `>= 1.10` (S3 native state locking via `use_lockfile`)
- [AWS CLI](https://aws.amazon.com/cli/) with base credentials (SSO profile or keys) that can `sts:AssumeRole` on `terraform-deploy-role`
- An IAM role named **`terraform-deploy-role`** (or equivalent) with deploy permissions for this stack
- An **S3 bucket for Terraform state**, created **outside** this root (versioning on, block public access, encryption). This stack does not manage the state bucket.

## Auth (`assume_role`)

The AWS provider assumes your deploy role. Copy the example tfvars and set the real ARN:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars → terraform_deploy_role_arn = "arn:aws:iam::ACCOUNT_ID:role/terraform-deploy-role"
```

`terraform.tfvars` is gitignored. Do not commit real account IDs or secrets.

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

## Validate locally

Without a real bucket yet, you can validate configuration syntax with a local backend skip:

```powershell
terraform init -backend=false
terraform validate
```

With bucket + role configured:

```powershell
terraform init -backend-config=backend.hcl
terraform validate
```

Do not run `terraform apply` without reviewing `terraform plan`. Destroy the environment when idle to avoid cost.
