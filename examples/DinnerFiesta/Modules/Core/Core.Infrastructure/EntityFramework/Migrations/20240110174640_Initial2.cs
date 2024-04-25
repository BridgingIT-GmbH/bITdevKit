using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class Initial2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_CreatedDate",
                schema: "core",
                table: "__Outbox_Messages",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_ProcessedDate",
                schema: "core",
                table: "__Outbox_Messages",
                column: "ProcessedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_CreatedDate",
                schema: "core",
                table: "__Outbox_DomainEvents",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_ProcessedDate",
                schema: "core",
                table: "__Outbox_DomainEvents",
                column: "ProcessedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX___Outbox_Messages_CreatedDate",
                schema: "core",
                table: "__Outbox_Messages");

            migrationBuilder.DropIndex(
                name: "IX___Outbox_Messages_ProcessedDate",
                schema: "core",
                table: "__Outbox_Messages");

            migrationBuilder.DropIndex(
                name: "IX___Outbox_DomainEvents_CreatedDate",
                schema: "core",
                table: "__Outbox_DomainEvents");

            migrationBuilder.DropIndex(
                name: "IX___Outbox_DomainEvents_ProcessedDate",
                schema: "core",
                table: "__Outbox_DomainEvents");
        }
    }
}
