resource "aws_security_group" "alb" {
  name        = "${var.name_prefix}-alb"
  description = "Public HTTP entry for the TaskFlow API."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-alb" }
}

resource "aws_security_group" "api" {
  name        = "${var.name_prefix}-api"
  description = "TaskFlow API tasks. Ingress is the ALB only."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-api" }
}

resource "aws_security_group" "scheduler" {
  name        = "${var.name_prefix}-scheduler"
  description = "Scheduler tasks. No ingress."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-scheduler" }
}

resource "aws_security_group" "notifications" {
  name        = "${var.name_prefix}-notifications"
  description = "Notifications tasks. No ingress."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-notifications" }
}

resource "aws_security_group" "redis" {
  name        = "${var.name_prefix}-redis"
  description = "Redis tasks. Ingress is the API only."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-redis" }
}

resource "aws_security_group" "rabbitmq" {
  name        = "${var.name_prefix}-rabbitmq"
  description = "RabbitMQ tasks. Ingress is the workers only."
  vpc_id      = var.vpc_id

  tags = { Name = "${var.name_prefix}-rabbitmq" }
}

resource "aws_vpc_security_group_ingress_rule" "alb_http" {
  security_group_id = aws_security_group.alb.id
  description       = "HTTP from allowed_api_cidr"
  cidr_ipv4         = var.allowed_api_cidr
  from_port         = 80
  to_port           = 80
  ip_protocol       = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "alb_to_api" {
  security_group_id            = aws_security_group.alb.id
  description                  = "API targets"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "api_from_alb" {
  security_group_id            = aws_security_group.api.id
  description                  = "ALB"
  referenced_security_group_id = aws_security_group.alb.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "redis_from_api" {
  security_group_id            = aws_security_group.redis.id
  description                  = "API"
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 6379
  to_port                      = 6379
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "rabbit_from_scheduler" {
  security_group_id            = aws_security_group.rabbitmq.id
  description                  = "Scheduler"
  referenced_security_group_id = aws_security_group.scheduler.id
  from_port                    = 5672
  to_port                      = 5672
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "rabbit_from_notifications" {
  security_group_id            = aws_security_group.rabbitmq.id
  description                  = "Notifications"
  referenced_security_group_id = aws_security_group.notifications.id
  from_port                    = 5672
  to_port                      = 5672
  ip_protocol                  = "tcp"
}

locals {
  egress_groups = {
    api           = aws_security_group.api.id
    scheduler     = aws_security_group.scheduler.id
    notifications = aws_security_group.notifications.id
    redis         = aws_security_group.redis.id
    rabbitmq      = aws_security_group.rabbitmq.id
  }
}

resource "aws_vpc_security_group_egress_rule" "tasks" {
  for_each = local.egress_groups

  security_group_id = each.value
  description       = "Egress through the NAT Gateway"
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}
