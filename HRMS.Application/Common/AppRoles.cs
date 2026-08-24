namespace HRMS.Application.Common;

public static class AppRoles
{
    public const string SuperAdmin = "superadmin";
    public const string Admin      = "admin";
    public const string Employee   = "employee";
    public const string SalesManager    = "sales_manager";
    public const string SalesExecutive  = "sales_executive";
    public const string HrAdmin         = "HR Admin";
    public const string LegacyAdmin     = "Admin";
    public const string SupportAgent    = "Support Agent";

    public const string AdminAndSuperAdmin      = Admin + "," + SuperAdmin;
    public const string SuperAdminAndAdmin      = SuperAdmin + "," + Admin;
    public const string AdminSuperAdminEmployee = Admin + "," + SuperAdmin + "," + Employee;
    public const string AdminSuperAdminSales    = Admin + "," + SuperAdmin + "," + SalesManager + "," + SalesExecutive;
    public const string AdminSuperAdminSalesManagers = Admin + "," + SuperAdmin + "," + SalesManager;
    public const string HrAdminAndAdmin         = HrAdmin + "," + LegacyAdmin;
    public const string HrAdminAdminSupport     = HrAdmin + "," + LegacyAdmin + "," + SupportAgent;
}
