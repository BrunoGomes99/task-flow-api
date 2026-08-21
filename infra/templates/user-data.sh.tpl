#!/bin/bash
set -euo pipefail
exec > >(tee /var/log/taskflow-user-data.log) 2>&1

echo "TaskFlow user-data bootstrap starting"

# Create app dir early so SSM/redeploy can find it even if a later step fails.
install -d -m 0755 /opt/taskflow

# Amazon Linux 2023 ships "docker" in Amazon repos; "docker-compose-plugin" does not.
dnf install -y docker
if ! command -v aws >/dev/null 2>&1; then
  dnf install -y awscli
fi

# Compose V2 CLI plugin (official GitHub release; pinned for reproducible boots).
COMPOSE_VERSION="v2.32.4"
install -d -m 0755 /usr/local/lib/docker/cli-plugins
curl -fsSL "https://github.com/docker/compose/releases/download/$${COMPOSE_VERSION}/docker-compose-linux-$(uname -m)" \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

systemctl enable --now docker

until docker info >/dev/null 2>&1; do
  echo "Waiting for Docker daemon..."
  sleep 2
done

docker compose version

echo '${compose_b64}' | base64 -d > /opt/taskflow/docker-compose.yml
echo '${env_b64}' | base64 -d > /opt/taskflow/.env
chmod 600 /opt/taskflow/.env

ECR_LOGIN_OK=0
for i in $(seq 1 10); do
  if aws ecr get-login-password --region '${aws_region}' \
    | docker login --username AWS --password-stdin '${ecr_registry}'; then
    ECR_LOGIN_OK=1
    break
  fi
  echo "ECR login attempt $${i} failed; retrying..."
  sleep 3
done

if [ "$${ECR_LOGIN_OK}" -ne 1 ]; then
  echo "WARN: could not authenticate to ECR; leaving compose files in place"
  exit 0
fi

cd /opt/taskflow
if docker compose pull && docker compose up -d; then
  echo "TaskFlow compose stack is up"
else
  echo "WARN: docker compose up failed (API image may not exist in ECR yet). Files remain in /opt/taskflow"
fi

echo "TaskFlow user-data bootstrap finished"
