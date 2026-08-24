# ============================================================================
# Terraform Outputs - RatanHR HRMS v1.0.4
# ============================================================================

output "load_balancer_dns" {
  description = "ALB DNS name"
  value       = aws_lb.main.dns_name
}

output "load_balancer_arn" {
  description = "ALB ARN"
  value       = aws_lb.main.arn
}

output "domain_name" {
  description = "Domain name (CNAME should point to ALB)"
  value       = var.domain_name
}

output "route53_nameservers" {
  description = "Route53 nameservers (update domain registrar)"
  value       = aws_route53_zone.main.name_servers
}

output "rds_endpoint" {
  description = "RDS cluster endpoint (writer)"
  value       = aws_rds_cluster.mysql.endpoint
  sensitive   = true
}

output "rds_reader_endpoint" {
  description = "RDS cluster reader endpoint"
  value       = aws_rds_cluster.mysql.reader_endpoint
  sensitive   = true
}

output "redis_primary_endpoint" {
  description = "Redis primary endpoint"
  value       = aws_elasticache_replication_group.redis.primary_endpoint_address
  sensitive   = true
}

output "redis_password" {
  description = "Redis password"
  value       = random_password.redis_password.result
  sensitive   = true
}

output "ec2_instance_id" {
  description = "EC2 instance ID"
  value       = aws_instance.api.id
}

output "ec2_private_ip" {
  description = "EC2 private IP"
  value       = aws_instance.api.private_ip
}

output "s3_backup_bucket" {
  description = "S3 bucket for backups"
  value       = aws_s3_bucket.backups.id
}

output "cloudwatch_log_group_api" {
  description = "CloudWatch log group for API"
  value       = aws_cloudwatch_log_group.api.name
}

output "ssl_certificate_arn" {
  description = "SSL certificate ARN"
  value       = aws_acm_certificate.main.arn
}

output "deployment_summary" {
  description = "Deployment summary"
  value = {
    load_balancer_dns   = aws_lb.main.dns_name
    domain_name         = var.domain_name
    rds_database        = var.mysql_database
    redis_cluster       = aws_elasticache_replication_group.redis.id
    ec2_instance        = aws_instance.api.id
    ssl_certificate     = aws_acm_certificate.main.arn
    backup_bucket       = aws_s3_bucket.backups.id
    cloudwatch_logs     = aws_cloudwatch_log_group.api.name
    next_steps          = "1) Update domain registrar nameservers 2) Monitor EC2 user-data deployment 3) Check ALB health"
  }
}
