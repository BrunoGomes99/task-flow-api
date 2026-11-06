output "alb_dns_name" {
  value = aws_lb.api.dns_name
}

output "cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "api_service_name" {
  value = aws_ecs_service.api.name
}

output "scheduler_service_name" {
  value = aws_ecs_service.scheduler.name
}

output "notifications_service_name" {
  value = aws_ecs_service.notifications.name
}
