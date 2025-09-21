using Microsoft.EntityFrameworkCore.Migrations;

namespace Pjx_Api.Migrations
{
    public partial class SeedDeptOrg : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "OrganizationId", "Name", "OwnerUserId" },
                values: new object[] { 1, "Default Organization", null });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "DepartmentId", "Name", "OrganizationId" },
                values: new object[] { 1, "Default Department A", 1 });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "DepartmentId", "Name", "OrganizationId" },
                values: new object[] { 2, "Default Department B", 1 });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Organizations",
                keyColumn: "OrganizationId",
                keyValue: 1);
        }
    }
}
