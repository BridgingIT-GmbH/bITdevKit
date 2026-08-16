// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using NullLogger = NullLogger;

/// <summary>
/// Represents db context extensions.
/// </summary>
public static partial class DbContextExtensions
{
    /// <summary>
    ///     Only persists the entities of the specified type <see cref="TEntity" />
    /// </summary>
    public static async Task<int> SaveChangesAsync<TEntity>(
        this DbContext context,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return await context.SaveChangesAsync<TEntity>(NullLogger.Instance, cancellationToken);
    }

    /// <summary>
    ///     Only persists the entities of the specified type <see cref="TEntity" />
    /// </summary>
    public static async Task<int> SaveChangesAsync<TEntity>(
        this DbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var states = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not TEntity && e.State is not EntityState.Unchanged)
            .GroupBy(x => x.State).ToList();

        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.Entity is not TEntity))
        {
            entry.State = EntityState.Unchanged;
        }

        if (logger is not null)
        {
            foreach (var entry in context.ChangeTracker.Entries().Where(e => e.Entity is TEntity))
            {
                TypedLogger.LogEntityState(logger, Constants.LogKey, entry.Entity.GetType().Name, entry.IsKeySet, entry.State);
            }
        }

        var count = await context.SaveChangesAsync(cancellationToken);

        foreach (var state in states) // reset the states on the 'unchanged' entities
        {
            foreach (var entry in state)
            {
                entry.State = state.Key;
            }
        }

        return count;
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the entity state operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        /// <param name="entityKeySet">The entity key set used by the operation.</param>
        /// <param name="entityEntryState">The entity entry state used by the operation.</param>
        [LoggerMessage(2, LogLevel.Trace, "[{LogKey}] dbcontext entity state: {EntityType} (keySet={EntityKeySet}) -> {EntityEntryState}")]
        public static partial void LogEntityState(ILogger logger, string logKey, string entityType, bool entityKeySet, EntityState entityEntryState);
    }
}
