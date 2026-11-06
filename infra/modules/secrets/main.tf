locals {
  secrets = {
    atlas              = var.atlas_connection_string
    jwt                = var.jwt_secret
    rabbitmq           = var.rabbitmq_password
    brevo_api_key      = var.brevo_api_key
    brevo_sender_email = var.brevo_sender_email
    grafana_headers    = var.grafana_otlp_headers
  }
}

resource "aws_secretsmanager_secret" "this" {
  for_each = local.secrets
  name     = "${var.name_prefix}/${each.key}"

  tags = { Name = "${var.name_prefix}-${each.key}" }
}

resource "aws_secretsmanager_secret_version" "this" {
  for_each      = local.secrets
  secret_id     = aws_secretsmanager_secret.this[each.key].id
  secret_string = each.value
}
