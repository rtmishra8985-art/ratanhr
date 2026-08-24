# External Secrets Operator — Setup Guide

Provides the production secret source for the Kubernetes deployment. The
generated `hrms-secrets` Secret is not committed to this repository.
Never commit real secret values to git.

## 1. Install ESO via Helm

```bash
helm repo add external-secrets https://charts.external-secrets.io
helm repo update
helm install external-secrets external-secrets/external-secrets \
  -n external-secrets --create-namespace
```

## 2. Create secrets in your backend (AWS example)

```bash
# Database credentials
aws secretsmanager create-secret --name hrms/production/db \
  --secret-string '{"connection_string":"Host=...","name":"hrms_db","username":"hrms","password":"<PASS>"}'

# JWT signing key
aws secretsmanager create-secret --name hrms/production/jwt \
  --secret-string '{"key":"<64-char-random-string>"}'

# Redis
aws secretsmanager create-secret --name hrms/production/redis \
  --secret-string '{"connection_string":"redis-svc:6379,password=<PASS>","password":"<PASS>"}'

# SMTP
aws secretsmanager create-secret --name hrms/production/smtp \
  --secret-string '{"host":"smtp.sendgrid.net","username":"apikey","password":"<SENDGRID_KEY>","from_address":"noreply@ratanhr.com"}'

# CORS
aws secretsmanager create-secret --name hrms/production/cors \
  --secret-string '{"allowed_origins":"https://app.ratanhr.com"}'
```

## 3. Create a service account with IRSA (AWS)

```bash
eksctl create iamserviceaccount \
  --name external-secrets-sa \
  --namespace external-secrets \
  --cluster <your-cluster> \
  --attach-policy-arn arn:aws:iam::aws:policy/SecretsManagerReadWrite \
  --approve
```

## 4. Apply manifests

```bash
kubectl apply -f k8s/external-secrets/cluster-secret-store.yaml
kubectl apply -f k8s/external-secrets/external-secret.yaml
```

## 5. Verify

```bash
kubectl get externalsecret -n hrms
# STATUS should show "SecretSynced"

kubectl get secret hrms-secrets -n hrms
# Should exist and contain all keys
```
