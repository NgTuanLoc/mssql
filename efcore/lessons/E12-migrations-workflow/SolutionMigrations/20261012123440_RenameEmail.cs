using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreLessons.E12.Solution.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded as DropColumn + AddColumn, which would delete every email. Rename, then widen.
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Customers",
                newName: "ContactEmail");

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Customers",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(320)",
                oldMaxLength: 320);

            migrationBuilder.RenameColumn(
                name: "ContactEmail",
                table: "Customers",
                newName: "Email");
        }
    }
}
