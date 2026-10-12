using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class SplitFullName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded as DropColumn first, which would lose every name. Add, copy, then drop.
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // EXEC: in an idempotent script this compiles only when it runs (the columns it names
            // don't exist yet, or no longer exist, when the script is compiled).
            migrationBuilder.Sql("""
                EXEC(N'UPDATE Customers
                SET FirstName = LEFT(FullName, CHARINDEX('' '', FullName + '' '') - 1),
                    LastName = LTRIM(SUBSTRING(FullName, CHARINDEX('' '', FullName + '' '') + 1, 100))')
                """);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("EXEC(N'UPDATE Customers SET FullName = TRIM(FirstName + '' '' + LastName)')");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Customers");
        }
    }
}
