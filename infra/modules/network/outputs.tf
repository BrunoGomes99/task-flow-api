output "vpc_id" {
  description = "ID of the VPC."
  value       = aws_vpc.this.id
}

output "public_subnet_id" {
  description = "First public subnet. Kept until the EC2 module is removed."
  value       = aws_subnet.public.id
}

output "public_subnet_ids" {
  description = "Both public subnet IDs, for the ALB."
  value       = [aws_subnet.public.id, aws_subnet.public_b.id]
}

output "private_subnet_ids" {
  description = "Private subnet IDs. Depends on the default route to the NAT Gateway."
  value       = [aws_subnet.private_a.id, aws_subnet.private_b.id]
  depends_on = [
    aws_route_table_association.private_a,
    aws_route_table_association.private_b,
  ]
}

output "nat_public_ip" {
  description = "Elastic IP of the NAT Gateway. This is the Atlas allowlist entry."
  value       = aws_eip.nat.public_ip
}
