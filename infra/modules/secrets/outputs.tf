output "arns" {
  description = "Secret ARNs keyed by atlas, jwt, rabbitmq, brevo_api_key, brevo_sender_email, grafana_headers."
  value       = { for key, secret in aws_secretsmanager_secret.this : key => secret.arn }
}
