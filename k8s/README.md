# Kubernetes Deployment Guide — HRMS v2.1.0

Phase 7: Updated from PostgreSQL to MySQL 8.4.
All `postgres-svc` references replaced with `mysql-svc` (port 3306).
`postgres-statefulset.yaml` preserved as `.bak` for rollback reference.

---

## Prerequisites

| Tool | Purpose |
|------|---------|
| `kubectl` ≥ 1.28 | Apply manifests |
| `kustomize` ≥ 5.0 (or `kubectl apply -k`) | Ordered apply |
| nginx Ingress Controller | Route external traffic |
| cert-manager ≥ 1.14 | Automatic Let's Encrypt TLS |
| metrics-server | HPA CPU/memory scaling |

---

## Architecture

```
Internet → nginx Ingress (TLS + security headers)
              ↓
         hrms-api-svc (ClusterIP :80)
              ↓
         hrms-api Deployment (2–10 pods via HPA)
              ↓
    ┌─────────────────────┐
    │  mysql-svc          │  redis-svc
    │  (StatefulSet, 1x)  │  (StatefulSet, 1x)
    └─────────────────────┘
```

---

## First-time Setup

```bash
# 1. Create namespace
kubectl apply -f k8s/namespace.yaml

# 2. Install/configure External Secrets Operator and create the referenced
#    ClusterSecretStore. The ExternalSecret in this directory then materialises
#    `hrms-secrets`; no credentials are committed to this repository.
kubectl apply -f k8s/external-secrets/cluster-secret-store.yaml
kubectl apply -f k8s/external-secrets/external-secret.yaml

# 3. Apply remaining config
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/mysql-statefulset.yaml
kubectl apply -f k8s/redis-deployment.yaml

# 4. Wait for MySQL to be ready
kubectl wait --for=condition=ready pod -l app=hrms-mysql -n hrms --timeout=180s

# 5. Run one-shot DB migration job
kubectl apply -f k8s/migrate-job.yaml
kubectl wait --for=condition=complete job/hrms-migrate -n hrms --timeout=120s

# 6. Deploy the API
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/ingress.yaml
kubectl apply -f k8s/hpa.yaml
```

Or using kustomize (applies everything in the right order):
```bash
# Set the exact immutable image reference before applying to a cluster.
# This prevents a placeholder or mutable image alias from reaching release.
kustomize edit set image hrms-api=registry.example.com/your-org/hrms-api:1.0.0
kubectl apply -k k8s/
```

---

## Rolling Update (Subsequent Deploys)

```bash
# 1. Run migration job (if schema changes exist)
kubectl delete job hrms-migrate -n hrms --ignore-not-found
kubectl apply -f k8s/migrate-job.yaml
kubectl wait --for=condition=complete job/hrms-migrate -n hrms --timeout=120s

# 2. Update the API image to the exact release tag or digest
kubectl set image deployment/hrms-api api=registry.example.com/your-org/hrms-api:<new-tag> -n hrms
kubectl rollout status deployment/hrms-api -n hrms
```

---

## Secrets Management

Sensitive values are retrieved by External Secrets Operator from the configured
secret manager. No credentials are stored in this repository.

Required secret keys (MySQL 8.4):

| Key | Description |
|-----|-------------|
| `ConnectionStrings__DefaultConnection` | Full MySQL connection string |
| `Jwt__PrivateKeyPem` | RSA-2048 private key PEM (signing) |
| `Jwt__PublicKeyPem` | RSA-2048 public key PEM (verification) |
| `Security__EncryptionKey` | AES-256 base64 key for PII columns |
| `Cors__AllowedOrigins` | Comma-separated allowed origins |
| `Redis__ConnectionString` | Redis connection string |
| `MYSQL_DATABASE` | Database name (for MySQL StatefulSet) |
| `MYSQL_USER` | Application user (for MySQL StatefulSet) |
| `MYSQL_PASSWORD` | Application user password |
| `MYSQL_ROOT_PASSWORD` | MySQL root password |
| `REDIS_PASSWORD` | Redis password |

Encode a value:
```bash
echo -n "Server=mysql-svc;Port=3306;Database=hrms_db;User ID=hrms;Password=<PASS>;AllowPublicKeyRetrieval=True;SslMode=Required" | base64 -w 0
```

**Recommended:** Use [External Secrets Operator](https://external-secrets.io/) with AWS Secrets Manager, Azure Key Vault, or GCP Secret Manager. See `k8s/external-secrets/` for manifests.

---

## Backup

Daily backup CronJob runs at 02:00 UTC:

```bash
kubectl apply -f k8s/backup-cronjob.yaml

# Check backup jobs
kubectl get jobs -n hrms -l app.kubernetes.io/component=backup

# View backup logs
kubectl logs job/hrms-mysql-backup-<suffix> -n hrms
```

---

## Manifest Index

| File | Purpose |
|------|---------|
| `namespace.yaml` | `hrms` namespace |
| `configmap.yaml` | Non-secret config (MySQL host: `mysql-svc:3306`) |
| `external-secrets/` | External Secrets Operator resources; configure the provider before applying |
| `mysql-statefulset.yaml` | MySQL 8.4 StatefulSet + headless Service |
| `postgres-statefulset.yaml.bak` | Previous PostgreSQL 16 manifest (rollback reference) |
| `redis-deployment.yaml` | Redis Deployment + Service |
| `migrate-job.yaml` | One-shot EF Core migration Job |
| `api-deployment.yaml` | HRMS API Deployment + Service |
| `ingress.yaml` | nginx Ingress (TLS termination) |
| `hpa.yaml` | HorizontalPodAutoscaler (2–10 API pods) |
| `networkpolicy.yaml` | NetworkPolicy (DB reachable only from API pods) |
| `backup-cronjob.yaml` | MySQL backup CronJob (daily 02:00 UTC) |
| `kustomization.yaml` | Kustomize root |
| `external-secrets/` | External Secrets Operator manifests |

---

## Troubleshooting

```bash
# API pod logs
kubectl logs -l app=hrms-api -n hrms --tail=100

# MySQL pod
kubectl exec -it statefulset/hrms-mysql -n hrms -- mysql -u hrms -p

# Migration job logs
kubectl logs job/hrms-migrate -n hrms

# Describe pod for events
kubectl describe pod -l app=hrms-api -n hrms
```

**Common issues:**

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Authentication failed` at startup | `MYSQL_PASSWORD` secret missing | Check `kubectl get secret hrms-secrets -n hrms` |
| `Can't connect to MySQL server` | MySQL pod not ready | `kubectl wait --for=condition=ready pod -l app=hrms-mysql -n hrms` |
| Migration job `BackoffLimitExceeded` | Schema conflict or bad connection | Check job logs; ensure connection string is correct |
