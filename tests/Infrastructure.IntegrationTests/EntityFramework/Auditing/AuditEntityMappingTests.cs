// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using System.Reflection;
using Infrastructure.EntityFramework.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

public sealed class AuditEntityMappingTests
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public void ClientDbSets_ApplyEntityMappingsWithoutModelCreatingAndMatchReviewedSchema(string backend)
    {
        using DbContext context = backend switch
        {
            "SqlServer" => new SqlServerAuditMigrationContext(new DbContextOptionsBuilder<SqlServerAuditMigrationContext>()
                .UseSqlServer("Server=localhost;Database=AuditingModel;Integrated Security=true;TrustServerCertificate=true").Options),
            "Postgres" => new PostgresAuditMigrationContext(new DbContextOptionsBuilder<PostgresAuditMigrationContext>()
                .UseNpgsql("Host=localhost;Database=AuditingModel;Username=auditing").Options),
            _ => new SqliteAuditMigrationContext(new DbContextOptionsBuilder<SqliteAuditMigrationContext>()
                .UseSqlite("Data Source=:memory:").Options)
        };
        typeof(AuditMigrationContext).GetMethod("OnModelCreating", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic).ShouldBeNull();
        var model = context.GetService<IDesignTimeModel>().Model;
        model.GetEntityTypes().Count().ShouldBe(4);
        foreach (var entity in model.GetEntityTypes()) entity.GetProperties().ShouldNotContain(property => property.IsShadowProperty());
        var root = model.FindEntityType(typeof(AuditRecordEntity));
        root.GetReferencingForeignKeys().Count().ShouldBe(2);
        root.GetReferencingForeignKeys().ShouldAllBe(foreignKey => foreignKey.IsRequired && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        var control = model.FindEntityType(typeof(AuditStoreStateEntity));
        control.FindProperty(nameof(AuditStoreStateEntity.Singleton)).ValueGenerated.ShouldBe(ValueGenerated.Never);
        control.GetCheckConstraints().Single().Sql.ShouldBe("\"Singleton\" = 1");
        var snapshotType = typeof(AuditMigrationContext).Assembly.GetTypes().Single(type =>
            typeof(ModelSnapshot).IsAssignableFrom(type) && type.GetCustomAttribute<DbContextAttribute>()?.ContextType == context.GetType());
        var snapshot = (ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true);
        var previous = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var changes = context.GetService<IMigrationsModelDiffer>().GetDifferences(previous.GetRelationalModel(), model.GetRelationalModel());
        changes.ShouldBeEmpty("Entity-owned mappings must preserve the client's existing migration schema.");
    }
}
