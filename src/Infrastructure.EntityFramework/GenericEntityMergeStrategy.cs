// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

public static class GenericEntityMergeStrategy
{
    public class Options
    {
        public HashSet<string> IgnoredNavigations { get; set; } = [];

        public int? MaxDepth { get; set; } = null; // null = unlimited

        public ILogger Logger { get; set; } = null; // optional ILogger
    }

    public static async Task<TEntity> MergeAsync<TEntity>(
        DbContext context,
        TEntity incoming,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await MergeInternalAsync(context, incoming, new Options(), [], 0, cancellationToken);
    }

    public static async Task<TEntity> MergeAsync<TEntity>(
        DbContext context,
        TEntity incoming,
        Options options,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await MergeInternalAsync(context, incoming, options, [], 0, cancellationToken);
    }

    private static async Task<TEntity> MergeInternalAsync<TEntity>(
        DbContext context,
        TEntity incoming,
        Options options,
        HashSet<string> visited,
        int depth,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));

        // Build query with includes for all navigations (except ignored)
        IQueryable<TEntity> query = context.Set<TEntity>();
        foreach (var nav in entityType.GetNavigations())
        {
            if (options.IgnoredNavigations.Contains(nav.Name))
            {
                continue;
            }

            query = query.Include(nav.Name);
        }

        var existing = await query.FirstOrDefaultAsync(e => e.Id.Equals(incoming.Id), cancellationToken);
        if (existing == null)
        {
            options.Logger?.LogDebug("[EntityMergeStrategy] Entity {EntityType}({EntityId}) not found, fallback to insert.",
                typeof(TEntity).Name, incoming.Id);
            return incoming; // fallback to insert
        }

        // Update scalar properties
        context.Entry(existing).CurrentValues.SetValues(incoming);
        options.Logger?.LogDebug("[EntityMergeStrategy] Updated scalars for {EntityType}({EntityId})",
            typeof(TEntity).Name, incoming.Id);

        // Mark this entity as visited
        var key = $"{typeof(TEntity).Name}:{existing.Id}";
        if (!visited.Add(key))
        {
            options.Logger?.LogDebug("[EntityMergeStrategy] Skipped already visited {EntityKey}", key);
            return existing; // already processed, avoid recursion
        }

        // Stop if max depth reached
        if (options.MaxDepth.HasValue && depth >= options.MaxDepth.Value)
        {
            options.Logger?.LogDebug("[EntityMergeStrategy] Max depth {MaxDepth} reached at {EntityKey}",
                options.MaxDepth, key);
            return existing;
        }

        // Reconcile references
        MergeChildReferences(context, existing, incoming, options, visited, depth + 1);

        // Reconcile collections
        MergeChildCollections(context, existing, incoming, options, visited, depth + 1);

        return existing;
    }

    private static void MergeChildReferences(DbContext context, object existing, object incoming, Options options, HashSet<string> visited, int depth)
    {
        var entityType = context.Model.FindEntityType(existing.GetType());
        foreach (var nav in entityType.GetNavigations().Where(n => !n.IsCollection))
        {
            if (options.IgnoredNavigations.Contains(nav.Name))
            {
                continue;
            }

            if (nav.TargetEntityType.IsOwned())
            {
                continue; // skip owned types
            }

            var incomingRef = existing.GetType().GetProperty(nav.Name)?.GetValue(incoming);
            var existingRef = existing.GetType().GetProperty(nav.Name)?.GetValue(existing);

            if (incomingRef == null)
            {
                if (nav.ForeignKey.Properties.All(p => p.IsNullable))
                {
                    existing.GetType().GetProperty(nav.Name)?.SetValue(existing, null);
                    options.Logger?.LogDebug("[EntityMergeStrategy] Nullified {Nav} on {EntityType}", nav.Name, entityType.Name);
                }
                continue;
            }

            var keyProps = nav.TargetEntityType.FindPrimaryKey().Properties;
            var incomingKey = GetKeyValues(context, incomingRef, keyProps);

            if (existingRef == null)
            {
                existing.GetType().GetProperty(nav.Name)?.SetValue(existing, incomingRef);
                CopyForeignKeyValues(context, existing, incomingRef, nav.ForeignKey);
                options.Logger?.LogDebug("[EntityMergeStrategy] Attached new {Nav} to {EntityType}", nav.Name, entityType.Name);
            }
            else
            {
                var existingKey = GetKeyValues(context, existingRef, keyProps);
                if (!KeysEqual(existingKey, incomingKey))
                {
                    existing.GetType().GetProperty(nav.Name)?.SetValue(existing, incomingRef);
                    CopyForeignKeyValues(context, existing, incomingRef, nav.ForeignKey);
                    options.Logger?.LogDebug("[EntityMergeStrategy] Replaced {Nav} on {EntityType}", nav.Name, entityType.Name);
                }
                else
                {
                    context.Entry(existingRef).CurrentValues.SetValues(incomingRef);
                    options.Logger?.LogDebug("[EntityMergeStrategy] Updated {Nav} on {EntityType}", nav.Name, entityType.Name);

                    // Recurse deeper only if not visited
                    var childKey = $"{existingRef.GetType().Name}:{string.Join(",", existingKey)}";
                    if (visited.Add(childKey))
                    {
                        MergeChildCollections(context, existingRef, incomingRef, options, visited, depth);
                        MergeChildReferences(context, existingRef, incomingRef, options, visited, depth);
                    }
                }
            }
        }
    }

    private static void MergeChildCollections(DbContext context, object existing, object incoming, Options options, HashSet<string> visited, int depth)
    {
        var entityType = context.Model.FindEntityType(existing.GetType());
        foreach (var nav in entityType.GetNavigations().Where(n => n.IsCollection))
        {
            if (options.IgnoredNavigations.Contains(nav.Name))
            {
                continue;
            }

            var clrCollection = context.Entry(existing).Collection(nav.Name).CurrentValue;
            var incomingCollection = (IEnumerable)existing.GetType().GetProperty(nav.Name)?.GetValue(incoming);

            if (clrCollection == null || incomingCollection == null)
            {
                continue;
            }

            var existingCollection = ((IEnumerable)clrCollection).Cast<object>().ToList();
            var incomingList = incomingCollection.Cast<object>().ToList();

            var keyProps = nav.TargetEntityType.FindPrimaryKey().Properties;

            // Remove missing
            foreach (var existingChild in existingCollection.ToList())
            {
                var existingKey = GetKeyValues(context, existingChild, keyProps);
                if (!incomingList.Any(ic => KeysEqual(existingKey, GetKeyValues(context, ic, keyProps))))
                {
                    context.Remove(existingChild);
                    clrCollection.GetType().GetMethod("Remove")?.Invoke(clrCollection, [existingChild]);
                    options.Logger?.LogDebug("[EntityMergeStrategy] Removed {Nav} child {ChildKey}", nav.Name, string.Join(",", existingKey));
                }
            }

            // Add or update
            foreach (var incomingChild in incomingList)
            {
                var incomingKey = GetKeyValues(context, incomingChild, keyProps);
                var existingChild = existingCollection
                    .FirstOrDefault(ec => KeysEqual(GetKeyValues(context, ec, keyProps), incomingKey));

                if (existingChild == null)
                {
                    clrCollection.GetType().GetMethod("Add")?.Invoke(clrCollection, [incomingChild]);
                    options.Logger?.LogDebug("[EntityMergeStrategy] Added {Nav} child {ChildKey}", nav.Name, string.Join(",", incomingKey));
                }
                else
                {
                    context.Entry(existingChild).CurrentValues.SetValues(incomingChild);
                    options.Logger?.LogDebug("[EntityMergeStrategy] Updated {Nav} child {ChildKey}", nav.Name, string.Join(",", incomingKey));

                    // 🔁 Recurse deeper only if not visited
                    var childKey = $"{existingChild.GetType().Name}:{string.Join(",", incomingKey)}";
                    if (visited.Add(childKey))
                    {
                        MergeChildCollections(context, existingChild, incomingChild, options, visited, depth);
                        MergeChildReferences(context, existingChild, incomingChild, options, visited, depth);
                    }
                }
            }
        }
    }

    private static object[] GetKeyValues(DbContext context, object entity, IReadOnlyList<IProperty> keyProps)
    {
        var entry = context.Entry(entity);
        return keyProps.Select(p => entry.Property(p.Name).CurrentValue).ToArray();
    }

    private static bool KeysEqual(object[] a, object[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!Equals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static void CopyForeignKeyValues(DbContext context, object owner, object reference, IForeignKey fk)
    {
        var ownerEntry = context.Entry(owner); // !
        var refEntry = context.Entry(reference);

        foreach (var (prop, principalProp) in fk.Properties.Zip(fk.PrincipalKey.Properties))
        {
            ownerEntry.Property(prop.Name).CurrentValue = refEntry.Property(principalProp.Name).CurrentValue;
        }
    }
}