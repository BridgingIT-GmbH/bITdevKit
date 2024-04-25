using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BridgingIT.DevKit.Examples.EventSourcing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AggregateEvent",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateVersion = table.Column<int>(type: "int", nullable: false),
                    Identifier = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregateEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventstoreOutbox",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Aggregate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AggregateEvent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    RetryAttempt = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventstoreOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Snapshot",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AggregateEvent_AggregateId",
                schema: "dbo",
                table: "AggregateEvent",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_AggregateEvent_AggregateIDVersion",
                schema: "dbo",
                table: "AggregateEvent",
                columns: new[] { "AggregateId", "AggregateVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_AggregateEvent_AggregateType",
                schema: "dbo",
                table: "AggregateEvent",
                column: "AggregateType");

            migrationBuilder.CreateIndex(
                name: "IX_AggregateEvent_IX_AggregateEvent_AggregateIDAggrTypeVers",
                schema: "dbo",
                table: "AggregateEvent",
                columns: new[] { "AggregateId", "AggregateType", "AggregateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventstoreOutbox_TimeStamp",
                schema: "dbo",
                table: "EventstoreOutbox",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshot_Id_AggregateType",
                schema: "dbo",
                table: "Snapshot",
                columns: new[] { "Id", "AggregateType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AggregateEvent",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EventstoreOutbox",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Snapshot",
                schema: "dbo");
        }
    }
}
