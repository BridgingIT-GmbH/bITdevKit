// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.Modules.Core.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "__Outbox_DomainEvents",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                name: "ForecastTypes",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1048)", maxLength: 1048, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestGuidEntities",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MyProperty1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MyProperty2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MyProperty3 = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AuditState_CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_CreatedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_UpdatedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_UpdatedReasons = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    AuditState_Deactivated = table.Column<bool>(type: "bit", nullable: true),
                    AuditState_DeactivatedReasons = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    AuditState_DeactivatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_DeactivatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_DeactivatedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_Deleted = table.Column<bool>(type: "bit", nullable: true),
                    AuditState_DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_DeletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_DeletedReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_DeletedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGuidEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestIntEntities",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MyProperty1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MyProperty2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MyProperty3 = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AuditState_CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_CreatedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_UpdatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_UpdatedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_UpdatedReasons = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    AuditState_Deactivated = table.Column<bool>(type: "bit", nullable: true),
                    AuditState_DeactivatedReasons = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: true),
                    AuditState_DeactivatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_DeactivatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_DeactivatedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_Deleted = table.Column<bool>(type: "bit", nullable: true),
                    AuditState_DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuditState_DeletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditState_DeletedReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AuditState_DeletedDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestIntEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                schema: "core",
                columns: table => new
                {
                    Identifier = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Visits = table.Column<int>(type: "int", nullable: false),
                    LastVisitDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RegisterDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AdDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Identifier);
                });

            migrationBuilder.CreateTable(
                name: "Forecasts",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemperatureMin = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    TemperatureMax = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    WindSpeed = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    TypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forecasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Forecasts_ForecastTypes_TypeId",
                        column: x => x.TypeId,
                        principalSchema: "core",
                        principalTable: "ForecastTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TestGuidChildEntities",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestGuidEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MyProperty1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MyProperty2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGuidChildEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestGuidChildEntities_TestGuidEntities_TestGuidEntityId",
                        column: x => x.TestGuidEntityId,
                        principalSchema: "core",
                        principalTable: "TestGuidEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestIntChildEntities",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestIntEntityId = table.Column<int>(type: "int", nullable: false),
                    MyProperty1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MyProperty2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestIntChildEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestIntChildEntities_TestIntEntities_TestIntEntityId",
                        column: x => x.TestIntEntityId,
                        principalSchema: "core",
                        principalTable: "TestIntEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "ForecastTypes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { new Guid("102954ff-aa73-495b-a730-98f2d5ca10f3"), "test", "AAA" },
                    { new Guid("f059e932-d6ff-406d-ba9d-282fe4fdc084"), "test", "BBB" }
                });

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_CreatedDate",
                schema: "core",
                table: "__Outbox_DomainEvents",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_EventId",
                schema: "core",
                table: "__Outbox_DomainEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_ProcessedDate",
                schema: "core",
                table: "__Outbox_DomainEvents",
                column: "ProcessedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_DomainEvents_Type",
                schema: "core",
                table: "__Outbox_DomainEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_CreatedDate",
                schema: "core",
                table: "__Outbox_Messages",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_MessageId",
                schema: "core",
                table: "__Outbox_Messages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_ProcessedDate",
                schema: "core",
                table: "__Outbox_Messages",
                column: "ProcessedDate");

            migrationBuilder.CreateIndex(
                name: "IX___Outbox_Messages_Type",
                schema: "core",
                table: "__Outbox_Messages",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Forecasts_TypeId",
                schema: "core",
                table: "Forecasts",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastTypes_Name",
                schema: "core",
                table: "ForecastTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TestGuidChildEntities_TestGuidEntityId",
                schema: "core",
                table: "TestGuidChildEntities",
                column: "TestGuidEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TestIntChildEntities_TestIntEntityId",
                schema: "core",
                table: "TestIntChildEntities",
                column: "TestIntEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__Outbox_DomainEvents",
                schema: "core");

            migrationBuilder.DropTable(
                name: "__Outbox_Messages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Forecasts",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TestGuidChildEntities",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TestIntChildEntities",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserAccounts",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ForecastTypes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TestGuidEntities",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TestIntEntities",
                schema: "core");
        }
    }
}
