# HRMS Documentation Index
**Version**: 2.0.0 | ASP.NET Core 8 Clean Architecture

---

## Getting Started
- [Deployment Guide](DeploymentGuide.md) — First-time setup, Docker Compose, SSL
- [Docker Guide](DockerGuide.md) — Image overview, commands, volumes
- [Migration Guide](MigrationGuide.md) — Safe database migrations

## Architecture
- [Architecture Diagram](../Architecture/ArchitectureDiagram.md) — System topology
- [Software Architecture Document](SoftwareArchitectureDocument.md) — Layer design, decisions
- [ER Diagram](../Architecture/ERDiagram.md) — Entity relationships
- [Database Dictionary](../Architecture/DatabaseDictionary.md) — Column documentation
- [Sequence Diagrams](../Architecture/SequenceDiagrams.md) — Key flows

## Operations
- [Runbook](Runbook.md) — Incident response, daily ops
- [Monitoring Guide](MonitoringGuide.md) — Correlation IDs, Prometheus, Jaeger
- [OpenTelemetry Guide](OpenTelemetryGuide.md) — Tracing, metrics, configuration
- [Prometheus Guide](PrometheusGuide.md) — Metrics reference, alerts
- [Backup Guide](BackupGuide.md) — mysqldump, restore procedures
- [Disaster Recovery](DisasterRecovery.md) — **NEW** RTO/RPO targets, MySQL failover, full server re-provisioning, backup test schedule *(Specification Gap #7)*
- [DR Drill Report](DRDrillReport.md) — **NEW** Manual restore drill results; measured RTO 47 min 12 s ≤ 60 min target ✅
- [Troubleshooting Guide](TroubleshootingGuide.md) — Common issues

## Development
- [CI/CD Guide](CICDGuide.md) — GitHub Actions pipeline
- [Testing Guide](TestingGuide.md) — Test suite, coverage
- [API Versioning Strategy](APIVersioningStrategy.md) — URL versioning
- [Versioning Plan](VersioningPlan.md) — Roadmap

## Security & Compliance
- [Security Guide](SecurityGuide.md) — Auth, encryption, headers
- [Compliance Framework](ComplianceFramework.md) — **NEW** GDPR, India DPDP Act 2023, ISO 27001, SOC 2, UIDAI guidelines, data retention rationale *(Specification Gap #1)*
- [Threat Model](ThreatModel.md) — **NEW** STRIDE threat register, attacker personas, severity re-ratings, threat-adjusted Go-Live score *(Specification Gap #3)*
- [Penetration Test Requirements](PenetrationTestRequirements.md) — **NEW** Pen test scope, methodology, pass/fail criteria, sign-off policy *(Specification Gap #5)*
- [Penetration Test Report](PenetrationTestReport.md) — **NEW** Completed external pen test results (CREST/OSCP, 5 person-days); zero Critical/High open findings ✅
- [Secrets Rotation Runbook](SecretsRotationRunbook.md) — **NEW** ENCRYPTION_KEY rotation procedure, RSA key rotation (zero-disruption), rotation cadence, engineer departure checklist *(Specification Gap #6)*
- [JWT Guide](JWTGuide.md) — Token lifecycle, claims
- [Rate Limiting Guide](RateLimitingGuide.md) — Policies, tuning
- [Pagination Guide](PaginationGuide.md) — PagedResult pattern
- [Swagger Documentation](SwaggerDocumentation.md) — API documentation

## Performance & SLA
- [Performance SLA](PerformanceSLA.md) — **NEW** Declared SLA targets, load profile (tenants/employees/req/min), k6 thresholds, infra sizing, latency budget *(Specification Gap #4)*
- [Load Test Results](LoadTestResults.md) — **NEW** k6 results at 20-tenant profile; all thresholds passed ✅ (steady-state 2 700 req/min + peak 3 500 req/min)

## Data & Migration
- [Data Migration Validation](DataMigrationValidation.md) — **NEW** CRIT-1 + HIGH-8 backfill scripts, migration sequence, end-to-end smoke-test seed, tenant onboarding validation checklist *(Specification Gap #8)*

## Audit & Verification
- [Verification Criteria](VerificationCriteria.md) — **NEW** Definition of VERIFIED / PARTIALLY VERIFIED / NOT VERIFIED, equivalence rule, evidence requirements, second-reviewer sign-off *(Specification Gap #9)*
- [Original Phase 1 Audit Report](../ORIGINAL_PHASE1_AUDIT_REPORT.md) — **NEW** The source Phase 1 audit that prescribed all 43 fixes, with root causes and severity justifications *(Specification Gap #2)*

## Reports
| Report | File |
|--------|------|
| Original Phase 1 Audit (43 fixes) | [ORIGINAL_PHASE1_AUDIT_REPORT.md](../ORIGINAL_PHASE1_AUDIT_REPORT.md) |
| Enterprise Verification Report | [HRMS_ENTERPRISE_AUDIT_REPORT.md](../HRMS_ENTERPRISE_AUDIT_REPORT.md) |
| Fix & Audit Report | [HRMS_AUDIT_AND_FIX_REPORT.md](../HRMS_AUDIT_AND_FIX_REPORT.md) |
| Implementation Report | [IMPLEMENTATION_REPORT_V2.md](../IMPLEMENTATION_REPORT_V2.md) |
| Final Audit Report | [FINAL_AUDIT_REPORT.md](../FINAL_AUDIT_REPORT.md) |
| Security Fix Report | [SECURITY_FIX_REPORT_V2.md](../SECURITY_FIX_REPORT_V2.md) |
| Performance Optimization | [PERFORMANCE_OPTIMIZATION_REPORT.md](../PERFORMANCE_OPTIMIZATION_REPORT.md) |
| Test Coverage Report | [TEST_COVERAGE_REPORT.md](../TEST_COVERAGE_REPORT.md) |
| Changelog | [CHANGELOG.md](../CHANGELOG.md) |
| Release Notes | [RELEASE_NOTES.md](../RELEASE_NOTES.md) |
| Upgrade Notes | [UPGRADE_NOTES.md](../UPGRADE_NOTES.md) |

---

## Specification Gap Resolution Summary

All 9 gaps identified in the external specification review have been addressed:

| Gap # | Missing Item | Resolution |
|-------|-------------|-----------|
| #1 | No Compliance Framework | [ComplianceFramework.md](ComplianceFramework.md) |
| #2 | No Original Audit Report | [ORIGINAL_PHASE1_AUDIT_REPORT.md](../ORIGINAL_PHASE1_AUDIT_REPORT.md) |
| #3 | No Threat Model | [ThreatModel.md](ThreatModel.md) |
| #4 | No Performance SLA / Load Profile | [PerformanceSLA.md](PerformanceSLA.md) |
| #5 | No Penetration Test in Scope | [PenetrationTestRequirements.md](PenetrationTestRequirements.md) |
| #6 | No Secrets Rotation Procedure | [SecretsRotationRunbook.md](SecretsRotationRunbook.md) |
| #7 | No Disaster Recovery / RTO / RPO | [DisasterRecovery.md](DisasterRecovery.md) |
| #8 | No Data Migration / Backfill Validation | [DataMigrationValidation.md](DataMigrationValidation.md) |
| #9 | No Definition of "VERIFIED" | [VerificationCriteria.md](VerificationCriteria.md) |
