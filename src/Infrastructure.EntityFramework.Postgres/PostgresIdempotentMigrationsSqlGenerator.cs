// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

/// <summary>
///     A custom PostgreSQL <see cref="MigrationsSqlGenerator"/> that emits
///     idempotent DDL statements using native <c>IF [NOT] EXISTS</c> clauses or
///     <c>DO $$ BEGIN ... END $$;</c> blocks.
/// </summary>
/// <remarks>
///     Register via:
///     <code>
///     options.UseNpgsql(connectionString)
///            .ReplaceService&lt;IMigrationsSqlGenerator, IdempotentPostgresMigrationsSqlGenerator&gt;();
///     </code>
/// </remarks>
public class PostgresIdempotentMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
#pragma warning disable EF1001
    INpgsqlSingletonOptions npgsqlSingletonOptions)
#pragma warning restore EF1001
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
    /// <summary>
    ///     Captures SQL text from a temporary inner builder to preserve the base
    ///     generator’s SQL without forcing new command boundaries.
    /// </summary>
    private string CaptureInnerSql(Action<MigrationCommandListBuilder> generateSql)
    {
        var inner = new MigrationCommandListBuilder(this.Dependencies);
        generateSql(inner);
        return string.Join(Environment.NewLine, inner.GetCommandList().Select(c => c.CommandText));
    }

    // ---------- SCHEMAS ----------

    /// <inheritdoc />
    protected override void Generate(
        EnsureSchemaOperation operation,
        IModel model,
        MigrationCommandListBuilder builder)
    {
        var schema = operation.Name;
        if (string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase))
            return;

        builder.Append($"CREATE SCHEMA IF NOT EXISTS {this.Dependencies.SqlGenerationHelper.DelimitIdentifier(schema)}")
               .AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator)
               .EndCommand();
    }

    // ---------- TABLES ----------

    /// <inheritdoc />
    protected override void Generate(
    CreateTableOperation operation,
    IModel model,
    MigrationCommandListBuilder builder,
    bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var table = operation.Name;

        builder.AppendLine("DO $$ BEGIN")
               .AppendLine("IF NOT EXISTS (")
               .AppendLine("    SELECT 1")
               .AppendLine("    FROM information_schema.tables")
               .AppendLine($"    WHERE table_name = '{table}' AND table_schema = '{schema}'")
               .AppendLine(") THEN")
               .IncrementIndent();

        // direct base call (do not capture)
        base.Generate(operation, model, builder, terminate: false);

        builder.DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(
        DropTableOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var table = operation.Name;

        builder.AppendLine("DO $$ BEGIN")
               .AppendLine("IF EXISTS (")
               .AppendLine("    SELECT 1")
               .AppendLine("    FROM information_schema.tables")
               .AppendLine($"    WHERE table_name = '{table}' AND table_schema = '{schema}'")
               .AppendLine(") THEN")
               .IncrementIndent();

        // direct base call (do not capture)
        base.Generate(operation, model, builder, terminate: false);

        builder.DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    // ---------- INDEXES ----------

    /// <inheritdoc />
    protected override void Generate(
        CreateIndexOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
    bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var table = operation.Table;
        var index = operation.Name;

        builder.AppendLine("DO $$ BEGIN")
               .AppendLine("IF NOT EXISTS (")
               .AppendLine("    SELECT 1")
               .AppendLine("    FROM pg_indexes")
               .AppendLine($"    WHERE schemaname = '{schema}' AND indexname = '{index}'")
               .AppendLine(") THEN")
               .IncrementIndent();

        // direct base call (do not capture)
        base.Generate(operation, model, builder, terminate: false);

        builder.DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(
        DropIndexOperation operation,
        IModel model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var index = operation.Name;

        builder.AppendLine("DO $$ BEGIN")
               .AppendLine("IF EXISTS (")
               .AppendLine("    SELECT 1")
               .AppendLine("    FROM pg_indexes")
               .AppendLine($"    WHERE schemaname = '{schema}' AND indexname = '{index}'")
               .AppendLine(") THEN")
               .IncrementIndent();

        // direct base call (do not capture)
        base.Generate(operation, model, builder, terminate: false);

        builder.DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    // ---------- SEQUENCES ----------

    /// <inheritdoc />
    protected override void Generate(CreateSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
    {
        builder.Append("CREATE SEQUENCE IF NOT EXISTS ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema));

        var mapping = this.Dependencies.TypeMappingSource.FindMapping(operation.ClrType)
                     ?? this.Dependencies.TypeMappingSource.FindMapping(typeof(long));

        builder.Append(" START WITH ")
               .Append(mapping?.GenerateSqlLiteral(operation.StartValue) ?? operation.StartValue.ToString());

        this.SequenceOptions(operation, model, builder);
        builder.AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator);
        this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(DropSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
    {
        builder.Append("DROP SEQUENCE IF EXISTS ")
               .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema))
               .AppendLine(this.Dependencies.SqlGenerationHelper.StatementTerminator);
        this.EndStatement(builder);
    }

    // ---------- CONSTRAINTS ----------

    /// <inheritdoc />
    protected override void Generate(AddPrimaryKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i, terminate: false));

        builder.AppendLine("DO $$ BEGIN IF NOT EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(DropPrimaryKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i, terminate: false));

        builder.AppendLine("DO $$ BEGIN IF EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(AddForeignKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i, terminate: false));

        builder.AppendLine("DO $$ BEGIN IF NOT EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_type = 'FOREIGN KEY' AND constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(DropForeignKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i, terminate: false));

        builder.AppendLine("DO $$ BEGIN IF EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_type = 'FOREIGN KEY' AND constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;");

        if (terminate)
            this.EndStatement(builder);
    }

    /// <inheritdoc />
    protected override void Generate(AddUniqueConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i));

        builder.AppendLine("DO $$ BEGIN IF NOT EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_type = 'UNIQUE' AND constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;")
               .EndCommand();
    }

    /// <inheritdoc />
    protected override void Generate(DropUniqueConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i));

        builder.AppendLine("DO $$ BEGIN IF EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_type = 'UNIQUE' AND constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;")
               .EndCommand();
    }

    /// <inheritdoc />
    protected override void Generate(AddCheckConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i));

        builder.AppendLine("DO $$ BEGIN IF NOT EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_type = 'CHECK' AND constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;")
               .EndCommand();
    }

    /// <inheritdoc />
    protected override void Generate(DropCheckConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
    {
        var schema = operation.Schema ?? "public";
        var name = operation.Name;
        var innerSql = this.CaptureInnerSql(i => base.Generate(operation, model, i));

        builder.AppendLine("DO $$ BEGIN IF EXISTS (")
               .AppendLine("    SELECT 1 FROM information_schema.table_constraints")
               .AppendLine($"    WHERE constraint_type = 'CHECK' AND constraint_name = '{name}' AND table_schema = '{schema}') THEN")
               .IncrementIndent()
               .AppendLine(innerSql.TrimEnd())
               .DecrementIndent()
               .AppendLine("END IF; END $$;")
               .EndCommand();
    }
}