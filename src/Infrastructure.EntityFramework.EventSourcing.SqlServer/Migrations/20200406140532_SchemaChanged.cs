// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

public partial class SchemaChanged : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER SCHEMA dbo TRANSFER EventStore.AggregateEvent");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER SCHEMA EventStore TRANSFER dbo.AggregateEvent");
    }
}
