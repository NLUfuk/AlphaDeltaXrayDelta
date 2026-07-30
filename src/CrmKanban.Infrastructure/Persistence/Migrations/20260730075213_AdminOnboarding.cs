using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmKanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanCreateCompany",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanCreateCompany",
                table: "Users");
        }
    }
}
