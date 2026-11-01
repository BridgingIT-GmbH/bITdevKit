// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

using Infrastructure.EntityFramework.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// These explicit test-host contexts generate and execute real reviewed migrations.
// Production applications own their own migrations and design-time factories.
public abstract class AuditMigrationContext(DbContextOptions options) : DbContext(options), IAuditingDbContext
{
    public DbSet<AuditRecordEntity> AuditRecords { get; set; }

    public DbSet<AuditActionEntity> AuditActions { get; set; }

    public DbSet<AuditEntityChangeEntity> AuditEntityChanges { get; set; }

    public DbSet<AuditStoreStateEntity> AuditStoreState { get; set; }
}

public sealed class SqlServerAuditMigrationContext(DbContextOptions<SqlServerAuditMigrationContext> options) : AuditMigrationContext(options);

public sealed class PostgresAuditMigrationContext(DbContextOptions<PostgresAuditMigrationContext> options) : AuditMigrationContext(options);

public sealed class SqliteAuditMigrationContext(DbContextOptions<SqliteAuditMigrationContext> options) : AuditMigrationContext(options);

public sealed class SqlServerAuditDesignFactory : IDesignTimeDbContextFactory<SqlServerAuditMigrationContext>
{
    public SqlServerAuditMigrationContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqlServerAuditMigrationContext>()
        .UseSqlServer("Server=localhost;Database=AuditingDesign;Integrated Security=true;TrustServerCertificate=true").Options);
}

public sealed class PostgresAuditDesignFactory : IDesignTimeDbContextFactory<PostgresAuditMigrationContext>
{
    public PostgresAuditMigrationContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresAuditMigrationContext>()
        .UseNpgsql("Host=localhost;Database=AuditingDesign;Username=auditing").Options);
}

public sealed class SqliteAuditDesignFactory : IDesignTimeDbContextFactory<SqliteAuditMigrationContext>
{
    public SqliteAuditMigrationContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqliteAuditMigrationContext>()
        .UseSqlite("Data Source=auditing-design.db").Options);
}
