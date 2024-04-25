namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

public partial class InitialCreateForOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EventstoreOutbox",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                AggregateId = table.Column<Guid>(nullable: false),
                AggregateType = table.Column<string>(nullable: true),
                EventType = table.Column<string>(nullable: true),
                Payload = table.Column<string>(nullable: true),
                TimeStamp = table.Column<DateTime>(nullable: false),
                IsProcessed = table.Column<bool>(nullable: false),
                RetryAttempt = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventstoreOutbox", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EventstoreOutbox",
            schema: "dbo");
    }
}
