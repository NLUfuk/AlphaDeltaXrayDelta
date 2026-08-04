using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmKanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSignInCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Invitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Invitations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Invitations");
        }
    }
}
