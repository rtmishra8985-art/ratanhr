using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations.MySql
{
    /// <inheritdoc />
    /// <summary>
    /// Item 6: folds db_performance.sql, db_indexes_fix.sql and db_softdelete_fix.sql
    /// into the EF migration chain so index/soft-delete state is versioned with the
    /// model instead of being applied out-of-band by hand-run scripts.
    ///
    /// Notes:
    ///  * Indexes that already existed in the baseline schema are not re-created
    ///    (employees.company_id, web_attendances.att_date, the unique
    ///    web_attendances(employee_id, att_date), the unique
    ///    payslips(company_id, employee_id, month, year) and
    ///    helpdesk_tickets.raised_by_employee_id).
    ///  * The scripts referenced objects that do not exist in this schema
    ///    (table web_attendance, table training_records, column
    ///    web_attendances.attendance_date, column assets.employee_id); those are
    ///    remapped here to the real names (web_attendances, training_enrollments,
    ///    att_date, assigned_to_employee_id).
    ///  * The soft-delete COLUMNS from db_softdelete_fix.sql (users.is_deleted,
    ///    users.deleted_at, assets/appreciations/helpdesk_tickets deleted_at and
    ///    updated_at, onboarding_records.deleted_at) are already present in
    ///    20260810080843_MySqlBaselineSchema, so only their supporting indexes are
    ///    added here.
    /// </summary>
    public partial class FoldDbScriptIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MIG-ORD-01 (run 6 audit remediation): MySQL cannot index a BLOB/TEXT
            // column without a prefix length. These employee_id columns are mapped as
            // unbounded strings by the baseline schema (longtext), so every CreateIndex
            // below over them fails with error 1170 on a clean database. Widen them to
            // varchar(255) here, before the indexes are created. The later
            // 20260812072330_AuditRemediation20260812ModelSync migration re-asserts the
            // same varchar(255) shape and is a no-op once this has run.
            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "appreciations",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "bonuses",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "deductions",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_documents",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_exits",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_promotions",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_transfers",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "training_enrollments",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_employees_user_id",
                table: "employees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payslips_employee_id",
                table: "payslips",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_payslips_month_year",
                table: "payslips",
                columns: new[] { "month", "year" });

            migrationBuilder.CreateIndex(
                name: "ix_bonuses_employee_id",
                table: "bonuses",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_bonuses_company_employee",
                table: "bonuses",
                columns: new[] { "company_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deductions_employee_id",
                table: "deductions",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_deductions_company_employee",
                table: "deductions",
                columns: new[] { "company_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_employee_id",
                table: "employee_documents",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_transfers_employee_id",
                table: "employee_transfers",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_promotions_employee_id",
                table: "employee_promotions",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_exits_employee_id",
                table: "employee_exits",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_assigned_to_employee_id",
                table: "assets",
                column: "assigned_to_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_company_deleted",
                table: "assets",
                columns: new[] { "company_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_training_enrollments_employee_id",
                table: "training_enrollments",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_web_attendances_company_employee",
                table: "web_attendances",
                columns: new[] { "company_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_company_employee",
                table: "leave_requests",
                columns: new[] { "company_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_start_end",
                table: "leave_requests",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_users_is_deleted",
                table: "users",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_appreciations_employee_deleted",
                table: "appreciations",
                columns: new[] { "employee_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_helpdesk_tickets_company_deleted",
                table: "helpdesk_tickets",
                columns: new[] { "company_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_records_employee_deleted",
                table: "onboarding_records",
                columns: new[] { "employee_id", "deleted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_employees_user_id", table: "employees");
            migrationBuilder.DropIndex(name: "ix_payslips_employee_id", table: "payslips");
            migrationBuilder.DropIndex(name: "ix_payslips_month_year", table: "payslips");
            migrationBuilder.DropIndex(name: "ix_bonuses_employee_id", table: "bonuses");
            migrationBuilder.DropIndex(name: "ix_bonuses_company_employee", table: "bonuses");
            migrationBuilder.DropIndex(name: "ix_deductions_employee_id", table: "deductions");
            migrationBuilder.DropIndex(name: "ix_deductions_company_employee", table: "deductions");
            migrationBuilder.DropIndex(name: "ix_employee_documents_employee_id", table: "employee_documents");
            migrationBuilder.DropIndex(name: "ix_employee_transfers_employee_id", table: "employee_transfers");
            migrationBuilder.DropIndex(name: "ix_employee_promotions_employee_id", table: "employee_promotions");
            migrationBuilder.DropIndex(name: "ix_employee_exits_employee_id", table: "employee_exits");
            migrationBuilder.DropIndex(name: "ix_refresh_tokens_user_id", table: "refresh_tokens");
            migrationBuilder.DropIndex(name: "ix_password_reset_tokens_user_id", table: "password_reset_tokens");
            migrationBuilder.DropIndex(name: "ix_assets_assigned_to_employee_id", table: "assets");
            migrationBuilder.DropIndex(name: "ix_assets_company_deleted", table: "assets");
            migrationBuilder.DropIndex(name: "ix_training_enrollments_employee_id", table: "training_enrollments");
            migrationBuilder.DropIndex(name: "ix_web_attendances_company_employee", table: "web_attendances");
            migrationBuilder.DropIndex(name: "ix_leave_requests_company_employee", table: "leave_requests");
            migrationBuilder.DropIndex(name: "ix_leave_requests_start_end", table: "leave_requests");
            migrationBuilder.DropIndex(name: "ix_users_is_deleted", table: "users");
            migrationBuilder.DropIndex(name: "ix_appreciations_employee_deleted", table: "appreciations");
            migrationBuilder.DropIndex(name: "ix_helpdesk_tickets_company_deleted", table: "helpdesk_tickets");
            migrationBuilder.DropIndex(name: "ix_onboarding_records_employee_deleted", table: "onboarding_records");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "appreciations",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "bonuses",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "deductions",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_documents",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_exits",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_promotions",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "employee_transfers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "employee_id",
                table: "training_enrollments",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
