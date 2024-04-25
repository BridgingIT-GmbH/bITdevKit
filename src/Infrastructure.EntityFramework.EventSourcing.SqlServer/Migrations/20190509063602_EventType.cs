namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

public partial class EventType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "EventType",
            schema: "EventStore",
            table: "AggregateEvent",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "Identifier",
            schema: "EventStore",
            table: "AggregateEvent",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            "TimeStamp",
            schema: "EventStore",
            table: "AggregateEvent",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "EventType",
            schema: "EventStore",
            table: "AggregateEvent");

        migrationBuilder.DropColumn(
            "Identifier",
            schema: "EventStore",
            table: "AggregateEvent");

        migrationBuilder.DropColumn(
            "TimeStamp",
            schema: "EventStore",
            table: "AggregateEvent");
    }
}