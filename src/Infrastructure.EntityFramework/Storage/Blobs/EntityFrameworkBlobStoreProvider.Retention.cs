// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

public sealed partial class EntityFrameworkBlobStoreProvider<TContext> where TContext : DbContext, IBlobStoreContext
{
    private static bool IsExpired(StorageBlob blob, DateTimeOffset cutoff) => blob.ExpiresAt <= cutoff;

    private static IQueryable<StorageBlob> QueryLiveBlobs(TContext context, DateTimeOffset cutoff)
    {
        if (!IsSqlite(context))
        {
            return context.StorageBlobs.Where(row => row.ExpiresAt == null || row.ExpiresAt > cutoff);
        }

        // Match the sweep's UTC TEXT representation for SQLite DateTimeOffset values.
        var entity = context.Model.FindEntityType(typeof(StorageBlob));
        var store = StoreObjectIdentifier.Table(entity.GetTableName(), entity.GetSchema());
        var sql = context.GetService<ISqlGenerationHelper>();
        var table = sql.DelimitIdentifier(store.Name, store.Schema);
        var property = entity.FindProperty(nameof(StorageBlob.ExpiresAt));
        var expiration = sql.DelimitIdentifier(property.GetColumnName(store));
        using var command = context.Database.GetDbConnection().CreateCommand();
        var parameter = property.GetRelationalTypeMapping().CreateParameter(command, "@cutoff", cutoff.ToUniversalTime());
        // Only delimited model identifiers enter SQL text. The cutoff is a database parameter.
        var query = $"SELECT * FROM {table} WHERE {expiration} IS NULL OR {expiration} > {{0}}";
        return context.StorageBlobs.FromSqlRaw(query, parameter);
    }

    private static async Task<List<StorageBlob>> ReadExpiredCandidatesAsync(TContext context, BlobRetentionSweepRequest request, CancellationToken cancellationToken)
    {
        if (!IsSqlite(context))
            return await context.StorageBlobs.Where(row => row.ExpiresAt != null && row.ExpiresAt <= request.ExpiresOnOrBefore)
                .OrderBy(row => row.ExpiresAt).ThenBy(row => row.Id).Take(Math.Max(1, request.BatchSize))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        // SQLite cannot translate LINQ DateTimeOffset ordering. Provider writes normalize expiry to UTC,
        // whose native TEXT representation sorts chronologically without applying a function to the index column.
        var entity = context.Model.FindEntityType(typeof(StorageBlob));
        var store = StoreObjectIdentifier.Table(entity.GetTableName(), entity.GetSchema());
        var sql = context.GetService<ISqlGenerationHelper>();
        var table = sql.DelimitIdentifier(store.Name, store.Schema);
        var expirationProperty = entity.FindProperty(nameof(StorageBlob.ExpiresAt));
        var expiration = sql.DelimitIdentifier(expirationProperty.GetColumnName(store));
        var id = sql.DelimitIdentifier(entity.FindProperty(nameof(StorageBlob.Id)).GetColumnName(store));
        using var command = context.Database.GetDbConnection().CreateCommand();
        var cutoff = expirationProperty.GetRelationalTypeMapping().CreateParameter(command, "@cutoff", request.ExpiresOnOrBefore.ToUniversalTime());
        // Only delimited model identifiers enter the SQL text. EF applies query filters before the outer Take.
        var query = $"SELECT * FROM {table} WHERE {expiration} IS NOT NULL AND {expiration} <= {{0}} ORDER BY {expiration}, {id}";
        return await context.StorageBlobs.FromSqlRaw(query, cutoff).Take(Math.Max(1, request.BatchSize))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
