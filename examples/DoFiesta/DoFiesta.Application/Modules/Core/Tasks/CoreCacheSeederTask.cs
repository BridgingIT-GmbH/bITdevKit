// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>Populates the in-memory demonstration collection with fictional content for dashboard inspection.</summary>
/// <param name="cache">The application's declared collections.</param>
/// <param name="catalog">The finalized collection enablement and policies.</param>
/// <param name="logger">The structured startup logger.</param>
/// <example><code>services.AddStartupTasks().WithTask&lt;CoreCacheSeederTask&gt;();</code></example>
public sealed class CoreCacheSeederTask(IApplicationCache cache, CacheCatalog catalog, ILogger<CoreCacheSeederTask> logger) : IStartupTask
{
    /// <summary>Identifies the typed string collection populated at startup.</summary>
    /// <example><code>var collection = cache.GetCollection&lt;string&gt;(CoreCacheSeederTask.CollectionName);</code></example>
    public const string CollectionName = "memory-demo";

    private const int EntriesPerSize = 8;
    private static readonly int[] PayloadSizes = [1024, 8192, 32768, 131072, 524288, 1048576];
    private static readonly string[] Categories = ["catalog", "report", "document"];

    /// <summary>Writes 48 deterministic entries, replacing the same keys when the task is run again.</summary>
    /// <param name="cancellationToken">Cancels the remaining seed writes.</param>
    /// <returns>A task that completes after all entries have been confirmed stored.</returns>
    /// <exception cref="InvalidOperationException">A seed write failed or was rejected.</exception>
    /// <example><code>await task.ExecuteAsync(cancellationToken);</code></example>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!catalog.Options.Enabled || !catalog.Collections[CollectionName].Options.Enabled)
        {
            logger.LogInformation("[{LogKey}] demonstration cache seeding skipped (collection={CacheCollection}, enabled=false)", "IFR", CollectionName);
            return;
        }

        var collection = cache.GetCollection<string>(CollectionName);
        var index = 0;
        foreach (var size in PayloadSizes)
        {
            for (var entry = 0; entry < EntriesPerSize; entry++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // One long key per size bucket also exercises the dashboard's suffix display.
                var prefix = entry == EntriesPerSize - 1
                    ? "demo:memory:startup:fictional-content-for-dashboard-key-inspection:" + new string('x', 96)
                    : "demo:memory";
                var item = new CacheItem($"{prefix}:{size}:{entry:00}")
                    .WithScalarDimension("category", Categories[index % Categories.Length])
                    .WithScalarDimension("payloadBytes", size)
                    .WithScalarDimension("active", index % 2 == 0)
                    .WithDimension(CacheDimension.Set("tags", CacheDimensionKind.String, "demo", $"size-{size}"))
                    .WithScalarDimension("sequence", index + 1);
                // ASCII content keeps the logical payload size predictable without business data.
                var result = await collection.SetAsync(item, new string((char)('a' + index % 26), size),
                    cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.IsFailure || result.Value.StorageOutcome != CacheStorageOutcome.Stored)
                {
                    throw new InvalidOperationException($"The demonstration cache entry '{item.Key}' could not be stored.");
                }

                index++;
            }
        }

        logger.LogInformation("[{LogKey}] seed demonstration cache (collection={CacheCollection}, entries={CacheEntries}, payloadBytes={PayloadBytes})",
            "IFR", CollectionName, index, PayloadSizes.Sum(size => (long)size * EntriesPerSize));
    }
}