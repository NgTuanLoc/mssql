using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The scaffolded default would make every existing customer inactive. They were all active.
            // EXEC: in an idempotent script this compiles only when it runs, after IsActive exists.
            migrationBuilder.Sql("EXEC(N'UPDATE Customers SET IsActive = 1')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Customers");
        }
    }
}
