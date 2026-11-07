variable "name_prefix" {
  description = "Prefix applied to network resource names and tags."
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
}

variable "azs" {
  description = "Two availability zones. Index 0 keeps the existing public subnet."
  type        = list(string)

  validation {
    condition     = length(var.azs) == 2
    error_message = "azs must contain exactly two availability zones."
  }
}

variable "public_subnet_cidrs" {
  description = "Public subnet CIDRs, one per AZ. Index 0 is the existing subnet."
  type        = list(string)

  validation {
    condition     = length(var.public_subnet_cidrs) == 2
    error_message = "public_subnet_cidrs must contain two CIDRs."
  }
}

variable "private_subnet_cidrs" {
  description = "Private subnet CIDRs, one per AZ."
  type        = list(string)

  validation {
    condition     = length(var.private_subnet_cidrs) == 2
    error_message = "private_subnet_cidrs must contain two CIDRs."
  }
}
