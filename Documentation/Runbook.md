# Operations Runbook
**HRMS v2.1.0** | MySQL 8.4

---

## Service Contacts

| Role | Responsibility | Escalation |
|------|---------------|------------|
| DevOps | Infrastructure, Docker, SSL | Sev1 incidents |
| Backend | API bugs, migrations | Sev2+ |
| DBA | MySQL performance, data issues | As needed |

---

## Daily Health Checks

```bash
# 1. Service status
docker compose ps

# 2. Health endpoint
curl -s https://your-domain.com/health | python3 -m json.tool

# 3. Disk usage (backups + logs)
df -h
du -sh backups/
du -sh /var/lib/docker/volumes/hrms_logs/

# 4. Error rate (last 24h in logs)
grep "\[ERR\]" /var/lib/docker/volumes/hrms_logs/_data/hrms-$(date +%Y%m%d).log | wc -l
```

---

## Incident Response

### Severity 1 — API Down (all users affected)

1. Check container status: `docker compose ps`
2. Check API logs: `docker compose logs --tail=50 api`
3. If container crashed: `docker compose up -d api`
4. If migration failed: `docker compose run --rm migrate && docker compose up -d api`
5. If DB down: `docker compose up -d mysql && docker compose up -d api`
6. Notify users of downtime

### Severity 2 — Partial Failure (some features broken)

1. Check `/health` for subsystem status
2. Check correlation ID of failing requests in user reports
3. Search logs: `grep "CorrelationId=<id>" Logs/hrms-*.log`
4. Identify root cause from stack trace
5. Apply fix in staging, then production

### Severity 3 — Performance Degradation

1. Check `docker stats` for resource pressure
2. Check `hrms_db_query_duration_ms` in Prometheus
3. Run `EXPLAIN ANALYZE` on slow queries in MySQL client
4. Check for missing indexes (see `MigrationGuide.md`)
5. Check if streaming exports are being used for large reports

---

## Common Runbook Entries

### Restart API without downtime

```bash
docker compose up -d --no-deps api
```

### Run migrations (production)

```bash
# Always run migrations BEFORE deploying new code
docker compose run --rm migrate
docker compose up -d api
```

### Clear Redis cache

```bash
docker compose exec redis redis-cli -a $REDIS_PASSWORD FLUSHDB
```

### Force SSL certificate renewal

```bash
docker compose run --rm certbot certbot renew --force-renewal
docker compose exec nginx nginx -s reload
```

### Scale API horizontally

```bash
# Add a load balancer (nginx upstream) then:
docker compose up -d --scale api=3
```

### View active connections to MySQL

```bash
docker compose exec mysql mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db -e "
SELECT id, user, host, db, command, time, state, left(info, 100) AS query
FROM information_schema.processlist
WHERE command != 'Sleep'
ORDER BY time DESC;"
```

### Kill a long-running MySQL query

```bash
docker compose exec mysql mysql -u hrms -p"$MYSQL_PASSWORD" hrms_db -e "
SELECT CONCAT('KILL ', id, ';') AS kill_stmt
FROM information_schema.processlist
WHERE command != 'Sleep' AND time > 300;"
# Execute the returned KILL statements
```

---

## Maintenance Windows

### Scheduled Maintenance Steps

1. Announce downtime to users (at least 30 min notice)
2. `docker compose stop nginx` — stops new traffic
3. Wait for in-flight requests to complete (30s)
4. Apply changes (migrations, deploys)
5. `docker compose start nginx`
6. Verify `/health` returns Healthy
7. Announce end of maintenance

### Zero-Downtime Deployment (when no schema changes)

```bash
# Build new image
docker compose build api

# Rolling restart (brief interruption < 2s)
docker compose up -d --no-deps api

# Verify
curl https://your-domain.com/health
```
