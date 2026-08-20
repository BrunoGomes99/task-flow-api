data "aws_availability_zones" "available" {
  state = "available"
}

module "network" {
  source = "./modules/network"

  name_prefix        = var.name_prefix
  vpc_cidr           = var.vpc_cidr
  public_subnet_cidr = var.public_subnet_cidr
  az                 = data.aws_availability_zones.available.names[0]
}

module "ecr" {
  source = "./modules/ecr"

  repository_name = var.ecr_repository_name
}

module "compute" {
  source = "./modules/compute"

  name_prefix         = var.name_prefix
  aws_region          = var.aws_region
  vpc_id              = module.network.vpc_id
  public_subnet_id    = module.network.public_subnet_id
  ecr_repository_url  = module.ecr.repository_url
  ecr_repository_arn  = module.ecr.repository_arn
  allowed_api_cidr    = var.allowed_api_cidr
  allowed_ssh_cidr    = var.allowed_ssh_cidr
  instance_type       = var.instance_type
  api_image_tag       = var.api_image_tag
  jwt_secret          = var.jwt_secret
  jwt_issuer          = var.jwt_issuer
  jwt_audience        = var.jwt_audience
  mongo_root_username = var.mongo_root_username
  mongo_root_password = var.mongo_root_password
}
