# ER Diagram — HRMS Database Schema
**Version**: 2.0.0

```
┌─────────────────┐         ┌──────────────────┐
│    Companies    │1      * │    Employees      │
│─────────────────│─────────│──────────────────│
│ Id (PK)         │         │ Id (PK)           │
│ Name            │         │ EmployeeId (UQ)   │
│ Domain          │         │ CompanyId (FK)    │
│ IsActive        │         │ DepartmentId (FK) │
│ CreatedAt       │         │ FullName          │
└─────────────────┘         │ Email             │
         │                  │ Phone             │
         │1                 │ DateOfJoining     │
         │                  │ Department        │
         ▼*                 │ Designation       │
┌─────────────────┐         │ BankName          │
│ CompanyBranches │         │ AccountNumber*    │
│─────────────────│         │ UAN               │
│ Id (PK)         │         │ NationalId*       │
│ CompanyId (FK)  │         │ AadhaarNumber*    │
│ BranchName      │         │ IsActive          │
│ Location        │         └──────────────────┘
└─────────────────┘                 │1
                                    │
          ┌─────────────────────────┼───────────────────────────┐
          │                         │                           │
          ▼*                        ▼*                          ▼*
┌──────────────────┐    ┌──────────────────┐     ┌──────────────────┐
│   WebAttendance  │    │   Payslips        │     │  LeaveRequests   │
│──────────────────│    │──────────────────│     │──────────────────│
│ Id (PK)          │    │ Id (PK)           │     │ Id (PK)          │
│ EmployeeId (FK)  │    │ EmployeeId (FK)   │     │ EmployeeId (FK)  │
│ AttDate          │    │ Month             │     │ LeaveTypeId (FK) │
│ Status           │    │ Year              │     │ StartDate        │
│ CheckIn          │    │ GrossEarnings     │     │ EndDate          │
│ CheckOut         │    │ NetPay            │     │ TotalDays        │
└──────────────────┘    │ PFEmployee        │     │ Status           │
                        │ TDS               │     │ Reason           │
┌──────────────────┐    └──────────────────┘     └──────────────────┘
│ ExcelAttendance  │                                      │*
│──────────────────│    ┌──────────────────┐    ┌────────▼─────────┐
│ Id (PK)          │    │ SalaryStructures  │    │   LeaveTypes     │
│ EmployeeId (FK)  │    │──────────────────│    │──────────────────│
│ CompanyId (FK)   │    │ Id (PK)           │    │ Id (PK)          │
│ AttDate          │    │ EmployeeId (FK)   │    │ Name             │
│ Status           │    │ BasicPay          │    │ AnnualQuotaDays  │
│ HoursWorked      │    │ HRA               │    │ IsPaid           │
└──────────────────┘    │ IsActive          │    │ IsActive         │
                        └──────────────────┘    └──────────────────┘

┌──────────────────┐    ┌──────────────────┐     ┌──────────────────┐
│      Users       │    │   RefreshTokens   │     │  PasswordReset   │
│──────────────────│    │──────────────────│     │  Tokens          │
│ Id (PK)          │1──*│ Id (PK)           │     │──────────────────│
│ Email (UQ)       │    │ UserId (FK)       │     │ Id (PK)          │
│ PasswordHash     │    │ Token (hashed)    │     │ UserId (FK)      │
│ Role             │    │ ExpiresAt         │     │ Token (hashed)   │
│ CompanyId (FK)   │    │ IsRevoked         │     │ ExpiresAt        │
│ IsActive         │    └──────────────────┘     │ IsUsed           │
│ MustChangePwd    │                             └──────────────────┘
└──────────────────┘

┌──────────────────┐    ┌──────────────────┐     ┌──────────────────┐
│   AuditLogs      │    │  Departments      │     │  HolidayCalendar │
│──────────────────│    │──────────────────│     │──────────────────│
│ Id (PK)          │    │ Id (PK)           │     │ Id (PK)          │
│ UserId           │    │ CompanyId (FK)    │     │ CompanyId (FK)   │
│ EntityName       │    │ Name              │     │ HolidayName      │
│ EntityId         │    │ IsActive          │     │ Date             │
│ Action           │    └──────────────────┘     │ IsActive         │
│ OldValues (JSON) │                             └──────────────────┘
│ NewValues (JSON) │
│ IpAddress        │    ┌──────────────────┐     ┌──────────────────┐
│ CorrelationId    │    │   Permissions    │     │  Notifications   │
│ CreatedAt        │    │──────────────────│     │──────────────────│
└──────────────────┘    │ Id (PK)          │     │ Id (PK)          │
                        │ UserId (FK)      │     │ UserId (FK)      │
                        │ Module           │     │ Title            │
                        │ CanView          │     │ Message          │
                        │ CanCreate        │     │ IsRead           │
                        │ CanEdit          │     │ CreatedAt        │
                        │ CanDelete        │     └──────────────────┘
                        └──────────────────┘

* PII fields encrypted with AES-256
```

## Relationship Summary

| Entity | Relationships |
|--------|---------------|
| Company | Has many: Employees, CompanyBranches, Departments |
| Employee | Has many: Payslips, Attendances, LeaveRequests, Documents |
| Employee | Has one: SalaryStructure (active), EmployeeExit |
| User | Has many: RefreshTokens, Permissions |
| LeaveRequest | Belongs to: Employee, LeaveType |
| Payslip | Belongs to: Employee |

## Index Summary

| Table | Index | Purpose |
|-------|-------|---------|
| WebAttendances | `(EmployeeId, AttDate)` | Report queries |
| ExcelAttendances | `(CompanyId, AttDate)` | Monthly reports |
| Payslips | `(EmployeeId, Year, Month)` UNIQUE | One payslip per period |
| LeaveRequests | `(EmployeeId, Status)` | Pending leave queries |
| Employees | `(IsActive, CompanyId)` | Active employee lists |
| SalaryStructures | `(EmployeeId, IsActive)` | Active salary lookup |
| RefreshTokens | `Token` UNIQUE | Token validation |
