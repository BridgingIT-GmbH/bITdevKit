// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;

internal static class BlobDimensionMutations
{
    public static async Task<Result> ApplyAsync<TContext>(TContext context, StorageBlob owner, BlobDimensions replacement,
        BlobDimensionNameResolver<TContext> resolver, BlobDimensionMutationSession session, CancellationToken token) where TContext : DbContext
    {
        if (replacement is null) return Result.Success();
        var decoded = BlobDimensionValues.Deserialize(owner.DimensionsJson);
        if (decoded.IsFailure) return Result.Failure().WithErrors(decoded.Errors).WithMessages(decoded.Messages);
        var previous = decoded.Value;
        if (previous.Count == replacement.Count && previous.All(item => replacement.TryGetValue(item.Key, out var value) && value == item.Value))
        {
            owner.DimensionsJson = BlobDimensionValues.Serialize(replacement);
            return Result.Success();
        }

        var oldIds = await resolver.ResolveExistingAsync(context, owner.Container, previous.Keys, session, token).ConfigureAwait(false);
        if (oldIds.IsFailure) return Result.Failure().WithErrors(oldIds.Errors).WithMessages(oldIds.Messages);
        if (oldIds.Value.Count != previous.Count) return Result.Failure(new BlobStoreProviderError("Dimension snapshot refers to missing dictionary identities."));
        var newIds = await resolver.ResolveForWriteAsync(context, owner.Container, replacement.Keys, session, token).ConfigureAwait(false);
        if (newIds.IsFailure) return Result.Failure().WithErrors(newIds.Errors).WithMessages(newIds.Messages);
        foreach (var item in previous)
        {
            if (replacement.ContainsKey(item.Key)) continue;
            var nameId = oldIds.Value[BlobDimensionValues.CanonicalizeName(item.Key)];
            var key = (owner.Id, nameId);
            if (!session.Rows.TryGetValue(key, out var row))
            {
                row = new() { BlobId = owner.Id, NameId = nameId };
                session.Rows[key] = row;
            }

            var entry = context.Entry(row);
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
                session.Rows.Remove(key);
            }
            else entry.State = EntityState.Deleted;
        }

        foreach (var item in replacement)
        {
            if (previous.TryGetValue(item.Key, out var oldValue) && oldValue == item.Value) continue;
            var nameId = newIds.Value[BlobDimensionValues.CanonicalizeName(item.Key)];
            var key = (owner.Id, nameId);
            var existed = previous.ContainsKey(item.Key);
            if (!session.Rows.TryGetValue(key, out var row))
            {
                row = new() { BlobId = owner.Id, NameId = nameId };
                session.Rows[key] = row;
            }

            BlobDimensionValueEncoding.Encode(item.Value, row);
            var entry = context.Entry(row);
            if (entry.State != EntityState.Added) entry.State = existed || entry.State == EntityState.Deleted ? EntityState.Modified : EntityState.Added;
        }

        owner.DimensionsJson = BlobDimensionValues.Serialize(replacement);
        return Result.Success();
    }
}
