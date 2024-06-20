// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

public partial class IdentifierGuid : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "Identifier",
            schema: "dbo",
            table: "AggregateEvent",
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Identifier",
            schema: "dbo",
            table: "AggregateEvent",
            nullable: false);
    }
}
