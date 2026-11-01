using BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialAuditingSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "__Auditing_Records",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RootJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SearchJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<long>(type: "bigint", nullable: false),
                    StorageState = table.Column<int>(type: "int", nullable: false),
                    ArchivedUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    StartedUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    CompletedUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModuleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Completeness = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OriginAuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CausationAuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InitiatingUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitiatingActorJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalActionCount = table.Column<long>(type: "bigint", nullable: false),
                    RetainedActionCount = table.Column<long>(type: "bigint", nullable: false),
                    ChangeCount = table.Column<long>(type: "bigint", nullable: false),
                    HasFailedActions = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Auditing_Records", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "__Auditing_StoreState",
                columns: table => new
                {
                    Singleton = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreEpoch = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedPosition = table.Column<long>(type: "bigint", nullable: false),
                    LifecycleGeneration = table.Column<long>(type: "bigint", nullable: false),
                    RemovedThroughPosition = table.Column<long>(type: "bigint", nullable: false),
                    RejectCompletedBeforeUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    ArchiveCompletedBeforeUtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Auditing_StoreState", x => x.Singleton);
                    table.CheckConstraint("CK___Auditing_StoreState_Singleton", "\"Singleton\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "__Auditing_Actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    StartedUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    CompletedUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    DocumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Auditing_Actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK___Auditing_Actions___Auditing_Records_AuditId",
                        column: x => x.AuditId,
                        principalTable: "__Auditing_Records",
                        principalColumn: "AuditId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "__Auditing_EntityChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    TimestampUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PersistenceState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DocumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Auditing_EntityChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK___Auditing_EntityChanges___Auditing_Records_AuditId",
                        column: x => x.AuditId,
                        principalTable: "__Auditing_Records",
                        principalColumn: "AuditId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_Actions_AuditId_Ordinal",
                table: "__Auditing_Actions",
                columns: new[] { "AuditId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_EntityChanges_AuditId_Ordinal",
                table: "__Auditing_EntityChanges",
                columns: new[] { "AuditId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_Records_CausationAuditId",
                table: "__Auditing_Records",
                column: "CausationAuditId");

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_Records_OriginAuditId",
                table: "__Auditing_Records",
                column: "OriginAuditId");

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_Records_Position",
                table: "__Auditing_Records",
                column: "Position",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_Records_StorageState_CompletedUtcTicks",
                table: "__Auditing_Records",
                columns: new[] { "StorageState", "CompletedUtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX___Auditing_Records_StorageState_StartedUtcTicks_AuditId",
                table: "__Auditing_Records",
                columns: new[] { "StorageState", "StartedUtcTicks", "AuditId" });
            migrationBuilder.InitializeAuditingStore();
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__Auditing_Actions");

            migrationBuilder.DropTable(
                name: "__Auditing_EntityChanges");

            migrationBuilder.DropTable(
                name: "__Auditing_StoreState");

            migrationBuilder.DropTable(
                name: "__Auditing_Records");
        }
    }
}
