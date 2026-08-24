# Secrets Rotation Runbook
**HRMS v2.1.0** | MySQL 8.4 | Addresses Specification Gap #6

---

## Overview

The audit (MED-16, MED-18) identified secrets stored in config defaults but did not prescribe a rotation procedure. This runbook closes that gap. It covers the three secrets requiring special handling:

1. `ENCRYPTION_KEY` — AES-256 key used to encrypt PII in the database
2. RSA private key — used for RS256 JWT signing
3. All other secrets — database password, Redis password, Grafana password, SMTP credentials

---

## Rotation Cadence Policy

| Secret | Rotation Trigger | Maximum Age |
|--------|-----------------|-------------|
| `ENCRYPTION_KEY` | On confirmed compromise or engineer departure with key access | 12 months (forced) |
| RSA private key (`JWT_PRIVATE_KEY`) | On confirmed compromise or engineer departure | 6 months (forced) |
| `MYSQL_PASSWORD` | On engineer departure with DB access; on any confirmed breach | 90 days (forced) |
| `REDIS_PASSWORD` | On engineer departure | 90 days (forced) |
| `GRAFANA_ADMIN_PASSWORD` | On engineer departure | 90 days (forced) |
| `JWT_KEY` (if HMAC fallback used) | On engineer departure; on token theft suspicion | 30 days (forced) |
| SMTP credentials | On email provider security event | 90 days (forced) |
| All secrets | On engineer departure | Immediate |

---

## 1. Rotating `ENCRYPTION_KEY` (AES-256 PII Encryption)

### Why This Is Complex

The `ENCRYPTION_KEY` encrypts `AadhaarNumber`, `PanNumber`, and `BankAccountNumber` at rest in MySQL. Rotating it requires decrypting every row with the old key and re-encrypting with the new key — this cannot be done with a simple config change.

### Rotation Procedure

**Estimated downtime: 10–30 minutes** (depending on employee count).

```bash
# Step 1: Take a full backup before starting
docker compose exec mysql \
  sh -c "mysqldump -u hrms -p'$MYSQL_PASSWORD' --single-transaction hrms_db" | \
  gzip > "backups/pre_key_rotation_$(date +%Y%m%d_%H%M%S).sql.gz"

# Step 2: Generate a new AES-256 key
NEW_KEY=$(openssl rand -base64 32)
echo "New key (store securely — do NOT log): $NEW_KEY"

# Step 3: Stop the API to prevent writes during rotation
docker compose stop api

# Step 4: Run the key-rotation migration script
# This script reads OLD_ENCRYPTION_KEY, decrypts, re-encrypts with NEW_ENCRYPTION_KEY
docker compose run --rm \
  -e OLD_ENCRYPTION_KEY="$ENCRYPTION_KEY" \
  -e NEW_ENCRYPTION_KEY="$NEW_KEY" \
  api \
  dotnet HRMS.API.dll --run-migration key-rotation

# Step 5: Update the secret in your secrets manager / .env
# For Docker Compose:
sed -i "s/ENCRYPTION_KEY=.*/ENCRYPTION_KEY=$NEW_KEY/" .env
# For Kubernetes:
kubectl create secret generic hrms-secrets \
  --from-literal=ENCRYPTION_KEY="$NEW_KEY" \
  --dry-run=client -o yaml | kubectl apply -f -

# Step 6: Restart the API
docker compose start api

# Step 7: Verify PII reads/writes work
curl -s -H "Authorization: Bearer $ADMIN_JWT" \
  https://your-domain.com/api/employees/1/pii | jq .
# Should return decrypted (masked) PII — not garbage bytes

# Step 8: Verify no errors in logs for 5 minutes
docker compose logs --follow api | grep -i "error\|decrypt\|encrypt"
```

### Key-Rotation Migration Script

The `--run-migration key-rotation` command must be implemented in `HRMS.Infrastructure/Scripts/KeyRotationMigration.cs`:

```csharp
// Pseudocode — implement before first key rotation
public class KeyRotationMigration
{
    public async Task RunAsync(string oldKey, string newKey, AppDbContext db)
    {
        var employees = await db.Employees
            .Where(e => e.AadhaarNumber != null || e.PanNumber != null)
            .ToListAsync();

        foreach (var emp in employees)
        {
            if (emp.AadhaarNumber != null)
                emp.AadhaarNumber = ReEncrypt(emp.AadhaarNumber, oldKey, newKey);
            if (emp.PanNumber != null)
                emp.PanNumber = ReEncrypt(emp.PanNumber, oldKey, newKey);
            if (emp.BankAccountNumber != null)
                emp.BankAccountNumber = ReEncrypt(emp.BankAccountNumber, oldKey, newKey);
        }

        await db.SaveChangesAsync();
    }

    private string ReEncrypt(string ciphertext, string oldKey, string newKey)
    {
        var plaintext = AesEncryptionService.Decrypt(ciphertext, oldKey);
        return AesEncryptionService.Encrypt(plaintext, newKey);
    }
}
```

> ⚠️ **Sprint 1 action item:** Implement `KeyRotationMigration.cs` and wire it to `--run-migration key-rotation` before first production deployment.

---

## 2. Rotating RSA Private Key (JWT RS256 Signing)

### Why This Requires Care

Rotating the RSA private key invalidates all active JWT access tokens. Users will receive 401 errors and must re-login. Refresh tokens remain valid (stored in DB, validated by signature only on the refresh endpoint — which accepts the old signature for the grace period).

### Zero-Disruption Rotation (Recommended)

Use a **key overlap window** of 15 minutes:

```bash
# Step 1: Generate new RSA key pair
openssl genrsa -out jwt_private_new.pem 2048
openssl rsa -in jwt_private_new.pem -pubout -out jwt_public_new.pem

# Step 2: Add the new PUBLIC key to the validator
# HRMS.API reads JWT_PUBLIC_KEY_1 (old) and JWT_PUBLIC_KEY_2 (new)
# during the overlap window — both keys are accepted for validation
export JWT_PUBLIC_KEY_2="$(cat jwt_public_new.pem)"

# Step 3: Restart API with both public keys active (overlap starts)
docker compose up -d --no-deps api

# Step 4: Update the signing key to use the NEW private key
export JWT_PRIVATE_KEY="$(cat jwt_private_new.pem)"
export JWT_PUBLIC_KEY_1="$(cat jwt_public_new.pem)"
unset JWT_PUBLIC_KEY_2

# Step 5: Restart API (overlap ends — old tokens expire naturally in ≤ 30 min)
docker compose up -d --no-deps api

# Step 6: Securely delete old key files
shred -u jwt_private_old.pem
```

> **Note:** Multi-public-key validation (`JWT_PUBLIC_KEY_2`) must be implemented in `JwtService.cs` before first key rotation. This is a Sprint 1 item.

### Forced Rotation (Compromise Response)

If the private key is confirmed compromised:

```bash
# Immediately invalidate ALL sessions by rotating the key without overlap
# 1. Generate new key pair (as above)
# 2. Update secrets and restart API immediately
# 3. Notify all users that they must re-login
# 4. All refresh tokens remain valid; old JWTs are immediately invalid
```

---

## 3. Rotating Other Secrets

### Database Password (`MYSQL_PASSWORD`)

```bash
# Step 1: Update password in MySQL
docker compose exec mysql mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e \
  "ALTER USER 'hrms'@'%' IDENTIFIED BY 'new-strong-password-here';"

# Step 2: Update .env / Kubernetes secret
sed -i "s/MYSQL_PASSWORD=.*/MYSQL_PASSWORD=new-strong-password-here/" .env

# Step 3: Restart API (picks up new connection string)
docker compose up -d --no-deps api

# Step 4: Verify health check passes
curl https://your-domain.com/health
```

### Redis Password (`REDIS_PASSWORD`)

```bash
# Step 1: Update Redis config
docker compose exec redis redis-cli CONFIG SET requirepass "new-redis-password"

# Step 2: Update .env
sed -i "s/REDIS_PASSWORD=.*/REDIS_PASSWORD=new-redis-password/" .env

# Step 3: Restart API
docker compose up -d --no-deps api
```

### Grafana Admin Password

```bash
# Step 1: Reset via Grafana CLI
docker compose exec grafana grafana-cli admin reset-admin-password "new-grafana-password"

# Step 2: Update .env
sed -i "s/GRAFANA_ADMIN_PASSWORD=.*/GRAFANA_ADMIN_PASSWORD=new-grafana-password/" .env
```

---

## Rotation on Engineer Departure

When an engineer with secret access leaves the organisation, complete all of the following within **24 hours**:

| # | Action | Owner |
|---|--------|-------|
| 1 | Rotate `MYSQL_PASSWORD` | DevOps |
| 2 | Rotate `REDIS_PASSWORD` | DevOps |
| 3 | Rotate `GRAFANA_ADMIN_PASSWORD` | DevOps |
| 4 | Rotate `JWT_PRIVATE_KEY` (with overlap window) | DevOps |
| 5 | Rotate SMTP credentials | DevOps |
| 6 | Revoke the engineer's GitHub/CI access | Engineering Manager |
| 7 | Rotate `ENCRYPTION_KEY` if engineer had DB read access | Security Lead — schedule within 7 days to avoid extended downtime |
| 8 | Audit `AuditLogs` for any anomalous data access in the 7 days before departure | Security Lead |

---

## Secrets Inventory

| Secret Variable | Purpose | Stored In | Rotation Complexity |
|----------------|---------|-----------|---------------------|
| `ENCRYPTION_KEY` | AES-256 PII encryption | `.env` / K8s Secret | High (requires DB re-encryption) |
| `JWT_PRIVATE_KEY` | RS256 JWT signing | `.env` / K8s Secret | Medium (overlap window) |
| `MYSQL_PASSWORD` | Database access | `.env` / K8s Secret | Low |
| `REDIS_PASSWORD` | Cache access | `.env` / K8s Secret | Low |
| `GRAFANA_ADMIN_PASSWORD` | Monitoring dashboard | `.env` / K8s Secret | Low |
| `SMTP_PASSWORD` | Email delivery | `.env` / K8s Secret | Low |
| `SESSION_SECRET` | Session signing (if used) | `.env` / K8s Secret | Low |

---

## Sprint 1 Action Items

- [ ] Implement `KeyRotationMigration.cs` for `ENCRYPTION_KEY` rotation
- [ ] Implement multi-public-key JWT validation for zero-disruption RSA key rotation
- [ ] Add rotation reminder to team calendar (90-day cadence for standard secrets)
- [ ] Document secrets inventory in the team's password manager (reference this file for procedures)

---

*Runbook approved: 2026-07-26. Review on any rotation event or annually.*
