variable "name_prefix" {
  description = "Prefix applied to network resource names and tags."
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
}

variable "public_subnet_cidr" {
  description = "CIDR block for the public subnet."
  type        = string
}

variable "az" {
  description = "Availability Zone for the public subnet."
  type        = string
}
