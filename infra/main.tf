data "aws_availability_zones" "available" {
  state = "available"
}

module "network" {
  source = "./modules/network"

  name_prefix          = var.name_prefix
  vpc_cidr             = var.vpc_cidr
  azs                  = slice(data.aws_availability_zones.available.names, 0, 2)
  public_subnet_cidrs  = var.public_subnet_cidrs
  private_subnet_cidrs = var.private_subnet_cidrs
}

module "ecr" {
  source = "./modules/ecr"

  repository_name = var.ecr_repository_name
}

module "ecr_scheduler" {
  source = "./modules/ecr"

  repository_name = var.ecr_scheduler_repository_name
}

module "ecr_notifications" {
  source = "./modules/ecr"

  repository_name = var.ecr_notifications_repository_name
}

module "security" {
  source = "./modules/security"

  name_prefix      = var.name_prefix
  vpc_id           = module.network.vpc_id
  allowed_api_cidr = var.allowed_api_cidr
}

module "secrets" {
  source = "./modules/secrets"

  name_prefix             = var.name_prefix
  atlas_connection_string = var.atlas_connection_string
  jwt_secret              = var.jwt_secret
  rabbitmq_password       = var.rabbitmq_password
  brevo_api_key           = var.brevo_api_key
  brevo_sender_email      = var.brevo_sender_email
  grafana_otlp_headers    = var.grafana_otlp_headers
}

module "ecs" {
  source = "./modules/ecs"

  name_prefix             = var.name_prefix
  aws_region              = var.aws_region
  vpc_id                  = module.network.vpc_id
  public_subnet_ids       = module.network.public_subnet_ids
  private_subnet_ids      = module.network.private_subnet_ids
  grafana_otlp_endpoint   = var.grafana_otlp_endpoint
  adot_image              = var.adot_image
  adot_config             = file("${path.module}/adot/collector.yaml")
  jwt_issuer              = var.jwt_issuer
  jwt_audience            = var.jwt_audience
  cors_allowed_origins    = var.cors_allowed_origins
  rabbitmq_user           = var.rabbitmq_user
  brevo_sender_name       = var.brevo_sender_name
  api_image               = "${module.ecr.repository_url}:${var.api_image_tag}"
  scheduler_image         = "${module.ecr_scheduler.repository_url}:${var.scheduler_image_tag}"
  notifications_image     = "${module.ecr_notifications.repository_url}:${var.notifications_image_tag}"
  api_image_tag           = var.api_image_tag
  scheduler_image_tag     = var.scheduler_image_tag
  notifications_image_tag = var.notifications_image_tag
  secret_arns             = module.secrets.arns

  security_group_ids = {
    alb           = module.security.alb_security_group_id
    api           = module.security.api_security_group_id
    scheduler     = module.security.scheduler_security_group_id
    notifications = module.security.notifications_security_group_id
    redis         = module.security.redis_security_group_id
    rabbitmq      = module.security.rabbitmq_security_group_id
  }
}
