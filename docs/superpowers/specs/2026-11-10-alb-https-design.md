# TaskFlow — ALB HTTPS Design

Date: 2026-11-10  
Status: **Approved, not started.** Branch, when the domain exists: `feature/alb-https`. Plan: [../plans/2026-11-10-alb-https.md](../plans/2026-11-10-alb-https.md).

Do not start this branch until an operator controls a DNS name for the API. The browser CORS policy is already specified in [2026-11-10-cors-design.md](2026-11-10-cors-design.md) and lands first, on `feature/cors`.

The ECS migration left HTTPS out on purpose. This design is that follow-up. It does not reopen the migration. See [2026-11-04-ecs-migration-design.md](2026-11-04-ecs-migration-design.md), non-goal "HTTPS, a custom domain, or an ACM certificate."

## Goal

Terminate TLS on the existing public ALB so a Cloudflare Pages site (HTTPS) can call the API without mixed content. The certificate is a public ACM certificate in the same region as the ALB. DNS stays at the operator's DNS host. This stack does not create a Route 53 zone.

## Non-goals

- A second load balancer, a CloudFront distribution, or TLS between the ALB and the API task.
- Changing the target group protocol, the container port `8080`, or the health check `GET /health/ready`.
- Setting `ASPNETCORE_HTTPS_PORT` or `ASPNETCORE_HTTPS_PORTS`. Removing `UseHttpsRedirection` from `Program.cs`. Inside the container the request stays HTTP, and the middleware has no HTTPS port, so it does not redirect. The target-group health check must keep receiving `200`.
- Proxying the API through Cloudflare (orange-cloud / Flexible SSL). The browser talks HTTPS to the ALB. The certificate name is the API domain.
- CORS policy code. Wiring a Pages origin is setting `cors_allowed_origins` in gitignored tfvars, which the CORS design already accepts.
- A certificate for the `*.elb.amazonaws.com` name. ACM will not issue one. The smoke check cannot call the raw ALB hostname under this certificate.
- A zone apex. The API name is a subdomain, so the operator can publish a CNAME.
- HSTS, a www redirect, or HTTP/2 configuration beyond the ALB default.

## Decisions (Approved)

### D1 — One subdomain, required after this branch

Variable `api_domain_name` is a string with no default. Example shape: `api.example.com`. It must contain at least two dots, so a typical apex such as `example.com` and a URL with a scheme are rejected. Apply fails closed when it is missing.

This branch stays unmerged until that name is real. After merge, every apply uses it. An empty string is not a supported "HTTP for now" mode.

### D2 — ACM DNS validation, records created by hand

Terraform creates `aws_acm_certificate` for `api_domain_name` with DNS validation, in `var.aws_region` (the ALB region). `aws_acm_certificate_validation` waits until the validation record answers.

Terraform outputs `acm_validation_records` (name, type, value). The operator creates those CNAMEs at the DNS host. Terraform does not create them, because the zone is not in this stack.

The operator also creates one CNAME from `api_domain_name` to `alb_dns_name`. That name is what browsers and the deploy smoke check use.

`terraform apply` blocks on validation until the ACM CNAME exists. That wait is the expected first apply, not a failure.

### D3 — Listeners

The target group stays HTTP on port 8080. Its health check stays `GET /health/ready`, matcher `200`.

| Listener | Action |
|---|---|
| 443 HTTPS | Forward to the existing API target group. Certificate is the validated ACM certificate. SSL policy `ELBSecurityPolicy-TLS13-1-2-2021-06`. |
| 80 HTTP | Redirect to HTTPS on 443 with status `HTTP_301`. It no longer forwards. |

The ALB security group keeps ingress 80 and gains ingress 443, both from `allowed_api_cidr`. Port 80 stays open so the redirect can run. Egress to the API on 8080 is unchanged.

`health_url` becomes `https://<api_domain_name>/health/ready`.

### D4 — Deploy smoke check uses the certificate name

`.github/workflows/ci.yml` today runs `curl -fsS "http://${ALB_DNS_NAME}/health/ready"`. After this change the curl is `curl -fsS "https://${API_DOMAIN_NAME}/health/ready"`.

`API_DOMAIN_NAME` is a GitHub Actions variable, the same hostname as `api_domain_name`, with no scheme. `ALB_DNS_NAME` remains documented for operators who need the raw balancer name. The smoke check does not use it: the certificate does not match that name, and curl would fail the TLS handshake.

A redirect check, run by hand after the first apply: `curl -sI http://<api_domain_name>/health/ready` returns `301` and a `Location` on `https://<api_domain_name>/`.

### D5 — Cost

The ACM public certificate costs nothing. The new listener is on the ALB that already exists. There is no second hourly ALB charge. NAT Gateway and the existing ALB hour remain the costs that `terraform destroy` stops.

### D6 — Pages origin is tfvars, not this design's code

When the Pages hostname exists, the same apply that turns on HTTPS sets `cors_allowed_origins` to that exact origin, for example `https://your-project.pages.dev`. The CORS branch has to be merged first, because this branch only consumes that variable.

## Success criteria

- [ ] `api_domain_name` is required. The certificate's domain is that name, in the stack region.
- [ ] Apply outputs the ACM validation CNAME and does not create a hosted zone.
- [ ] Port 443 forwards to the existing target group. Port 80 responds `301` to HTTPS.
- [ ] The target group and its health check stay HTTP on port 8080.
- [ ] The API task does not gain `ASPNETCORE_HTTPS_PORT` or `ASPNETCORE_HTTPS_PORTS`.
- [ ] `health_url` and the deploy curl use `https://<api_domain_name>/health/ready`.
- [ ] Ingress 443 is limited to `allowed_api_cidr`. Workers, Redis, and RabbitMQ stay private.
