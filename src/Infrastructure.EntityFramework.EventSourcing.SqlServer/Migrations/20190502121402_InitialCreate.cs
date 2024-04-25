namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            "EventStore");

        migrationBuilder.CreateTable(
            "AggregateEvent",
            schema: "EventStore",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                AggregateId = table.Column<Guid>(nullable: false),
                AggregateVersion = table.Column<int>(nullable: false),
                Data = table.Column<byte[]>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AggregateEvent", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "AggregateEvent",
            "EventStore");
    }
}