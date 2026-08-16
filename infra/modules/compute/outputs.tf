output "public_ip" {
  description = "Public IPv4 address of the API host."
  value       = aws_instance.this.public_ip
}

output "instance_id" {
  description = "ID of the EC2 instance."
  value       = aws_instance.this.id
}

output "security_group_id" {
  description = "ID of the instance security group."
  value       = aws_security_group.this.id
}
