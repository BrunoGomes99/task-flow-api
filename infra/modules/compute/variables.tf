variable "name_prefix" {
  description = "Prefix applied to compute resource names and tags."
  type        = string
}

variable "aws_region" {
  description = "AWS region (used by user-data for ECR login)."
  type        = string
}

variable "vpc_id" {
  description = "VPC that hosts the instance security group."
  type        = string
}

variable "public_subnet_id" {
  description = "Public subnet for the EC2 instance."
  type        = string
}

variable "ecr_repository_url" {
  description = "ECR repository URL for the API image (registry/name)."
  type        = string
}

variable "ecr_repository_arn" {
  description = "ECR repository ARN used to scope pull permissions."
  type        = string
}

variable "allowed_api_cidr" {
  description = "CIDR allowed to reach the API on TCP 8080."
  type        = string
}

variable "allowed_ssh_cidr" {
  description = "CIDR allowed to access SSH. An empty value keeps SSH disabled."
  type        = string
  default     = ""
}

variable "instance_type" {
  description = "EC2 instance type. Default is a small x86 study size matching the Dockerfile architecture."
  type        = string
}

variable "api_image_tag" {
  description = "Tag of the API image to pull from ECR on first boot."
  type        = string
}

variable "jwt_secret" {
  description = "Symmetric JWT signing key (at least 32 UTF-8 bytes). Written to the instance .env only."
  type        = string
  sensitive   = true
}

variable "jwt_issuer" {
  description = "Jwt:Issuer value passed to the API container."
  type        = string
}

variable "jwt_audience" {
  description = "Jwt:Audience value passed to the API container."
  type        = string
}

variable "mongo_root_username" {
  description = "MongoDB root username (compose network only; not published on the host)."
  type        = string
}

variable "mongo_root_password" {
  description = "MongoDB root password. Written to the instance .env only."
  type        = string
  sensitive   = true
}
