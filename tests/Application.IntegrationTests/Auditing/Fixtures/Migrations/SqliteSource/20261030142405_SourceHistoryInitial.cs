using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures.Migrations.SqliteSource
{
    /// <inheritdoc />
    public partial class SourceHistoryInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "__ChangeHistory_Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeSetSequence = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    EntityClrType = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EntityIdType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    PropertyName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PropertyPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PathKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CollectionAction = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CollectionItemId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ValueClrType = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    OldValueHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    NewValueHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CaptureStrategy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CaptureSource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CaptureStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CaptureMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    BulkOperationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AffectedEntityCount = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRestoreable = table.Column<bool>(type: "INTEGER", nullable: false),
                    RestorePlanName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RestoreExecutionMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DomainRestoreHandlerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChangedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChangedByEmail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ChangedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ChangedDateTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FlowId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ModuleName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ActivityParentId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Properties = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___ChangeHistory_Entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Code = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_BulkOperationId",
                table: "__ChangeHistory_Entries",
                column: "BulkOperationId");

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_ChangedByUserId_ChangedDateTicks",
                table: "__ChangeHistory_Entries",
                columns: new[] { "ChangedByUserId", "ChangedDateTicks" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_ChangeSetId_ChangeSetSequence",
                table: "__ChangeHistory_Entries",
                columns: new[] { "ChangeSetId", "ChangeSetSequence" });

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_CorrelationId",
                table: "__ChangeHistory_Entries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_EntityType_EntityId_ChangedDateTicks",
                table: "__ChangeHistory_Entries",
                columns: new[] { "EntityType", "EntityId", "ChangedDateTicks" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_EntityType_EntityId_PropertyName_ChangedDateTicks",
                table: "__ChangeHistory_Entries",
                columns: new[] { "EntityType", "EntityId", "PropertyName", "ChangedDateTicks" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX___ChangeHistory_Entries_ModuleName_ChangedDateTicks",
                table: "__ChangeHistory_Entries",
                columns: new[] { "ModuleName", "ChangedDateTicks" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                table: "Customers",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__ChangeHistory_Entries");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
