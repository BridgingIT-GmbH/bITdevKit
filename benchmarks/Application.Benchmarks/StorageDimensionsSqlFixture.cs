// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data;
using Infrastructure.EntityFramework;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>Bulk-copies immutable SQL test fixtures after EF key generation, relationship fix-up and conversion.</summary>
/// <remarks>Use only for owned benchmark setup. Registration and measured calls still use the real providers.</remarks>
/// <example><code>await StorageDimensionsSqlFixture.WriteTrackedAsync(context);</code></example>
public static class StorageDimensionsSqlFixture
{
    /// <summary>Copies added owner, chunk and Dimension rows in foreign-key order under one transaction.</summary>
    /// <param name="context">A SQL Server context tracking only new fixture rows.</param>
    /// <example><code>await StorageDimensionsSqlFixture.WriteTrackedAsync(context); context.ChangeTracker.Clear();</code></example>
    public static async Task WriteTrackedAsync(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var entries = context.ChangeTracker.Entries().ToArray();
        Type[] order = [typeof(StorageBlob), typeof(StorageDocument), typeof(StorageBlobChunk),
            typeof(StorageBlobDimension), typeof(StorageDocumentDimension)];
        if (entries.Any(entry => entry.State != EntityState.Added || !order.Contains(entry.Metadata.ClrType)))
        {
            throw new InvalidOperationException("Bulk fixture setup requires only new owner, chunk and value rows.");
        }

        var connection = (SqlConnection)context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        foreach (var type in order)
        {
            var rows = entries.Where(entry => entry.Metadata.ClrType == type).ToArray();
            if (rows.Length == 0)
            {
                continue;
            }

            var entity = rows[0].Metadata;
            var table = entity.GetTableName();
            var schema = entity.GetSchema() ?? "dbo";
            var identifier = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var properties = entity.GetProperties().ToArray();
            var converters = properties.Select(property => property.GetTypeMapping().Converter).ToArray();
            using var data = new DataTable();
            for (var index = 0; index < properties.Length; index++)
            {
                var providerType = converters[index]?.ProviderClrType ?? properties[index].ClrType;
                data.Columns.Add(properties[index].GetColumnName(identifier), Nullable.GetUnderlyingType(providerType) ?? providerType);
            }

            foreach (var row in rows)
            {
                data.Rows.Add(properties.Select((property, index) =>
                {
                    var value = row.Property(property.Name).CurrentValue;
                    return (converters[index] is { } converter ? converter.ConvertToProvider(value) : value) ?? DBNull.Value;
                }).ToArray());
            }

            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls, transaction)
            {
                DestinationTableName = $"[{schema}].[{table}]",
                BulkCopyTimeout = 120,
                BatchSize = data.Rows.Count
            };
            foreach (DataColumn column in data.Columns)
            {
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulk.WriteToServerAsync(data);
        }

        await transaction.CommitAsync();
    }
}