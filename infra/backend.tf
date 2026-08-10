terraform {
  backend "s3" {
    # bucket, key, and region are supplied via -backend-config=backend.hcl
    # (see backend.hcl.example). Do not commit a real backend.hcl.
    encrypt      = true
    use_lockfile = true
  }
}
