variable "aws_region" {
  description = "AWS region where infrastructure is created."
  type        = string
  default     = "us-east-1"
}

variable "terraform_deploy_role_arn" {
  description = "ARN of the IAM role Terraform assumes for all AWS API calls (e.g. terraform-deploy-role)."
  type        = string
}

variable "name_prefix" {
  description = "Prefix applied to resource names and tags."
  type        = string
  default     = "taskflow"
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
  default     = "10.0.0.0/16"
}

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

variable "allowed_api_cidr" {
  description = "CIDR allowed to reach the API load balancer on TCP 80. Default is open for study; restrict to your IP in tfvars when possible."
  type        = string
  default     = "0.0.0.0/0"

  validation {
    condition     = can(cidrhost(var.allowed_api_cidr, 0))
    error_message = "allowed_api_cidr must be a valid CIDR block."
  }
}

variable "cors_allowed_origins" {
  description = "Browser origins allowed to call the API. Empty allows no cross-origin browser call. Set the Cloudflare Pages origin when that hostname exists."
  type        = list(string)
  default     = []

  validation {
    condition = alltrue([
      for origin in var.cors_allowed_origins :
      can(regex("^https?://[A-Za-z0-9.-]+(:[0-9]+)?$", origin))
    ])
    error_message = "Each CORS origin must be an absolute http or https origin with no path, query, or trailing slash."
  }
}

variable "ecr_repository_name" {
  description = "Name of the ECR repository for the TaskFlow API image."
  type        = string
  default     = "taskflow-api"
}

variable "ecr_scheduler_repository_name" {
  description = "Name of the ECR repository for the scheduler image."
  type        = string
  default     = "taskflow-scheduler"
}

variable "ecr_notifications_repository_name" {
  description = "Name of the ECR repository for the notifications image."
  type        = string
  default     = "taskflow-notifications"
}

variable "api_image_tag" {
  description = "Tag of the API image to pull from ECR on first boot."
  type        = string
  default     = "latest"
}

variable "jwt_secret" {
  description = "Symmetric JWT signing key (at least 32 UTF-8 bytes). Do not commit real values."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.jwt_secret) >= 32
    error_message = "jwt_secret must be at least 32 characters."
  }
}

variable "jwt_issuer" {
  description = "Jwt:Issuer passed to the API container."
  type        = string
  default     = "TaskFlow"
}

variable "jwt_audience" {
  description = "Jwt:Audience passed to the API container."
  type        = string
  default     = "TaskFlow.Api"
}

variable "rabbitmq_user" {
  description = "RabbitMQ user on the Compose network."
  type        = string
  default     = "taskflow"

  validation {
    condition     = can(regex("^[A-Za-z0-9]+$", var.rabbitmq_user))
    error_message = "rabbitmq_user must contain only letters and digits."
  }
}

variable "rabbitmq_password" {
  description = "RabbitMQ password. Do not commit real values."
  type        = string
  sensitive   = true

  validation {
    condition     = can(regex("^[A-Za-z0-9]{12,}$", var.rabbitmq_password))
    error_message = "rabbitmq_password must be at least 12 letters or digits, with no spaces or symbols."
  }
}

variable "brevo_api_key" {
  description = "Brevo API key for the notifications worker. Do not commit real values."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.brevo_api_key) > 0
    error_message = "brevo_api_key must not be empty."
  }
}

variable "brevo_sender_email" {
  description = "Verified Brevo sender email address."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.brevo_sender_email))
    error_message = "brevo_sender_email must be an email address."
  }
}

variable "brevo_sender_name" {
  description = "Display name on reminder emails."
  type        = string
  default     = "TaskFlow"
}

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
