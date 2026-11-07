locals {
  dotnet_base_env = [
    { name = "ASPNETCORE_HTTP_PORTS", value = "8080" },
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "MongoDb__DatabaseName", value = "TaskFlowDb" },
    { name = "OpenTelemetry__Enabled", value = "true" },
    { name = "OpenTelemetry__OtlpEndpoint", value = "http://localhost:4318" },
    { name = "OpenTelemetry__Environment", value = "aws" },
  ]

  dotnet = {
    api = {
      container = "taskflow-api"
      image     = var.api_image
      tag       = var.api_image_tag
      environment = concat(local.dotnet_base_env, [
        { name = "EnableSwagger", value = "true" },
        { name = "Redis__ConnectionString", value = "redis:6379,abortConnect=false" },
        { name = "Jwt__Issuer", value = var.jwt_issuer },
        { name = "Jwt__Audience", value = var.jwt_audience },
        { name = "SERVICE_VERSION", value = var.api_image_tag },
      ])
      secrets = [
        { name = "MongoDb__ConnectionString", valueFrom = var.secret_arns["atlas"] },
        { name = "Jwt__Secret", valueFrom = var.secret_arns["jwt"] },
      ]
    }
    scheduler = {
      container = "taskflow-scheduler"
      image     = var.scheduler_image
      tag       = var.scheduler_image_tag
      environment = concat(local.dotnet_base_env, [
        { name = "RabbitMq__Host", value = "rabbitmq" },
        { name = "RabbitMq__Port", value = "5672" },
        { name = "RabbitMq__User", value = var.rabbitmq_user },
        { name = "SERVICE_VERSION", value = var.scheduler_image_tag },
      ])
      secrets = [
        { name = "MongoDb__ConnectionString", valueFrom = var.secret_arns["atlas"] },
        { name = "RabbitMq__Password", valueFrom = var.secret_arns["rabbitmq"] },
      ]
    }
    notifications = {
      container = "taskflow-notifications"
      image     = var.notifications_image
      tag       = var.notifications_image_tag
      environment = concat(local.dotnet_base_env, [
        { name = "RabbitMq__Host", value = "rabbitmq" },
        { name = "RabbitMq__Port", value = "5672" },
        { name = "RabbitMq__User", value = var.rabbitmq_user },
        { name = "Brevo__SenderName", value = var.brevo_sender_name },
        { name = "SERVICE_VERSION", value = var.notifications_image_tag },
      ])
      secrets = [
        { name = "MongoDb__ConnectionString", valueFrom = var.secret_arns["atlas"] },
        { name = "RabbitMq__Password", valueFrom = var.secret_arns["rabbitmq"] },
        { name = "Brevo__ApiKey", valueFrom = var.secret_arns["brevo_api_key"] },
        { name = "Brevo__SenderEmail", valueFrom = var.secret_arns["brevo_sender_email"] },
      ]
    }
  }
}

resource "aws_ecs_task_definition" "dotnet" {
  for_each                 = local.dotnet
  family                   = "${var.name_prefix}-${each.key}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.execution.arn

  container_definitions = jsonencode([
    {
      name      = each.value.container
      image     = each.value.image
      essential = true
      portMappings = [{ name = "http", containerPort = 8080, protocol = "tcp" }]
      environment = each.value.environment
      secrets     = each.value.secrets
      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://127.0.0.1:8080/health/live || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.this[each.key].name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = each.value.container
        }
      }
    },
    {
      name      = "adot"
      image     = var.adot_image
      essential = false
      environment = [
        { name = "AOT_CONFIG_CONTENT", value = var.adot_config },
        { name = "GRAFANA_OTLP_ENDPOINT", value = var.grafana_otlp_endpoint },
        { name = "SERVICE_VERSION", value = each.value.tag },
      ]
      secrets = [
        { name = "GRAFANA_OTLP_HEADERS", valueFrom = var.secret_arns["grafana_headers"] },
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.this[each.key].name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "adot"
        }
      }
    },
  ])

  lifecycle {
    ignore_changes = [container_definitions]
  }
}

resource "aws_ecs_task_definition" "redis" {
  family                   = "${var.name_prefix}-redis"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.execution.arn

  container_definitions = jsonencode([{
    name      = "redis"
    image     = "redis:7.4-alpine"
    essential = true
    command   = ["redis-server", "--save", "", "--appendonly", "no", "--maxmemory", "64mb", "--maxmemory-policy", "allkeys-lru"]
    portMappings = [{ name = "redis", containerPort = 6379, protocol = "tcp" }]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.this["redis"].name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "redis"
      }
    }
  }])
}

resource "aws_ecs_task_definition" "rabbitmq" {
  family                   = "${var.name_prefix}-rabbitmq"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.execution.arn

  container_definitions = jsonencode([{
    name      = "rabbitmq"
    image     = "rabbitmq:4.1-alpine"
    essential = true
    portMappings = [{ name = "rabbitmq", containerPort = 5672, protocol = "tcp" }]
    environment = [{ name = "RABBITMQ_DEFAULT_USER", value = var.rabbitmq_user }]
    secrets     = [{ name = "RABBITMQ_DEFAULT_PASS", valueFrom = var.secret_arns["rabbitmq"] }]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.this["rabbitmq"].name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "rabbitmq"
      }
    }
  }])
}
