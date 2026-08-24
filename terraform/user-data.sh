#!/bin/bash
# ==============================================================================
# user-data.sh - EC2 User Data Script
# Executed on EC2 instance startup to deploy RatanHR HRMS application
# ==============================================================================

set -euo pipefail

# Logging
exec > >(tee -a /var/log/user-data.log)
exec 2>&1

echo "========== RatanHR HRMS Deployment Started =========="
echo "Timestamp: $(date -Iseconds)"

# ==============================================================================
# STEP 1: System Updates
# ==============================================================================

echo "[$(date -Iseconds)] Step 1: Updating system packages..."
apt-get update
apt-get upgrade -y
apt-get install -y \
  curl \
  wget \
  git \
  ca-certificates \
  gnupg \
  lsb-release \
  apt-transport-https \
  vim \
  htop \
  net-tools \
  jq

# ==============================================================================
# STEP 2: Install Docker
# ==============================================================================

echo "[$(date -Iseconds)] Step 2: Installing Docker..."
mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Add ubuntu user to docker group
usermod -aG docker ubuntu

echo "[$(date -Iseconds)] Docker installed: $(docker --version)"

# ==============================================================================
# STEP 3: Create deployment directory
# ==============================================================================

echo "[$(date -Iseconds)] Step 3: Creating deployment directory..."
mkdir -p /opt/ratanhr
cd /opt/ratanhr
chown -R ubuntu:ubuntu /opt/ratanhr

# ==============================================================================
# STEP 4: Create environment file
# ==============================================================================

echo "[$(date -Iseconds)] Step 4: Creating .env file..."
cat > /opt/ratanhr/.env << 'EOF'
# Database
MYSQL_HOST=${mysql_host}
MYSQL_PORT=3306
MYSQL_USER=${mysql_user}
MYSQL_PASSWORD=${mysql_password}
MYSQL_DATABASE=${mysql_database}
MYSQL_ROOT_PASSWORD=${mysql_password}
ConnectionStrings__DefaultConnection=Server=$${MYSQL_HOST};Port=3306;Database=$${MYSQL_DATABASE};Uid=$${MYSQL_USER};Pwd=$${MYSQL_PASSWORD};AllowPublicKeyRetrieval=true;SslMode=Required;

# Redis
REDIS_HOST=${redis_host}
REDIS_PORT=6379
REDIS_PASSWORD=${redis_password}
REDIS_CONNECTION_STRING=$${REDIS_HOST}:6379,password=$${REDIS_PASSWORD},ssl=False,abortConnect=False

# Domain
DOMAIN_NAME=${domain_name}
APP_BASE_URL=https://$${DOMAIN_NAME}
ALLOWED_HOSTS=$${DOMAIN_NAME}
ALLOWED_ORIGINS=https://$${DOMAIN_NAME}

# JWT Keys
JWT_PRIVATE_KEY_PEM=${jwt_private}
JWT_PUBLIC_KEY_PEM=${jwt_public}

# Encryption
ENCRYPTION_KEY=${encryption_key}

# SMTP
EMAIL_HOST=${smtp_host}
EMAIL_PORT=${smtp_port}
EMAIL_USERNAME=${smtp_user}
EMAIL_PASSWORD=${smtp_password}
EMAIL_FROM_ADDRESS=noreply@$${DOMAIN_NAME}
EMAIL_FROM_NAME=RatanHR HRMS

# Environment
ASPNETCORE_ENVIRONMENT=Production
APP_ENV=production

# Backup
BACKUP_ENCRYPTION_KEY=${encryption_key}
BACKUP_RETAIN_DAYS=14
BACKUP_CRON_SCHEDULE=0 2 * * *

# Monitoring
OTEL_OTLP_ENDPOINT=http://localhost:4317
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=$(openssl rand -base64 16)

# Hangfire
Hangfire__UseRedis=true
Features__BiometricRealtime=false
EOF

chmod 600 /opt/ratanhr/.env
chown ubuntu:ubuntu /opt/ratanhr/.env

echo "[$(date -Iseconds)] Environment file created"

# ==============================================================================
# STEP 5: Clone or download docker-compose file
# ==============================================================================

echo "[$(date -Iseconds)] Step 5: Setting up Docker Compose..."

# RHR-011 FIX: this step previously wrote a placeholder comment instead of the
# real compose file, so Step 7 (`docker compose up -d`) ran against an empty
# file and deployed nothing -- every step after this one reported success
# while the application was never actually started. The real compose file is
# uploaded to S3 by Terraform (see aws_s3_object.compose_file in main.tf) and
# downloaded here via the AWS CLI, authenticated by this instance's existing
# IAM role (already has s3:GetObject on this bucket -- no new permissions
# needed). Embedding the file directly in user_data was rejected: it risks
# exceeding AWS's hard 16KB EC2 user-data size limit once combined with this
# script and the JWT PEM keys.
if ! command -v aws >/dev/null 2>&1; then
  echo "[$(date -Iseconds)] AWS CLI not found -- installing..."
  curl -fsSL "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o /tmp/awscliv2.zip
  cd /tmp && unzip -q awscliv2.zip && ./aws/install
  cd /opt/ratanhr
fi

aws s3 cp "s3://${backup_bucket}/${compose_s3_key}" /opt/ratanhr/docker-compose.prod.yml --region "${aws_region}"

if ! grep -q "services:" /opt/ratanhr/docker-compose.prod.yml; then
  echo "[$(date -Iseconds)] FATAL: docker-compose.prod.yml does not look like a valid compose file after download. Aborting deployment." >&2
  exit 1
fi

echo "[$(date -Iseconds)] docker-compose.prod.yml downloaded from S3 ($(wc -l < /opt/ratanhr/docker-compose.prod.yml) lines)"

# ==============================================================================
# STEP 6: Pull Docker images
# ==============================================================================

echo "[$(date -Iseconds)] Step 6: Pulling Docker images (this may take several minutes)..."
docker pull mysql:8.4
docker pull redis:7.4-alpine
docker pull clamav/clamav:1.3
docker pull nginx:1.27.0-alpine
docker pull certbot/certbot:v2.11.0

echo "[$(date -Iseconds)] Docker images pulled successfully"

# ==============================================================================
# STEP 7: Start application stack with docker-compose
# ==============================================================================

echo "[$(date -Iseconds)] Step 7: Starting Docker Compose stack..."
cd /opt/ratanhr

# Create directories for volumes
mkdir -p ./mysql-data ./redis-data ./backups ./uploads ./logs

# Start services
docker compose -f docker-compose.prod.yml up -d

echo "[$(date -Iseconds)] Docker Compose stack started"

# ==============================================================================
# STEP 8: Wait for services to be healthy
# ==============================================================================

echo "[$(date -Iseconds)] Step 8: Waiting for services to become healthy..."
max_attempts=60
attempt=0

while [ $attempt -lt $max_attempts ]; do
  echo "[$(date -Iseconds)] Health check attempt $((attempt + 1))/$max_attempts..."
  
  # Check if all services are running
  if docker compose -f docker-compose.prod.yml ps | grep -q "running"; then
    echo "[$(date -Iseconds)] Services are running"
    break
  fi
  
  attempt=$((attempt + 1))
  sleep 5
done

# Give services a bit more time to stabilize
sleep 30

# ==============================================================================
# STEP 9: Verify deployment
# ==============================================================================

echo "[$(date -Iseconds)] Step 9: Verifying deployment..."

# Check API health
if curl -f http://localhost:8080/health 2>/dev/null; then
  echo "[$(date -Iseconds)] âœ“ API is healthy"
else
  echo "[$(date -Iseconds)] âœ— API health check failed"
fi

# Check MySQL
if docker exec ratanhr-mysql mysqladmin -u${mysql_user} -p${mysql_password} ping 2>/dev/null; then
  echo "[$(date -Iseconds)] âœ“ MySQL is healthy"
else
  echo "[$(date -Iseconds)] âœ— MySQL health check failed"
fi

# Check Redis
if docker exec ratanhr-redis redis-cli -a ${redis_password} ping 2>/dev/null | grep -q "PONG"; then
  echo "[$(date -Iseconds)] âœ“ Redis is healthy"
else
  echo "[$(date -Iseconds)] âœ— Redis health check failed"
fi

# ==============================================================================
# STEP 10: Setup CloudWatch monitoring
# ==============================================================================

echo "[$(date -Iseconds)] Step 10: Setting up CloudWatch monitoring..."

# Install CloudWatch agent
wget https://s3.amazonaws.com/amazoncloudwatch-agent/ubuntu/amd64/latest/amazon-cloudwatch-agent.deb
dpkg -i -E ./amazon-cloudwatch-agent.deb

# Configure and start CloudWatch agent
cat > /opt/aws/amazon-cloudwatch-agent/etc/amazon-cloudwatch-agent.json << 'CW_CONFIG_EOF'
{
  "agent": {
    "metrics_collection_interval": 60
  },
  "logs": {
    "logs_collected": {
      "files": {
        "collect_list": [
          {
            "file_path": "/var/log/user-data.log",
            "log_group_name": "/aws/ec2/ratanhr-api",
            "log_stream_name": "{instance_id}-user-data"
          },
          {
            "file_path": "/var/log/docker.log",
            "log_group_name": "/aws/ec2/ratanhr-api",
            "log_stream_name": "{instance_id}-docker"
          }
        ]
      }
    }
  },
  "metrics": {
    "namespace": "RatanHR",
    "metrics_collected": {
      "cpu": {
        "measurement": [
          {
            "name": "cpu_usage_idle",
            "rename": "CPU_IDLE",
            "unit": "Percent"
          }
        ]
      },
      "mem": {
        "measurement": [
          {
            "name": "mem_used_percent",
            "rename": "MEM_USED",
            "unit": "Percent"
          }
        ]
      },
      "disk": {
        "measurement": [
          {
            "name": "used_percent",
            "rename": "DISK_USED",
            "unit": "Percent"
          }
        ],
        "metrics_collection_interval": 60,
        "resources": [
          "/"
        ]
      }
    }
  }
}
CW_CONFIG_EOF

# Start CloudWatch agent
/opt/aws/amazon-cloudwatch-agent/bin/amazon-cloudwatch-agent-ctl \
  -a fetch-config \
  -m ec2 \
  -s \
  -c file:/opt/aws/amazon-cloudwatch-agent/etc/amazon-cloudwatch-agent.json

echo "[$(date -Iseconds)] CloudWatch agent started"

# ==============================================================================
# STEP 11: Setup log rotation
# ==============================================================================

echo "[$(date -Iseconds)] Step 11: Setting up log rotation..."

cat > /etc/logrotate.d/ratanhr << 'LOGROTATE_EOF'
/opt/ratanhr/logs/*.log {
  daily
  rotate 14
  compress
  delaycompress
  notifempty
  create 0640 ubuntu ubuntu
  sharedscripts
  postrotate
    docker compose -f /opt/ratanhr/docker-compose.prod.yml kill -s HUP api 2>/dev/null || true
  endscript
}
LOGROTATE_EOF

echo "[$(date -Iseconds)] Log rotation configured"

# ==============================================================================
# STEP 12: Create systemd service for docker-compose
# ==============================================================================

echo "[$(date -Iseconds)] Step 12: Creating systemd service..."

cat > /etc/systemd/system/ratanhr.service << 'SYSTEMD_EOF'
[Unit]
Description=RatanHR HRMS Docker Compose Application
After=network.target

[Service]
Type=oneshot
ExecStart=/usr/bin/docker compose -f /opt/ratanhr/docker-compose.prod.yml up -d
ExecStop=/usr/bin/docker compose -f /opt/ratanhr/docker-compose.prod.yml down
RemainAfterExit=yes
StandardOutput=journal
StandardError=journal
SyslogIdentifier=ratanhr

[Install]
WantedBy=multi-user.target
SYSTEMD_EOF

systemctl daemon-reload
systemctl enable ratanhr

echo "[$(date -Iseconds)] Systemd service created and enabled"

# ==============================================================================
# STEP 13: Final summary
# ==============================================================================

echo ""
echo "========== RatanHR HRMS Deployment Completed =========="
echo "[$(date -Iseconds)] Deployment finished successfully!"
echo ""
echo "Access information:"
echo "  - Domain: https://${domain_name}"
echo "  - API: https://${domain_name}/api"
echo "  - Admin: https://${domain_name}/hangfire"
echo ""
echo "Important:"
echo "  - Update domain registrar nameservers with values from Terraform output"
echo "  - SSL certificate will be auto-renewed by Certbot"
echo "  - Database backups run daily at 2 AM UTC"
echo "  - Monitor CloudWatch logs at /aws/ec2/ratanhr-api"
echo ""
echo "Verify deployment:"
echo "  docker ps (check all containers running)"
echo "  docker logs ratanhr-api (check API logs)"
echo "  curl https://${domain_name}/health (check API health)"
echo ""
echo "========== Deployment Summary Complete =========="
