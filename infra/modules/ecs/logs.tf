locals {
  log_names = toset(["api", "scheduler", "notifications", "redis", "rabbitmq"])
}

resource "aws_cloudwatch_log_group" "this" {
  for_each          = local.log_names
  name              = "/ecs/${var.name_prefix}-${each.key}"
  retention_in_days = 7
}
