# Versioning Plan
**HRMS** | 2026–2027 Roadmap

---

## Current Version: v2.0.0 (July 2026)

### v2.0.0 Delivered
- DevOps phase: CI/CD, safe migrations, Docker pinning, SSL automation
- Observability phase: Correlation IDs, OpenTelemetry, Prometheus
- Performance phase: Streaming exports, composite indexes

---

## Planned: v2.1.0 (Q4 2026)

| Feature | Priority |
|---------|----------|
| Read replica support for reports | High |
| Report result caching (Redis, 15-min TTL) | Medium |
| Webhook notifications (leave approvals, payroll) | Medium |
| Bulk employee import via CSV | Low |

---

## Planned: v2.2.0 (Q1 2027)

| Feature | Priority |
|---------|----------|
| Multi-currency payroll support | High |
| SAML 2.0 SSO integration | High |
| PostgreSQL streaming replication setup guide | Medium |
| Grafana dashboard templates | Low |

---

## API Version Support Policy

| API Version | Released | Supported Until |
|-------------|----------|-----------------|
| v1 | July 2026 | July 2027 (12 months) |
| v2 | TBD | TBD |

---

## Semantic Versioning

HRMS follows [SemVer 2.0.0](https://semver.org/):

- **Major** (X.0.0): Breaking API changes, migration required
- **Minor** (2.X.0): New features, backward compatible
- **Patch** (2.0.X): Bug fixes, security patches, backward compatible

Security patches may bypass the normal release cycle and ship as immediate patch releases.
