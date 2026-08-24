using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations.MySql
{
    /// <inheritdoc />
    /// <summary>
    /// DB-2: EF Core's MySQL/Pomelo provider falls back to its maximum-precision
    /// convention, decimal(65,30), for any decimal property left without an explicit
    /// precision/scale. Ten columns across five tables were left unconfigured this way.
    /// This migration narrows them to decimal(14,2), matching every other INR currency
    /// column in the schema (salary, deductions, bonuses, etc.) and removing the
    /// unnecessary storage/index overhead and truncation risk of 30 fractional digits.
    /// </summary>
    public partial class DB2_DecimalPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "hours_worked",
                table: "excel_attendances",
                type: "decimal(14,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "gratuity_amount",
                table: "employee_exits",
                type: "decimal(14,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "settlement_amount",
                table: "employee_exits",
                type: "decimal(14,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "salary_increment",
                table: "employee_promotions",
                type: "decimal(14,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "total_amount",
                table: "expense_claims",
                type: "decimal(14,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_gst",
                table: "expense_claims",
                type: "decimal(14,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "expense_items",
                type: "decimal(14,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "gst_amount",
                table: "expense_items",
                type: "decimal(14,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "advance_amount",
                table: "travel_requests",
                type: "decimal(14,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_cost",
                table: "travel_requests",
                type: "decimal(14,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "hours_worked",
                table: "excel_attendances",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "gratuity_amount",
                table: "employee_exits",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "settlement_amount",
                table: "employee_exits",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "salary_increment",
                table: "employee_promotions",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "total_amount",
                table: "expense_claims",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_gst",
                table: "expense_claims",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "expense_items",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "gst_amount",
                table: "expense_items",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "advance_amount",
                table: "travel_requests",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_cost",
                table: "travel_requests",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,2)");
        }
    }
}
