using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Migrations.MySql
{
    /// <inheritdoc />
    /// <summary>
    /// Item 5: adds the overtime/bonus/arrears earnings columns to payslips so
    /// IndianPayrollCalculator's new inputs have somewhere to persist to.
    /// </summary>
    public partial class AddPayslipOvertimeBonusArrears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "overtime_pay",
                table: "payslips",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "bonus_amount",
                table: "payslips",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "arrears",
                table: "payslips",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "overtime_pay", table: "payslips");
            migrationBuilder.DropColumn(name: "bonus_amount", table: "payslips");
            migrationBuilder.DropColumn(name: "arrears", table: "payslips");
        }
    }
}
