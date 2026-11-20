// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;

internal static class DocumentDimensionMutations
{
    public static async Task<Result> ApplyAsync<TContext>(TContext context, StorageDocument owner, DocumentDimensions replacement,
        DocumentDimensionNameResolver<TContext> resolver, DocumentDimensionMutationSession session, CancellationToken token) where TContext : DbContext
    {
        if (replacement is null) return Result.Success();
        var decoded = DocumentDimensionValues.Deserialize(owner.DimensionsJson);
        if (decoded.IsFailure) return Result.Failure().WithErrors(decoded.Errors).WithMessages(decoded.Messages);
        var previous = decoded.Value;
        if (previous.Count == replacement.Count && previous.All(item => replacement.TryGetValue(item.Key, out var value) && value == item.Value))
        {
            owner.DimensionsJson = DocumentDimensionValues.Serialize(replacement);
            return Result.Success();
        }

        var oldIds = await resolver.ResolveExistingAsync(context, owner.Type, previous.Keys, session, token).ConfigureAwait(false);
        if (oldIds.IsFailure) return Result.Failure().WithErrors(oldIds.Errors).WithMessages(oldIds.Messages);
        if (oldIds.Value.Count != previous.Count) return Result.Failure(new DocumentStoreProviderError("Dimension snapshot refers to missing dictionary identities."));
        var newIds = await resolver.ResolveForWriteAsync(context, owner.Type, replacement.Keys, session, token).ConfigureAwait(false);
        if (newIds.IsFailure) return Result.Failure().WithErrors(newIds.Errors).WithMessages(newIds.Messages);
        foreach (var item in previous)
        {
            if (replacement.ContainsKey(item.Key)) continue;
            var nameId = oldIds.Value[DocumentDimensionValues.CanonicalizeName(item.Key)];
            var key = (owner.Id, nameId);
            if (!session.Rows.TryGetValue(key, out var row))
            {
                row = new() { DocumentId = owner.Id, NameId = nameId };
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
            var nameId = newIds.Value[DocumentDimensionValues.CanonicalizeName(item.Key)];
            var key = (owner.Id, nameId);
            var existed = previous.ContainsKey(item.Key);
            if (!session.Rows.TryGetValue(key, out var row))
            {
                row = new() { DocumentId = owner.Id, NameId = nameId };
                session.Rows[key] = row;
            }

            DocumentDimensionValueEncoding.Encode(item.Value, row);
            var entry = context.Entry(row);
            if (entry.State != EntityState.Added) entry.State = existed || entry.State == EntityState.Deleted ? EntityState.Modified : EntityState.Added;
        }

        owner.DimensionsJson = DocumentDimensionValues.Serialize(replacement);
        return Result.Success();
    }
}
