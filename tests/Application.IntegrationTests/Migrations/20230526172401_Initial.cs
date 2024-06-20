// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BridgingIT.DevKit.Application.IntegrationTests.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "__Outbox_DomainEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Outbox_DomainEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "__Outbox_Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Outbox_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "__Storage_Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    PartitionKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RowKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Storage_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_EventId",
                table: "__Outbox_DomainEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_Type",
                table: "__Outbox_DomainEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_MessageId",
                table: "__Outbox_Messages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_Type",
                table: "__Outbox_Messages",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX___Storage_Documents_PartitionKey",
                table: "__Storage_Documents",
                column: "PartitionKey");

            migrationBuilder.CreateIndex(
                name: "IX___Storage_Documents_RowKey",
                table: "__Storage_Documents",
                column: "RowKey");

            migrationBuilder.CreateIndex(
                name: "IX___Storage_Documents_Type",
                table: "__Storage_Documents",
                column: "Type");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__Outbox_DomainEvents");

            migrationBuilder.DropTable(
                name: "__Outbox_Messages");

            migrationBuilder.DropTable(
                name: "__Storage_Documents");

            migrationBuilder.DropTable(
                name: "Persons");
        }
    }
}
