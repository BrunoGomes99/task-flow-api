output "vpc_id" {
  description = "ID of the TaskFlow VPC."
  value       = module.network.vpc_id
}

output "public_subnet_id" {
  description = "ID of the public subnet."
  value       = module.network.public_subnet_id
}

output "ecr_repository_url" {
  description = "URL of the ECR repository for the API image."
  value       = module.ecr.repository_url
}

output "ecr_repository_name" {
  description = "Name of the ECR repository for the API image."
  value       = module.ecr.repository_name
}

output "public_ip" {
  description = "Public IPv4 address of the EC2 API host."
  value       = module.compute.public_ip
}

output "instance_id" {
  description = "EC2 instance ID (SSM redeploy target; temporary until ECS)."
  value       = module.compute.instance_id
}

output "health_url" {
  description = "Suggested health check URL for the API."
  value       = "http://${module.compute.public_ip}:8080/health"
}
