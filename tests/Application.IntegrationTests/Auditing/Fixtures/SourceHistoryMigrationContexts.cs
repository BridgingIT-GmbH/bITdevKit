// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

using Domain.Model;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public abstract class SourceHistoryMigrationContext(DbContextOptions options) : DbContext(options), IChangeHistoryContext
{
    public DbSet<HistoryCustomer> Customers { get; set; }

    public DbSet<ChangeHistoryEntry> History { get; set; }

    public DbSet<ChangeHistoryEntry> ChangeHistory { get => this.History; set => this.History = value; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistoryCustomer>().HasKey(customer => customer.Id);
        modelBuilder.Entity<HistoryCustomer>().HasIndex(customer => customer.Code).IsUnique();
    }
}

public sealed class HistoryCustomer : Entity<Guid>
{
    public string Name { get; set; }

    public string Code { get; set; }
}

public sealed class SqlServerSourceHistoryContext(DbContextOptions<SqlServerSourceHistoryContext> options) : SourceHistoryMigrationContext(options);

public sealed class PostgresSourceHistoryContext(DbContextOptions<PostgresSourceHistoryContext> options) : SourceHistoryMigrationContext(options);

public sealed class SqliteSourceHistoryContext(DbContextOptions<SqliteSourceHistoryContext> options) : SourceHistoryMigrationContext(options);

public sealed class SqlServerSourceDesignFactory : IDesignTimeDbContextFactory<SqlServerSourceHistoryContext>
{
    public SqlServerSourceHistoryContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqlServerSourceHistoryContext>()
        .UseSqlServer("Server=localhost;Database=AuditingSourceDesign;Integrated Security=true;TrustServerCertificate=true").Options);
}

public sealed class PostgresSourceDesignFactory : IDesignTimeDbContextFactory<PostgresSourceHistoryContext>
{
    public PostgresSourceHistoryContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresSourceHistoryContext>()
        .UseNpgsql("Host=localhost;Database=AuditingSourceDesign;Username=auditing").Options);
}

public sealed class SqliteSourceDesignFactory : IDesignTimeDbContextFactory<SqliteSourceHistoryContext>
{
    public SqliteSourceHistoryContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqliteSourceHistoryContext>()
        .UseSqlite("Data Source=auditing-source-design.db").Options);
}
