// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Applies cache shutdown and safe diagnostic policy to shared tracking of actual owned tasks.</summary>
/// <remarks>Every tracked task must already own its admission until actual completion. Tracking grants no capacity.</remarks>
/// <example><code>var occupied = tasks.Count;</code></example>
public sealed class CacheRuntimeTasks : IAsyncDisposable
{
    private readonly OwnedTaskTracker tracker;

    /// <summary>Tracks already admitted work with the catalog's stop budget and optional host lifetime signal.</summary>
    /// <example><code>var tasks = new CacheRuntimeTasks(catalog, logger, lifetime);</code></example>
    public CacheRuntimeTasks(CacheCatalog catalog, ILogger<CacheRuntimeTasks> logger = null, IHostApplicationLifetime lifetime = null)
    {
        var actualLogger = logger ?? NullLogger<CacheRuntimeTasks>.Instance;
        this.tracker = new(catalog.Options.BackgroundServices.StopTimeout,
            (exception, kind) => actualLogger.LogWarning("[{LogKey}] cache owned work failed (Kind={Kind}, ErrorType={ErrorType})",
                "ApplicationCaching", kind, exception.GetType().Name),
            count => actualLogger.LogWarning("[{LogKey}] cache work remains occupied after the stop budget (Count={Count})",
                "ApplicationCaching", count), lifetime?.ApplicationStopping ?? default);
    }

    /// <summary>Gets host-stop cancellation without treating it as native completion.</summary>
    public CancellationToken Stopping => this.tracker.Stopping;

    /// <summary>Requests cancellation while retaining all occupied accounts until actual completion.</summary>
    public void BeginStop() => this.tracker.BeginStop();

    /// <summary>Gets actually unfinished owned work, including work which ignores cancellation.</summary>
    public int Count => this.tracker.Count;

    /// <summary>Tracks admitted actual work and captures its live caller resource lease until completion.</summary>
    /// <example><code>tasks.Track(actualNativeWork, "provider-tail");</code></example>
    public void Track(Task task, string kind) => this.tracker.Track(task, kind);

    /// <summary>Waits only the cache's configured stop budget; timed-out work remains owned and tracked.</summary>
    public ValueTask DisposeAsync() => this.tracker.DisposeAsync();
}