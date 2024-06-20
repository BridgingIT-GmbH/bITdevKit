// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using System;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Snapshot",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Data = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                AggregateType = table.Column<string>(type: "nvarchar(450)", nullable: true),
                SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Snapshot", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Snapshot_Id_AggregateType",
            schema: "dbo",
            table: "Snapshot",
            columns: new[] { "Id", "AggregateType" },
            unique: true,
            filter: "[AggregateType] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Snapshot",
            schema: "dbo");

        migrationBuilder.DropIndex(
            name: "IX_AggregateEvent_IX_AggregateEvent_AggregateIDAggrTypeVers",
            schema: "dbo",
            table: "AggregateEvent");
    }
}
