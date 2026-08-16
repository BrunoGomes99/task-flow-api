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

variable "public_subnet_cidr" {
  description = "CIDR block for the public subnet."
  type        = string
  default     = "10.0.1.0/24"
}

variable "allowed_ssh_cidr" {
  description = "CIDR allowed to access SSH. An empty value keeps SSH disabled."
  type        = string
  default     = ""

  validation {
    condition     = var.allowed_ssh_cidr == "" || can(cidrhost(var.allowed_ssh_cidr, 0))
    error_message = "allowed_ssh_cidr must be empty or a valid CIDR block."
  }
}

variable "allowed_api_cidr" {
  description = "CIDR allowed to reach the API on TCP 8080. Default is open for study; restrict to your IP in tfvars when possible."
  type        = string
  default     = "0.0.0.0/0"

  validation {
    condition     = can(cidrhost(var.allowed_api_cidr, 0))
    error_message = "allowed_api_cidr must be a valid CIDR block."
  }
}

variable "ecr_repository_name" {
  description = "Name of the ECR repository for the TaskFlow API image."
  type        = string
  default     = "taskflow-api"
}

variable "instance_type" {
  description = "EC2 instance type for the API + Mongo host."
  type        = string
  default     = "t3.micro"
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

variable "mongo_root_username" {
  description = "MongoDB root username used on the instance (not published to the internet)."
  type        = string
  default     = "taskflow"
}

variable "mongo_root_password" {
  description = "MongoDB root password. Do not commit real values."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.mongo_root_password) >= 12
    error_message = "mongo_root_password must be at least 12 characters."
  }
}
