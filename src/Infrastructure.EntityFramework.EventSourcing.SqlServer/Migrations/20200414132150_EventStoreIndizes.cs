namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class EventStoreIndizes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "AggregateType",
            schema: "dbo",
            table: "AggregateEvent",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EventstoreOutbox_TimeStamp",
            schema: "dbo",
            table: "EventstoreOutbox",
            column: "TimeStamp");

        migrationBuilder.CreateIndex(
            name: "IX_AggregateEvent_AggregateId",
            schema: "dbo",
            table: "AggregateEvent",
            column: "AggregateId");

        migrationBuilder.CreateIndex(
            name: "IX_AggregateEvent_AggregateType",
            schema: "dbo",
            table: "AggregateEvent",
            column: "AggregateType");

        migrationBuilder.CreateIndex(
            name: "IX_AggregateEvent_AggregateIDVersion",
            schema: "dbo",
            table: "AggregateEvent",
            columns: new[] { "AggregateId", "AggregateVersion" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EventstoreOutbox_TimeStamp",
            schema: "dbo",
            table: "EventstoreOutbox");

        migrationBuilder.DropIndex(
            name: "IX_AggregateEvent_AggregateId",
            schema: "dbo",
            table: "AggregateEvent");

        migrationBuilder.DropIndex(
            name: "IX_AggregateEvent_AggregateType",
            schema: "dbo",
            table: "AggregateEvent");

        migrationBuilder.DropIndex(
            name: "IX_AggregateEvent_AggregateIDVersion",
            schema: "dbo",
            table: "AggregateEvent");

        migrationBuilder.AlterColumn<string>(
            name: "AggregateType",
            schema: "dbo",
            table: "AggregateEvent",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldNullable: true);
    }
}
