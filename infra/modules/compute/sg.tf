resource "aws_security_group" "this" {
  name        = "${var.name_prefix}-ec2"
  description = "TaskFlow API host. MongoDB is not exposed to the internet."
  vpc_id      = var.vpc_id

  ingress {
    description = "TaskFlow API HTTP"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = [var.allowed_api_cidr]
  }

  dynamic "ingress" {
    for_each = trimspace(var.allowed_ssh_cidr) != "" ? [var.allowed_ssh_cidr] : []
    content {
      description = "SSH from operator CIDR"
      from_port   = 22
      to_port     = 22
      protocol    = "tcp"
      cidr_blocks = [ingress.value]
    }
  }

  egress {
    description = "All egress (ECR pull, Mongo image pull, packages)"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.name_prefix}-ec2-sg"
  }
}
