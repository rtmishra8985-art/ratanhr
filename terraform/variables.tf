# ============================================================================
# Terraform Variables - RatanHR HRMS v1.0.4
# ============================================================================

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "ap-south-1"
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "production"
}

variable "project_name" {
  description = "Project name"
  type        = string
  default     = "ratanhr"
}

# ============================================================================
# VPC CONFIGURATION
# ============================================================================

variable "vpc_cidr" {
  description = "VPC CIDR block"
  type        = string
  default     = "10.0.0.0/16"
}

variable "public_subnet_1_cidr" {
  description = "Public subnet 1 CIDR"
  type        = string
  default     = "10.0.1.0/24"
}

variable "public_subnet_2_cidr" {
  description = "Public subnet 2 CIDR"
  type        = string
  default     = "10.0.2.0/24"
}

variable "private_subnet_1_cidr" {
  description = "Private subnet 1 CIDR"
  type        = string
  default     = "10.0.10.0/24"
}

variable "private_subnet_2_cidr" {
  description = "Private subnet 2 CIDR"
  type        = string
  default     = "10.0.11.0/24"
}

variable "ssh_cidr_blocks" {
  description = "CIDR blocks allowed for SSH"
  type        = list(string)
  default     = ["0.0.0.0/0"] # CHANGE THIS to your IP
}

# ============================================================================
# EC2 CONFIGURATION
# ============================================================================

variable "ec2_instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t3.medium" # 2 vCPU, 4 GB RAM
}

# ============================================================================
# RDS MYSQL CONFIGURATION
# ============================================================================

variable "mysql_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t3.micro" # 1 vCPU, 1 GB RAM
}

variable "mysql_database" {
  description = "MySQL database name"
  type        = string
  default     = "hrms_db"
  sensitive   = true
}

variable "mysql_user" {
  description = "MySQL user"
  type        = string
  default     = "hrms"
  sensitive   = true
}

variable "mysql_password" {
  description = "MySQL password (min 16 chars, mixed case, numbers, symbols)"
  type        = string
  sensitive   = true
}

variable "backup_retention_days" {
  description = "RDS backup retention period"
  type        = number
  default     = 14
}

# ============================================================================
# ELASTICACHE REDIS CONFIGURATION
# ============================================================================

variable "redis_node_type" {
  description = "ElastiCache node type"
  type        = string
  default     = "cache.t3.micro" # 0.5 GB
}

# ============================================================================
# DOMAIN & SSL
# ============================================================================

variable "domain_name" {
  description = "Domain name (e.g., hrms.yourdomain.com)"
  type        = string
}

# ============================================================================
# JWT KEYS (Base64 encoded PEM format)
# ============================================================================

variable "jwt_private_key" {
  description = "JWT private key (PEM format, base64 encoded)"
  type        = string
  sensitive   = true
}

variable "jwt_public_key" {
  description = "JWT public key (PEM format, base64 encoded)"
  type        = string
  sensitive   = true
}

variable "encryption_key" {
  description = "AES-256 encryption key (base64 encoded)"
  type        = string
  sensitive   = true
}

# ============================================================================
# SMTP CONFIGURATION
# ============================================================================

variable "smtp_host" {
  description = "SMTP server host"
  type        = string
}

variable "smtp_port" {
  description = "SMTP server port"
  type        = number
  default     = 587
}

variable "smtp_user" {
  description = "SMTP username"
  type        = string
  sensitive   = true
}

variable "smtp_password" {
  description = "SMTP password"
  type        = string
  sensitive   = true
}

# ============================================================================
# S3 BACKUP
# ============================================================================

variable "backup_bucket" {
  description = "S3 bucket for backups (must be globally unique)"
  type        = string
}
