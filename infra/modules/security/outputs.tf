output "alb_security_group_id" {
  value = aws_security_group.alb.id
}

output "api_security_group_id" {
  value = aws_security_group.api.id
}

output "scheduler_security_group_id" {
  value = aws_security_group.scheduler.id
}

output "notifications_security_group_id" {
  value = aws_security_group.notifications.id
}

output "redis_security_group_id" {
  value = aws_security_group.redis.id
}

output "rabbitmq_security_group_id" {
  value = aws_security_group.rabbitmq.id
}
