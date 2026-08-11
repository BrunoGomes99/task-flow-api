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
