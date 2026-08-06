using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmKanban.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualValue",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedValue",
                table: "Tickets",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualValue",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EstimatedValue",
                table: "Tickets");
        }
    }
}
