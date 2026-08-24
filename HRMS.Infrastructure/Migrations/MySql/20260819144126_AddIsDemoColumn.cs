using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddIsDemoColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PHASE FOUR: Demo Mode Implementation ───────────────────────────────────
            // Add IsDemo column to all company-scoped tables to safely identify demo records.
            // Default = false ensures all existing production data remains unmarked.
            // Demo seeding will set IsDemo = true for all generated test data.
            // Cleanup operations can safely delete only where IsDemo = true AND CompanyId in (1-5).

            // Companies table — top-level demo company marker
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "companies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_companies_is_demo",
                table: "companies",
                column: "is_demo");

            // Users table — demo user accounts
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_users_is_demo",
                table: "users",
                column: "is_demo");

            // Employees table — demo employee records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "employees",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_employees_is_demo",
                table: "employees",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_employees_company_is_demo",
                table: "employees",
                columns: new[] { "company_id", "is_demo" });

            // Web Attendance table — demo attendance records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "web_attendances",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_web_attendances_is_demo",
                table: "web_attendances",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_web_attendances_company_is_demo",
                table: "web_attendances",
                columns: new[] { "company_id", "is_demo" });

            // Leave Requests table — demo leave records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "leave_requests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_is_demo",
                table: "leave_requests",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_company_is_demo",
                table: "leave_requests",
                columns: new[] { "company_id", "is_demo" });

            // Payslips table — demo payroll records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "payslips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_payslips_is_demo",
                table: "payslips",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_payslips_company_is_demo",
                table: "payslips",
                columns: new[] { "company_id", "is_demo" });

            // Bonuses table — demo bonus records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "bonuses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_bonuses_is_demo",
                table: "bonuses",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_bonuses_company_is_demo",
                table: "bonuses",
                columns: new[] { "company_id", "is_demo" });

            // Deductions table — demo deduction records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "deductions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_deductions_is_demo",
                table: "deductions",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_deductions_company_is_demo",
                table: "deductions",
                columns: new[] { "company_id", "is_demo" });

            // Assets table — demo asset records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "assets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_assets_is_demo",
                table: "assets",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_assets_company_is_demo",
                table: "assets",
                columns: new[] { "company_id", "is_demo" });

            // Candidates table — demo recruitment candidates
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "candidates",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_is_demo",
                table: "candidates",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_company_is_demo",
                table: "candidates",
                columns: new[] { "company_id", "is_demo" });

            // Salary Structures table — demo salary structure records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "salary_structures",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_salary_structures_is_demo",
                table: "salary_structures",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_salary_structures_company_is_demo",
                table: "salary_structures",
                columns: new[] { "company_id", "is_demo" });

            // Leave Balances table — demo leave balance records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "leave_balances",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_leave_balances_is_demo",
                table: "leave_balances",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_leave_balances_company_is_demo",
                table: "leave_balances",
                columns: new[] { "company_id", "is_demo" });

            // Training Enrollments table — demo training enrollment records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "training_enrollments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_training_enrollments_is_demo",
                table: "training_enrollments",
                column: "is_demo");

            // Performance Reviews table — demo performance review records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "performance_reviews",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_performance_reviews_is_demo",
                table: "performance_reviews",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_performance_reviews_company_is_demo",
                table: "performance_reviews",
                columns: new[] { "company_id", "is_demo" });

            // Employee Skills table (new Phase 4 table) — demo skill records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "employee_skills",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_employee_skills_is_demo",
                table: "employee_skills",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_employee_skills_company_is_demo",
                table: "employee_skills",
                columns: new[] { "company_id", "is_demo" });

            // Project Assignments table (new Phase 4 table) — demo project assignment records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "project_assignments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_project_assignments_is_demo",
                table: "project_assignments",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_project_assignments_company_is_demo",
                table: "project_assignments",
                columns: new[] { "company_id", "is_demo" });

            // Award Recognition table (new Phase 4 table) — demo award records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "award_recognitions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_award_recognitions_is_demo",
                table: "award_recognitions",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_award_recognitions_company_is_demo",
                table: "award_recognitions",
                columns: new[] { "company_id", "is_demo" });

            // Announcements table — demo announcement records (if it exists)
            // Note: This table may not exist yet; migrations are additive and this will be skipped
            // if the table does not exist. MySQL does not support IF/THEN outside stored procedures
            // in plain multi-statement SQL, so this uses a stored procedure wrapper that is created,
            // invoked once, then dropped.
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS sp_add_is_demo_to_announcements;
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"
                CREATE PROCEDURE sp_add_is_demo_to_announcements()
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables
                               WHERE table_schema = DATABASE() AND table_name = 'announcements') THEN
                        ALTER TABLE announcements ADD COLUMN is_demo TINYINT(1) NOT NULL DEFAULT 0;
                        CREATE INDEX ix_announcements_is_demo ON announcements (is_demo);
                        CREATE INDEX ix_announcements_company_is_demo ON announcements (company_id, is_demo);
                    END IF;
                END
            ", suppressTransaction: true);

            migrationBuilder.Sql(@"CALL sp_add_is_demo_to_announcements();", suppressTransaction: true);

            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS sp_add_is_demo_to_announcements;", suppressTransaction: true);

            // Helpdesk Tickets table — demo helpdesk ticket records
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "helpdesk_tickets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_helpdesk_tickets_is_demo",
                table: "helpdesk_tickets",
                column: "is_demo");

            migrationBuilder.CreateIndex(
                name: "ix_helpdesk_tickets_company_is_demo",
                table: "helpdesk_tickets",
                columns: new[] { "company_id", "is_demo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: Remove IsDemo columns and indexes (production rollback path)

            migrationBuilder.DropIndex(
                name: "ix_companies_is_demo",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "ix_users_is_demo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_employees_company_is_demo",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_employees_is_demo",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_web_attendances_company_is_demo",
                table: "web_attendances");

            migrationBuilder.DropIndex(
                name: "ix_web_attendances_is_demo",
                table: "web_attendances");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "web_attendances");

            migrationBuilder.DropIndex(
                name: "ix_leave_requests_company_is_demo",
                table: "leave_requests");

            migrationBuilder.DropIndex(
                name: "ix_leave_requests_is_demo",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "leave_requests");

            migrationBuilder.DropIndex(
                name: "ix_payslips_company_is_demo",
                table: "payslips");

            migrationBuilder.DropIndex(
                name: "ix_payslips_is_demo",
                table: "payslips");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "payslips");

            migrationBuilder.DropIndex(
                name: "ix_bonuses_company_is_demo",
                table: "bonuses");

            migrationBuilder.DropIndex(
                name: "ix_bonuses_is_demo",
                table: "bonuses");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "bonuses");

            migrationBuilder.DropIndex(
                name: "ix_deductions_company_is_demo",
                table: "deductions");

            migrationBuilder.DropIndex(
                name: "ix_deductions_is_demo",
                table: "deductions");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "deductions");

            migrationBuilder.DropIndex(
                name: "ix_assets_company_is_demo",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_is_demo",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_candidates_company_is_demo",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "ix_candidates_is_demo",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "ix_salary_structures_company_is_demo",
                table: "salary_structures");

            migrationBuilder.DropIndex(
                name: "ix_salary_structures_is_demo",
                table: "salary_structures");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "salary_structures");

            migrationBuilder.DropIndex(
                name: "ix_leave_balances_company_is_demo",
                table: "leave_balances");

            migrationBuilder.DropIndex(
                name: "ix_leave_balances_is_demo",
                table: "leave_balances");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "leave_balances");

            migrationBuilder.DropIndex(
                name: "ix_training_enrollments_is_demo",
                table: "training_enrollments");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "training_enrollments");

            migrationBuilder.DropIndex(
                name: "ix_performance_reviews_company_is_demo",
                table: "performance_reviews");

            migrationBuilder.DropIndex(
                name: "ix_performance_reviews_is_demo",
                table: "performance_reviews");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "performance_reviews");

            migrationBuilder.DropIndex(
                name: "ix_employee_skills_company_is_demo",
                table: "employee_skills");

            migrationBuilder.DropIndex(
                name: "ix_employee_skills_is_demo",
                table: "employee_skills");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "employee_skills");

            migrationBuilder.DropIndex(
                name: "ix_project_assignments_company_is_demo",
                table: "project_assignments");

            migrationBuilder.DropIndex(
                name: "ix_project_assignments_is_demo",
                table: "project_assignments");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "project_assignments");

            migrationBuilder.DropIndex(
                name: "ix_award_recognitions_company_is_demo",
                table: "award_recognitions");

            migrationBuilder.DropIndex(
                name: "ix_award_recognitions_is_demo",
                table: "award_recognitions");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "award_recognitions");

            migrationBuilder.DropIndex(
                name: "ix_helpdesk_tickets_company_is_demo",
                table: "helpdesk_tickets");

            migrationBuilder.DropIndex(
                name: "ix_helpdesk_tickets_is_demo",
                table: "helpdesk_tickets");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "helpdesk_tickets");
        }
    }
}
