// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Writes prepared entity bulk insert batches through SQL Server <see cref="SqlBulkCopy"/>.
/// </summary>
/// <remarks>
/// This stateless provider is registered automatically by <c>AddSqlServerDbContext</c>. Entity metadata,
/// generated values, value conversion, and result conversion are handled by the shared bulk-insert dispatcher.
/// </remarks>
/// <example>
/// <code>
/// var inserted = await provider.InsertAsync(dbContext, batch, cancellationToken);
/// </code>
/// </example>
public sealed partial class SqlServerEntityBulkInsertProvider : IEntityBulkInsertProvider
{
    /// <summary>
    /// The Entity Framework provider name supported by this strategy.
    /// </summary>
    public const string EntityFrameworkProviderName = "Microsoft.EntityFrameworkCore.SqlServer";

    private readonly ILogger<SqlServerEntityBulkInsertProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerEntityBulkInsertProvider"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <example>
    /// <code>
    /// var provider = new SqlServerEntityBulkInsertProvider(loggerFactory);
    /// </code>
    /// </example>
    public SqlServerEntityBulkInsertProvider(ILoggerFactory loggerFactory)
    {
        this.logger = loggerFactory?.CreateLogger<SqlServerEntityBulkInsertProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<SqlServerEntityBulkInsertProvider>();
    }

    /// <inheritdoc />
    public string ProviderName => EntityFrameworkProviderName;

    /// <inheritdoc />
    public async Task<long> InsertAsync<TEntity>(
        DbContext context,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(batch, nameof(batch));

        var configuredOptions = GetConfiguredOptions(batch.Options);
        var table = CreateDataTable(batch);
        var connection = GetSqlConnection<TEntity>(context);
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken).AnyContext();
        }

        try
        {
            using var bulkCopy = CreateBulkCopy(context, connection, configuredOptions);
            bulkCopy.DestinationTableName = GetDelimitedTableName(batch.Schema, batch.TableName);
            bulkCopy.BatchSize = batch.Options.BatchSize;
            bulkCopy.BulkCopyTimeout = batch.Options.CommandTimeout;

            foreach (DataColumn column in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            TypedLogger.LogBulkInsert(
                this.logger,
                Infrastructure.EntityFramework.Constants.LogKey,
                typeof(TEntity).Name,
                batch.Entities.Count,
                bulkCopy.DestinationTableName);
            await bulkCopy.WriteToServerAsync(table, cancellationToken).AnyContext();

            return batch.Entities.Count;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync().AnyContext();
            }
        }
    }

    private static SqlBulkCopyOptions GetConfiguredOptions(EntityBulkInsertOptions options)
    {
        if (options is not SqlServerEntityBulkInsertOptions sqlServerOptions)
        {
            return options.KeepGeneratedIdentityValues
                ? SqlBulkCopyOptions.KeepIdentity
                : SqlBulkCopyOptions.Default;
        }

        var forbiddenOptions = sqlServerOptions.SqlBulkCopyOptions &
            (SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.UseInternalTransaction);
        if (forbiddenOptions != SqlBulkCopyOptions.Default)
        {
            throw new ArgumentException(
                $"{nameof(SqlServerEntityBulkInsertOptions.SqlBulkCopyOptions)} must not include " +
                $"{SqlBulkCopyOptions.KeepIdentity} or {SqlBulkCopyOptions.UseInternalTransaction}. " +
                $"Use {nameof(EntityBulkInsertOptions.KeepGeneratedIdentityValues)} to preserve generated identity values; " +
                "the provider manages its internal transaction from the active EF transaction.",
                nameof(sqlServerOptions));
        }

        return options.KeepGeneratedIdentityValues
            ? sqlServerOptions.SqlBulkCopyOptions | SqlBulkCopyOptions.KeepIdentity
            : sqlServerOptions.SqlBulkCopyOptions;
    }

    private static SqlBulkCopy CreateBulkCopy(
        DbContext context,
        SqlConnection connection,
        SqlBulkCopyOptions configuredOptions)
    {
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction is SqlTransaction sqlTransaction)
        {
            return new SqlBulkCopy(
                connection,
                configuredOptions & ~SqlBulkCopyOptions.UseInternalTransaction,
                sqlTransaction);
        }

        return new SqlBulkCopy(
            connection,
            configuredOptions | SqlBulkCopyOptions.UseInternalTransaction,
            null);
    }

    private static SqlConnection GetSqlConnection<TEntity>(DbContext context)
        where TEntity : class
    {
        return context.Database.GetDbConnection() as SqlConnection ??
            throw new InvalidOperationException(
                $"Bulk insert for '{typeof(TEntity).Name}' requires a Microsoft.Data.SqlClient.SqlConnection.");
    }

    private static DataTable CreateDataTable<TEntity>(EntityBulkInsertBatch<TEntity> batch)
        where TEntity : class
    {
        var table = new DataTable(batch.TableName);
        foreach (var column in batch.Columns)
        {
            table.Columns.Add(column.ColumnName, Nullable.GetUnderlyingType(column.ProviderClrType) ?? column.ProviderClrType);
        }

        foreach (var entity in batch.Entities)
        {
            var row = table.NewRow();
            foreach (var column in batch.Columns)
            {
                row[column.ColumnName] = column.GetProviderValue(entity) ?? DBNull.Value;
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private static string GetDelimitedTableName(string schema, string tableName)
    {
        return string.IsNullOrWhiteSpace(schema)
            ? Delimit(tableName)
            : $"{Delimit(schema)}.{Delimit(tableName)}";
    }

    private static string Delimit(string identifier)
    {
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static partial class TypedLogger
    {
        [LoggerMessage(1, LogLevel.Debug, "[{LogKey}] bulk inserting {EntityCount} {EntityType} entities into {TableName}")]
        public static partial void LogBulkInsert(ILogger logger, string logKey, string entityType, int entityCount, string tableName);
    }
}
