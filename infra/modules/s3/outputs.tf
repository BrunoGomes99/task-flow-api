output "logo_url" {
  description = "Public HTTPS URL of the reminder logo (email/logo.png)."
  value       = "https://${aws_s3_bucket.this.bucket_regional_domain_name}/${aws_s3_object.logo.key}"
}
