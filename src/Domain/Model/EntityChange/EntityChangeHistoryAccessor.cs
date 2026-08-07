// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides access to in-memory entity change sets awaiting persistence by outer layers.
/// </summary>
/// <example>
/// <code>
/// var pending = EntityChangeHistoryAccessor.ConsumePendingChangeSets(customer);
/// foreach (var changeSet in pending)
/// {
///     // Map the property changes to an infrastructure history table.
/// }
/// </code>
/// </example>
public static class EntityChangeHistoryAccessor
{
    private static readonly ConditionalWeakTable<IEntity, PendingChangeSetBuffer> PendingChangeSets = new();

    /// <summary>
    /// Gets the pending change sets for the specified entity without removing them.
    /// </summary>
    /// <param name="entity">The entity whose pending change sets should be inspected.</param>
    /// <returns>The pending change sets in creation order.</returns>
    public static IReadOnlyList<EntityChangeSet> GetPendingChangeSets(IEntity entity)
    {
        if (entity is null || !PendingChangeSets.TryGetValue(entity, out var buffer))
        {
            return [];
        }

        return buffer.Snapshot();
    }

    /// <summary>
    /// Gets and removes the pending change sets for the specified entity.
    /// </summary>
    /// <param name="entity">The entity whose pending change sets should be consumed.</param>
    /// <returns>The consumed change sets in creation order.</returns>
    public static IReadOnlyList<EntityChangeSet> ConsumePendingChangeSets(IEntity entity)
    {
        if (entity is null || !PendingChangeSets.TryGetValue(entity, out var buffer))
        {
            return [];
        }

        var result = buffer.Snapshot();
        buffer.Clear();
        return result;
    }

    internal static void AddPendingChangeSet(IEntity entity, EntityChangeSet changeSet)
    {
        if (entity is null || changeSet is null || changeSet.PropertyChanges.Count == 0)
        {
            return;
        }

        PendingChangeSets.GetOrCreateValue(entity).Add(changeSet);
    }

    private sealed class PendingChangeSetBuffer
    {
        private readonly List<EntityChangeSet> changeSets = [];

        public void Add(EntityChangeSet changeSet) => this.changeSets.Add(changeSet);

        public IReadOnlyList<EntityChangeSet> Snapshot() => this.changeSets.ToArray();

        public void Clear() => this.changeSets.Clear();
    }
}
