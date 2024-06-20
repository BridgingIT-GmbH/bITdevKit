// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class CommmandTypeAdded : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CommandType",
            schema: "dbo",
            table: "EventstoreOutbox",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CommandType",
            schema: "dbo",
            table: "EventstoreOutbox");
    }
}
