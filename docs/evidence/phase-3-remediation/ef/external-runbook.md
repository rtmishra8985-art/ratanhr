# External Runbook — EF Core Migration/Snapshot Verification

Run on a machine with .NET SDK 8.0.416 (per `global.json`) installed. Copy-ready.

```bash
cd RatanHR-merged-release-candidate

dotnet --info

dotnet restore HRMS.sln

# 1. Confirm the payslip migration and full chain are discoverable
dotnet ef migrations list \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API \
  --context ApplicationDbContext

# 2. Check for model/snapshot drift (expected: reports pending changes — see
#    docs/evidence/phase-3-remediation/ef/static-analysis.md for why)
dotnet ef migrations has-pending-model-changes \
  --project HRMS.Infrastructure \
  --startup-project HRMS.API \
  --context ApplicationDbContext
```

## If pending changes are reported (expected)

1. Do **not** hand-edit `ApplicationDbContextModelSnapshot_MySql.cs` further.
2. Regenerate it through the supported workflow:
   ```bash
   dotnet ef migrations add SyncModelSnapshot \
     --project HRMS.Infrastructure \
     --startup-project HRMS.API \
     --context ApplicationDbContext \
     --output-dir Migrations/MySql
   ```
3. Open the generated migration and diff it carefully against the 17 existing
   migrations. Because the snapshot was sparse, EF will likely propose adding many
   columns/indexes that **already exist in production** (created by the earlier
   hand-authored migrations) purely because the snapshot never recorded them.
   - Any `AddColumn`/`CreateIndex`/`AddForeignKey` that duplicates something an
     earlier migration already created against a live database must be edited out
     of `Up()`/`Down()` — keep only the snapshot correction, not a duplicate schema
     change. This is a manual review step; do not apply blindly.
4. Re-run `dotnet ef migrations has-pending-model-changes` until it reports none.
5. Verify a clean database builds from the full chain:
   ```bash
   dotnet ef database update \
     --project HRMS.Infrastructure \
     --startup-project HRMS.API \
     --context ApplicationDbContext \
     --connection "Server=127.0.0.1;Port=3306;Database=ratanhr_verify;Uid=root;Pwd=<local-only>;"
   ```
6. Confirm `ux_payslips_employee_month_year` exists on the resulting `payslips` table.

Save all command output under `docs/evidence/phase-3-remediation/ef/` (append, don't
overwrite this runbook or the static-analysis findings) and update
`docs/phase-3-readiness.md` with the real results before claiming this item VERIFIED.
