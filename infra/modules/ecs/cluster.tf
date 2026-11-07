resource "aws_service_discovery_http_namespace" "this" {
  name        = var.name_prefix
  description = "TaskFlow Service Connect"
}

resource "aws_ecs_cluster" "this" {
  name = var.name_prefix

  service_connect_defaults {
    namespace = aws_service_discovery_http_namespace.this.arn
  }

  tags = { Name = var.name_prefix }
}
