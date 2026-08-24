# Database Dictionary
**HRMS v2.1.0** | MySQL 8.4

---

## Companies

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | int | N | Primary key |
| Name | varchar | N | Company legal name |
| Domain | varchar | Y | Email domain for tenant identification |
| LogoPath | varchar | Y | Path to company logo |
| IsActive | bool | N | Soft-delete flag |
| CreatedAt | datetime(6) | N | Creation timestamp (UTC) |

---

## Employees

| Column | Type | Nullable | Encrypted | Description |
|--------|------|----------|-----------|-------------|
| Id | int | N | | Primary key |
| EmployeeId | varchar | N | | Business employee code (e.g. EMP001) |
| CompanyId | int | N | | FK → Companies.Id |
| FullName | varchar | N | | Display name |
| Email | varchar | N | | Work email |
| Phone | varchar | Y | | Contact phone |
| Department | varchar | Y | | Department name |
| Designation | varchar | Y | | Job title |
| DateOfJoining | date | N | | Employment start date |
| NationalId | varchar | Y | ✅ AES-256 | PAN / national ID |
| AadhaarNumber | varchar | Y | ✅ AES-256 | Aadhaar UID |
| BankName | varchar | Y | | Bank name |
| AccountNumber | varchar | Y | ✅ AES-256 | Bank account number |
| IFSC | varchar | Y | | Bank IFSC code |
| UAN | varchar | Y | | Universal Account Number (PF) |
| IsActive | bool | N | | Active/terminated |
| ProfilePicturePath | varchar | Y | | Profile photo path |

---

## Users

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | varchar | N | GUID primary key |
| Email | varchar | N | Login email (unique) |
| PasswordHash | varchar | N | BCrypt hash (factor 12) |
| Role | varchar | N | superadmin / admin / hr / employee |
| FullName | varchar | N | Display name |
| CompanyId | int | Y | Null for superadmin |
| EmployeeId | varchar | Y | Linked employee record |
| IsActive | bool | N | Account enabled flag |
| MustChangePassword | bool | N | Force password reset |
| CreatedAt | datetime(6) | N | Creation timestamp (UTC) |

---

## Payslips

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | int | N | Primary key |
| EmployeeId | varchar | N | FK → Employees.EmployeeId |
| Month | int | N | 1–12 |
| Year | int | N | e.g. 2026 |
| WorkingDays | int | N | Calendar working days |
| DaysPresent | int | N | Attendance days |
| BasicPay | decimal | N | Basic salary component |
| HRA | decimal | N | House Rent Allowance |
| DA | decimal | N | Dearness Allowance |
| Conveyance | decimal | N | Conveyance allowance |
| MedicalAllowance | decimal | N | Medical allowance |
| OtherAllowances | decimal | N | Miscellaneous |
| GrossEarnings | decimal | N | Sum of all earnings |
| PFEmployee | decimal | N | Employee PF contribution (12%) |
| PFEmployer | decimal | N | Employer PF contribution (12%) |
| ESI | decimal | N | Employee State Insurance |
| PT | decimal | N | Professional Tax |
| TDS | decimal | N | Tax Deducted at Source |
| TotalDeductions | decimal | N | Sum of all deductions |
| NetPay | decimal | N | GrossEarnings − TotalDeductions |
| GeneratedAt | datetime(6) | N | Payslip generation timestamp (UTC) |

---

## LeaveRequests

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | int | N | Primary key |
| EmployeeId | varchar | N | FK → Employees.EmployeeId |
| LeaveTypeId | int | N | FK → LeaveTypes.Id |
| StartDate | date | N | |
| EndDate | date | N | |
| TotalDays | int | N | Calculated duration |
| Status | varchar | N | Pending / Approved / Rejected |
| Reason | varchar | Y | Employee's reason |
| ApproverComment | varchar | Y | HR comment |
| CreatedAt | datetime(6) | N | Creation timestamp (UTC) |
| UpdatedAt | datetime(6) | Y | Last update timestamp (UTC) |

---

## AuditLogs

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | long | N | Primary key |
| UserId | varchar | Y | Actor user ID |
| EntityName | varchar | N | e.g. "Employee", "Payslip" |
| EntityId | varchar | Y | PK of changed record |
| Action | varchar | N | Create / Update / Delete |
| OldValues | text | Y | JSON snapshot before change |
| NewValues | text | Y | JSON snapshot after change |
| IpAddress | varchar | Y | Client IP |
| CorrelationId | varchar | Y | X-Correlation-ID of the request |
| CreatedAt | datetime(6) | N | Creation timestamp (UTC) |
