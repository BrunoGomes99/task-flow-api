# TaskFlow infrastructure

Terraform configuration for the TaskFlow AWS infrastructure.

## Validate locally

```powershell
terraform init
terraform validate
```

The configuration currently provisions only the public network foundation. Do not run `terraform apply` without reviewing the planned resources.
