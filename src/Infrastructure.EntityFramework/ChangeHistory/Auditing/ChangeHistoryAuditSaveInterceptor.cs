// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>Observes existing source rows at real save boundaries without generating history or inferring an ambient commit.</summary>
/// <example><code>options.AddInterceptors(saveObserver);</code></example>
public sealed class ChangeHistoryAuditSaveInterceptor<TContext>(ChangeHistoryAuditBridge<TContext> bridge) : SaveChangesInterceptor where TContext : DbContext
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        bridge.Saving(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        bridge.Saving(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        bridge.Saved(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await bridge.SavedAsync(eventData.Context).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData) => bridge.SaveFailed(eventData.Context);

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        bridge.SaveFailed(eventData.Context);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void SaveChangesCanceled(DbContextEventData eventData) => bridge.SaveFailed(eventData.Context);

    /// <inheritdoc />
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        bridge.SaveFailed(eventData.Context);
        return Task.CompletedTask;
    }
}
