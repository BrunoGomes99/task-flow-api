# Task 2 Report: Terraform root and network module

## Status

Completed.

## Delivered

- Added the Terraform root configuration, AWS provider constraints, configurable study defaults, and root outputs.
- Added the `network` module with a VPC, one public subnet, an Internet Gateway, public route table, and route table association.
- Configured the subnet to assign public IPs at launch and routed `0.0.0.0/0` through the Internet Gateway.
- Did not add a NAT Gateway, ECR, compute infrastructure, Compose configuration, or CD workflow.
- Added Terraform local-state and crash-log exclusions to `.gitignore`.
- Included the generated provider dependency lock file in version control.

## Validation

Ran from `infra/`:

```powershell
terraform init
terraform validate
terraform fmt -check -recursive
```

Results:

- `terraform init` completed successfully using `hashicorp/aws` v5.100.0.
- `terraform validate` returned: `Success! The configuration is valid.`
- `terraform fmt -check -recursive` and `git diff --check` completed successfully.

No `terraform apply` was run.

## Self-review

- The module exposes `vpc_id` and `public_subnet_id`, which are re-exposed by the root configuration for later compute use.
- Resource names are derived from `name_prefix`.
- SSH remains disabled by default through `allowed_ssh_cidr = ""`; the variable is intentionally reserved for a later compute/security-group task.

## Commit

`f255b13 infra: add Terraform root and minimal public VPC module`

## Review finding fix

- Removed the root `az` variable with its fixed `us-east-1a` default.
- Added `data "aws_availability_zones" "available"` with `state = "available"` and pass its first AZ to the network module, so the subnet AZ follows `aws_region`.

Commands run from `infra/`:

```powershell
terraform fmt -recursive
terraform validate
git diff --check
```

Validate output:

```text
Success! The configuration is valid.
```

No `terraform apply` was run.
