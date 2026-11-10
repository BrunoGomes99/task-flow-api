variable "name_prefix" { type = string }
variable "aws_region" { type = string }
variable "vpc_id" { type = string }
variable "public_subnet_ids" { type = list(string) }
variable "private_subnet_ids" { type = list(string) }
variable "security_group_ids" { type = map(string) }
variable "secret_arns" { type = map(string) }
variable "api_image" { type = string }
variable "scheduler_image" { type = string }
variable "notifications_image" { type = string }
variable "api_image_tag" { type = string }
variable "scheduler_image_tag" { type = string }
variable "notifications_image_tag" { type = string }
variable "adot_image" { type = string }
variable "adot_config" { type = string }
variable "grafana_otlp_endpoint" { type = string }
variable "jwt_issuer" { type = string }
variable "jwt_audience" { type = string }
variable "cors_allowed_origins" {
  type    = list(string)
  default = []
}
variable "rabbitmq_user" { type = string }
variable "brevo_sender_name" { type = string }
