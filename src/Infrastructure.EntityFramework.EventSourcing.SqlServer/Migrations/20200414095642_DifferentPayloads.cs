// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class DifferentPayloads : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Payload",
            schema: "dbo",
            table: "EventstoreOutbox");

        migrationBuilder.AddColumn<string>(
            name: "Aggregate",
            schema: "dbo",
            table: "EventstoreOutbox",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AggregateEvent",
            schema: "dbo",
            table: "EventstoreOutbox",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Aggregate",
            schema: "dbo",
            table: "EventstoreOutbox");

        migrationBuilder.DropColumn(
            name: "AggregateEvent",
            schema: "dbo",
            table: "EventstoreOutbox");

        migrationBuilder.AddColumn<string>(
            name: "Payload",
            schema: "dbo",
            table: "EventstoreOutbox",
            type: "nvarchar(max)",
            nullable: true);
    }
}
