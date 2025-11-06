// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
///     A minimal idempotent SQLite <see cref="MigrationsSqlGenerator"/> that emits
///     <c>CREATE</c> / <c>DROP</c> statements with SQLite's native
///     <c>IF NOT EXISTS</c> / <c>IF EXISTS</c> clauses for tables and indexes.
///     Other operations use the provider's default behavior.
/// </summary>
/// <remarks>
///     Register via:
///     <code>
///     options.UseSqlite(connectionString)
///            .ReplaceService&lt;IMigrationsSqlGenerator, IdempotentSqliteMigrationsSqlGenerator&gt;();
///     </code>
/// </remarks>
public class SqliteIdempotentMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IRelationalAnnotationProvider migrationsAnnotations)
    : SqliteMigrationsSqlGenerator(dependencies, migrationsAnnotations)
{
    // ---------- TABLES ----------

    /// <inheritdoc />
    protected override void Generate(
        CreateTableOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        builder.AppendLine("-- Create Table");
        builder.AppendLine();

        builder.Append("CREATE TABLE IF NOT EXISTS ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
               .AppendLine(" (");

        using (builder.Indent())
        {
            this.CreateTableColumns(operation, model, builder);
            this.CreateTableConstraints(operation, model, builder);
            builder.AppendLine();
        }

        builder.Append(")");

        if (terminate)
        {
            builder.AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator);
            this.EndStatement(builder);
        }
    }

    /// <inheritdoc />
    protected override void Generate(
        DropTableOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        builder.AppendLine("-- Drop Table");
        builder.AppendLine();

        builder.Append("DROP TABLE IF EXISTS ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

        if (terminate)
        {
            builder.AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator);
            this.EndStatement(builder);
        }
    }

    // ---------- INDEXES ----------

    /// <inheritdoc />
    protected override void Generate(
        CreateIndexOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        builder.AppendLine("-- Create Index");
        builder.AppendLine();

        builder.Append("CREATE ");
        if (operation.IsUnique)
        {
            builder.Append("UNIQUE ");
        }

        builder.Append("INDEX IF NOT EXISTS ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
               .Append(" ON ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table))
               .Append(" (");

        this.GenerateIndexColumnList(operation, model, builder);
        builder.Append(")");

        if (terminate)
        {
            builder.AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator);
            this.EndStatement(builder);
        }
    }

    /// <inheritdoc />
    protected override void Generate(
        DropIndexOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        builder.AppendLine("-- Drop Index");
        builder.AppendLine();

        builder.Append("DROP INDEX IF EXISTS ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

        if (terminate)
        {
            builder.AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator);
            this.EndStatement(builder);
        }
    }
}