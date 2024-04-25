namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class RemoveCommandType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CommandType",
            schema: "dbo",
            table: "EventstoreOutbox");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CommandType",
            schema: "dbo",
            table: "EventstoreOutbox",
            type: "nvarchar(max)",
            nullable: true);
    }
}
