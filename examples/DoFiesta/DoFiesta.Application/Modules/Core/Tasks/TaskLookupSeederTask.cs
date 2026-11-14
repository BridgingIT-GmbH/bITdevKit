// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>Warms the task-title lookup collection from its static reference source at startup.</summary>
/// <param name="lookups">The catalog using the same cache-aside path as task creation.</param>
/// <param name="catalog">The finalized collection enablement and policies.</param>
/// <param name="logger">The structured startup logger.</param>
/// <example><code>services.AddStartupTasks().WithTask&lt;TaskLookupSeederTask&gt;();</code></example>
public sealed class TaskLookupSeederTask(TaskLookupCatalog lookups, CacheCatalog catalog, ILogger<TaskLookupSeederTask> logger) : IStartupTask
{
    /// <summary>Loads every suggestion into the enabled collection without depending on a business database.</summary>
    /// <param name="cancellationToken">Cancels the remaining warm-up work.</param>
    /// <returns>A task that completes when every lookup has been read or loaded.</returns>
    /// <exception cref="InvalidOperationException">The lookup catalog returned an explicit failure.</exception>
    /// <example><code>await task.ExecuteAsync(cancellationToken);</code></example>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!catalog.Options.Enabled || !catalog.Collections[TaskLookupCatalog.CollectionName].Options.Enabled)
        {
            logger.LogInformation("[{LogKey}] task lookup cache warm-up skipped (collection={CacheCollection}, enabled=false)", "IFR", TaskLookupCatalog.CollectionName);
            return;
        }

        var result = await lookups.GetAllAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsFailure)
        {
            throw new InvalidOperationException("The task-title lookup cache could not be populated.");
        }

        logger.LogInformation("[{LogKey}] warm task lookup cache (collection={CacheCollection}, entries={CacheEntries})",
            "IFR", TaskLookupCatalog.CollectionName, result.Value.Count);
    }
}