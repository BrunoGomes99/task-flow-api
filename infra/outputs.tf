output "vpc_id" {
  description = "ID of the TaskFlow VPC."
  value       = module.network.vpc_id
}

output "ecr_repository_url" {
  description = "URL of the ECR repository for the API image."
  value       = module.ecr.repository_url
}

output "ecr_scheduler_repository_url" {
  description = "URL of the ECR repository for the scheduler image."
  value       = module.ecr_scheduler.repository_url
}

output "ecr_notifications_repository_url" {
  description = "URL of the ECR repository for the notifications image."
  value       = module.ecr_notifications.repository_url
}

output "ecr_repository_name" {
  description = "Name of the ECR repository for the API image."
  value       = module.ecr.repository_name
}

output "alb_dns_name" {
  description = "Public DNS name of the API load balancer."
  value       = module.ecs.alb_dns_name
}

output "nat_public_ip" {
  description = "NAT Elastic IP to place on the Atlas network access list."
  value       = module.network.nat_public_ip
}

output "health_url" {
  description = "API readiness URL."
  value       = "http://${module.ecs.alb_dns_name}/health/ready"
}

output "cluster_name" {
  description = "ECS cluster name."
  value       = module.ecs.cluster_name
}
