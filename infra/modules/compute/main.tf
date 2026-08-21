data "aws_ami" "al2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-2023*-x86_64"]
  }

  filter {
    name   = "architecture"
    values = ["x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }

  filter {
    name   = "root-device-type"
    values = ["ebs"]
  }
}

locals {
  compose_yaml = file("${path.root}/compose/docker-compose.aws.yml")
  env_file = join("\n", [
    "API_IMAGE=${var.ecr_repository_url}:${var.api_image_tag}",
    "MONGO_ROOT_USERNAME=${var.mongo_root_username}",
    "MONGO_ROOT_PASSWORD=${var.mongo_root_password}",
    "JWT_SECRET=${var.jwt_secret}",
    "JWT_ISSUER=${var.jwt_issuer}",
    "JWT_AUDIENCE=${var.jwt_audience}",
    "",
  ])
  user_data_raw = templatefile("${path.root}/templates/user-data.sh.tpl", {
    aws_region   = var.aws_region
    ecr_registry = split("/", var.ecr_repository_url)[0]
    compose_b64  = base64encode(local.compose_yaml)
    env_b64      = base64encode(local.env_file)
  })
  user_data = replace(replace(local.user_data_raw, "\r\n", "\n"), "\r", "\n")
}

resource "aws_instance" "this" {
  ami                         = data.aws_ami.al2023.id
  instance_type               = var.instance_type
  subnet_id                   = var.public_subnet_id
  vpc_security_group_ids      = [aws_security_group.this.id]
  iam_instance_profile        = aws_iam_instance_profile.this.name
  associate_public_ip_address = true
  user_data                   = local.user_data
  user_data_replace_on_change = false

  root_block_device {
    volume_size = 8
    volume_type = "gp3"
    encrypted   = true
  }

  metadata_options {
    http_endpoint = "enabled"
    http_tokens   = "required"
  }

  tags = {
    Name             = "${var.name_prefix}-api"
    TaskFlowRedeploy = "enabled"
  }
}
