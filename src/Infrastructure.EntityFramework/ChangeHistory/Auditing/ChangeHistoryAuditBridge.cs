// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Copies only source-produced history rows and confirms their retained membership after actual source commit.</summary>
/// <example><code>history.WithAuditing&lt;CustomerDbContext&gt;();</code></example>
public sealed class ChangeHistoryAuditBridge<TContext>(IServiceProvider services, IServiceScopeFactory scopes, AuditChangeHistoryRegistration<TContext> registration)
    where TContext : DbContext
{
    private readonly ConditionalWeakTable<DbContext, ChangeHistoryAuditEvidenceBuffer> buffers = new();
    private AuditRuntime Runtime => services.GetService<AuditRuntime>();
    private AuditChangeHistoryOptions Options => registration.Options;

    /// <summary>Stages bounded copied rows at the actual source save boundary.</summary>
    /// <example><code>bridge.Saving(context);</code></example>
    public void Saving(DbContext context)
    {
        try
        {
            if (context is null) return;
            this.buffers.TryGetValue(context, out var buffer);
            buffer?.BeginSave();
            var runtime = this.Runtime;
            if (context is not TContext || runtime is null || runtime.Suppressed || runtime.Enriching || runtime.Current is not { IsRecording: true } scope) return;
            var rows = context.ChangeTracker.Entries<ChangeHistoryEntry>().Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity).Take(this.Options.MaxPendingRows + 1).ToArray();
            if (rows.Length == 0) return;
            if (rows.Length > this.Options.MaxPendingRows) { scope.ReportFailure("AuditChangeHistoryLimit"); return; }

            if (buffer is null)
            {
                buffer = this.buffers.GetValue(context, _ => new(this.Options.MaxPendingRows));
                buffer.BeginSave();
            }

            var transaction = context.Database.IsRelational() ? context.Database.CurrentTransaction?.TransactionId : null;
            foreach (var group in rows.GroupBy(row => (row.ChangeSetId, row.EntityType, row.EntityId)))
            {
                var ordered = group.OrderBy(row => row.ChangeSetSequence).ThenBy(row => row.Id).ToArray();
                var first = ordered[0];
                var modelType = context.Model.GetEntityTypes().Select(entity => entity.ClrType).FirstOrDefault(type => type.AssemblyQualifiedName == first.EntityClrType);
                var summary = ordered.All(row => row.PathKind == "Summary");
                var evidence = new AuditChangeEvidence
                {
                    SourceStoreKey = this.Options.SourceStoreKey, ChangeSetId = first.ChangeSetId.ToString("D"), SourceRowIds = ordered.Select(row => row.Id.ToString("D")).ToImmutableArray(),
                    BulkOperationId = first.BulkOperationId?.ToString("D"), EntityType = modelType is null ? first.EntityType : runtime.Options.NamingConvention.GetResourceType(modelType),
                    EntityId = first.EntityId, Operation = first.Operation, CaptureSource = first.CaptureSource, Timestamp = first.ChangedDate,
                    CaptureStrategy = first.CaptureStrategy, CaptureStatus = first.CaptureStatus, CopyMode = this.Options.Mode.ToString(), AffectedEntityCount = first.AffectedEntityCount,
                    Properties = summary ? [] : ordered.Select(row => new AuditPropertyChange
                    {
                        Path = row.PropertyPath ?? row.PropertyName, PathKind = row.PathKind, ValueType = row.ValueClrType, Operation = row.CollectionAction ?? row.Operation, ItemIdentity = row.CollectionItemId,
                        OldValue = this.Value(row.OldValue, row.OldValueHash, row.CaptureStatus), NewValue = this.Value(row.NewValue, row.NewValueHash, row.CaptureStatus)
                    }).ToImmutableArray()
                };
                var handle = scope.StageChange(evidence);
                if (handle is not null && !buffer.Add(handle, transaction, ordered.Select(row => row.Id).ToImmutableArray()))
                {
                    handle.RollBack();
                    scope.ReportFailure("AuditChangeHistoryLimit");
                }
            }
        }
        catch (Exception) { this.Report("AuditChangeHistoryCaptureFailed"); }
    }

    /// <summary>Removes copied values from a failed save before a later transaction can confirm earlier source IDs.</summary>
    /// <example><code>bridge.SaveFailed(context);</code></example>
    public void SaveFailed(DbContext context)
    {
        if (context is not null && this.buffers.TryGetValue(context, out var buffer))
            foreach (var item in buffer.FailSave()) item.Handle.RollBack();
    }

    /// <summary>Associates a source-owned EF save transaction without interpreting savepoints as commits.</summary>
    /// <example><code>bridge.Started(context, transactionId);</code></example>
    public void Started(DbContext context, Guid transactionId)
    {
        if (context is not null && this.buffers.TryGetValue(context, out var buffer)) buffer.AttachTransaction(transactionId);
    }

    /// <summary>Discards copied values after an explicitly observed full rollback.</summary>
    /// <example><code>bridge.RolledBack(context, transactionId);</code></example>
    public void RolledBack(DbContext context, Guid transactionId)
    {
        if (context is null || !this.buffers.TryGetValue(context, out var buffer)) return;
        foreach (var pending in buffer.Take(transactionId)) pending.Handle.RollBack();
    }

    /// <summary>Confirms source IDs asynchronously using an independent bounded read after commit.</summary>
    /// <example><code>await bridge.CommittedAsync(context, transactionId);</code></example>
    public async Task CommittedAsync(DbContext context, Guid? transactionId)
    {
        var pending = this.Take(context, transactionId);
        if (pending.Length == 0) return;
        try
        {
            using var timeout = new CancellationTokenSource(this.Options.VerificationTimeout);
            using var suppression = this.Runtime?.Suppress();
            await using var scope = scopes.CreateAsyncScope();
            var source = scope.ServiceProvider.GetRequiredService<TContext>();
            source.Database.SetCommandTimeout((int)Math.Ceiling(this.Options.VerificationTimeout.TotalSeconds));
            var ids = pending.SelectMany(item => item.Rows).Distinct().ToArray();
            var retained = await source.Set<ChangeHistoryEntry>().IgnoreQueryFilters().AsNoTracking().Where(row => ids.Contains(row.Id)).Select(row => row.Id).ToListAsync(timeout.Token).ConfigureAwait(false);
            var confirmed = retained.Select(id => id.ToString("D")).ToHashSet(StringComparer.Ordinal);
            foreach (var item in pending) item.Handle.Confirm(confirmed);
        }
        catch (Exception) { this.Report("AuditChangeHistoryConfirmationUnavailable"); }
    }

    /// <summary>Confirms a synchronous source commit with bounded synchronous I/O and no sync-over-async.</summary>
    /// <example><code>bridge.Committed(context, transactionId);</code></example>
    public void Committed(DbContext context, Guid? transactionId)
    {
        var pending = this.Take(context, transactionId);
        if (pending.Length == 0) return;
        try
        {
            using var suppression = this.Runtime?.Suppress();
            using var scope = scopes.CreateScope();
            var source = scope.ServiceProvider.GetRequiredService<TContext>();
            source.Database.SetCommandTimeout((int)Math.Ceiling(this.Options.VerificationTimeout.TotalSeconds));
            var ids = pending.SelectMany(item => item.Rows).Distinct().ToArray();
            var retained = source.Set<ChangeHistoryEntry>().IgnoreQueryFilters().AsNoTracking().Where(row => ids.Contains(row.Id)).Select(row => row.Id).ToList();
            var confirmed = retained.Select(id => id.ToString("D")).ToHashSet(StringComparer.Ordinal);
            foreach (var item in pending) item.Handle.Confirm(confirmed);
        }
        catch (Exception) { this.Report("AuditChangeHistoryConfirmationUnavailable"); }
    }

    /// <summary>Confirms a completed autocommit save only when no ambient or explicit transaction remains.</summary>
    /// <example><code>await bridge.SavedAsync(context);</code></example>
    public Task SavedAsync(DbContext context) => context?.Database.IsRelational() == true && context.Database.CurrentTransaction is null && System.Transactions.Transaction.Current is null
        ? this.CommittedAsync(context, null) : Task.CompletedTask;

    /// <summary>Confirms the synchronous equivalent of an observable autocommit save.</summary>
    /// <example><code>bridge.Saved(context);</code></example>
    public void Saved(DbContext context)
    {
        if (context?.Database.IsRelational() == true && context.Database.CurrentTransaction is null && System.Transactions.Transaction.Current is null) this.Committed(context, null);
    }

    private ImmutableArray<ChangeHistoryAuditEvidenceBuffer.PendingChange> Take(DbContext context, Guid? transaction)
        => context is not null && this.buffers.TryGetValue(context, out var buffer) ? buffer.Take(transaction) : [];
    private AuditValue Value(string json, string hash, string status)
    {
        if (this.Options.Mode == AuditChangeHistoryMode.ReferenceOnly) return null;
        if (json is null && hash is not null) return AuditValue.Policy("Hashed", hash.Length <= 256 ? hash : null);
        if (json is null) return status is "Skipped" or "Failed" ? AuditValue.Policy("Unavailable") : AuditValue.From((string)null);
        if (Encoding.UTF8.GetByteCount(json) > (this.Runtime?.Options.Limits.MaxDetailValueBytes ?? 4_096)) return AuditValue.Policy("Oversized");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            return document.RootElement.ValueKind == JsonValueKind.String && document.RootElement.GetString() == "***REDACTED***" ? AuditValue.Policy("Redacted") : AuditValue.Json(document.RootElement);
        }
        catch (JsonException) { return AuditValue.Policy("Unavailable"); }
    }
    private void Report(string code) { this.Runtime?.Report(code); this.Runtime?.Current?.Collector.CaptureNote(code); }
}

/// <summary>Separates explicitly configured bridge options for each source context type.</summary>
/// <example><code>var options = registration.Options;</code></example>
public sealed record AuditChangeHistoryRegistration<TContext>(AuditChangeHistoryOptions Options) where TContext : DbContext;
