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

    private static Task<List<StorageBlob>> ReadExpiredCandidatesAsync(TContext context, BlobRetentionSweepRequest request,
        (DateTimeOffset ExpiresAt, Guid Id)? cursor, CancellationToken cancellationToken)
    {
        if (!IsSqlite(context))
        {
            var due = context.StorageBlobs.Where(row => row.ExpiresAt != null && row.ExpiresAt <= request.ExpiresOnOrBefore);
            if (cursor is { } anchor)
            {
                due = due.Where(row => row.ExpiresAt > anchor.ExpiresAt ||
                    (row.ExpiresAt == anchor.ExpiresAt && row.Id.CompareTo(anchor.Id) > 0));
            }

            return due
                .OrderBy(row => row.ExpiresAt).ThenBy(row => row.Id).Take(Math.Max(1, request.BatchSize))
                .ToListAsync(cancellationToken);
        }

        // SQLite cannot translate LINQ DateTimeOffset ordering. Provider writes normalize expiry to UTC,
        // whose native TEXT representation sorts chronologically without applying a function to the index column.
        var entity = context.Model.FindEntityType(typeof(StorageBlob));
        var store = StoreObjectIdentifier.Table(entity.GetTableName(), entity.GetSchema());
        var sql = context.GetService<ISqlGenerationHelper>();
        var table = sql.DelimitIdentifier(store.Name, store.Schema);
        var expirationProperty = entity.FindProperty(nameof(StorageBlob.ExpiresAt));
        var expiration = sql.DelimitIdentifier(expirationProperty.GetColumnName(store));
        var idProperty = entity.FindProperty(nameof(StorageBlob.Id));
        var id = sql.DelimitIdentifier(idProperty.GetColumnName(store));
        using var command = context.Database.GetDbConnection().CreateCommand();
        var cutoff = expirationProperty.GetRelationalTypeMapping().CreateParameter(command, "@cutoff", request.ExpiresOnOrBefore.ToUniversalTime());
        // Only delimited model identifiers enter the SQL text. EF applies query filters before the outer Take.
        var query = $"SELECT * FROM {table} WHERE {expiration} IS NOT NULL AND {expiration} <= {{0}}";
        object[] parameters = [cutoff];
        if (cursor is { } sqliteAnchor)
        {
            query += $" AND ({expiration} > {{1}} OR ({expiration} = {{1}} AND {id} > {{2}}))";
            parameters = [cutoff,
                expirationProperty.GetRelationalTypeMapping().CreateParameter(command, "@afterExpiry", sqliteAnchor.ExpiresAt.ToUniversalTime()),
                idProperty.GetRelationalTypeMapping().CreateParameter(command, "@afterId", sqliteAnchor.Id)];
        }

        query += $" ORDER BY {expiration}, {id}";
        return context.StorageBlobs.FromSqlRaw(query, parameters).Take(Math.Max(1, request.BatchSize))
            .ToListAsync(cancellationToken);
    }
}
