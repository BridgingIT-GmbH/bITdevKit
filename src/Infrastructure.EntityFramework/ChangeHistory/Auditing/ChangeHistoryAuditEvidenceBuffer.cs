// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Collections.Immutable;
using BridgingIT.DevKit.Application.Auditing;

/// <summary>Bounds weak ownership and source row identifiers without retaining source rows or values.</summary>
/// <example><code>var buffer = new ChangeHistoryAuditEvidenceBuffer(1_000);</code></example>
public sealed class ChangeHistoryAuditEvidenceBuffer(int maximumRows)
{
    private readonly List<PendingChange> pending = [];

    /// <summary>Current save invocation identity, used only for source observation.</summary>
    /// <example><code>buffer.BeginSave();</code></example>
    public Guid SaveId { get; private set; }

    /// <summary>Begins one save after removing handles whose audit owners have already finalized.</summary>
    /// <example><code>buffer.BeginSave();</code></example>
    public void BeginSave()
    {
        this.pending.RemoveAll(item => !item.Handle.IsOpen);
        this.SaveId = Guid.NewGuid();
    }

    /// <summary>Admits bounded identifiers for a collector-owned copied group.</summary>
    /// <example><code>var retained = buffer.Add(handle, transactionId, rowIds);</code></example>
    public bool Add(AuditChangeHandle handle, Guid? transactionId, ImmutableArray<Guid> rows)
    {
        if (handle is null) return false;
        if (rows.Length + this.pending.Sum(item => item.Rows.Length) > maximumRows) return false;
        this.pending.Add(new(handle, transactionId, this.SaveId, rows));
        return true;
    }

    /// <summary>Associates an EF-created save transaction with previously staged rows.</summary>
    /// <example><code>buffer.AttachTransaction(transactionId);</code></example>
    public void AttachTransaction(Guid id)
    {
        for (var index = 0; index < this.pending.Count; index++)
            if (this.pending[index].TransactionId is null) this.pending[index] = this.pending[index] with { TransactionId = id };
    }

    /// <summary>Discards observations from a save that failed or was canceled, without discarding earlier saves.</summary>
    /// <example><code>foreach (var item in buffer.FailSave()) item.Handle.RollBack();</code></example>
    public ImmutableArray<PendingChange> FailSave()
    {
        var items = this.pending.Where(item => item.SaveId == this.SaveId).ToImmutableArray();
        this.pending.RemoveAll(item => item.SaveId == this.SaveId);
        return items;
    }

    /// <summary>Transfers matching weak observations for finite commit confirmation.</summary>
    /// <example><code>var changes = buffer.Take(transactionId);</code></example>
    public ImmutableArray<PendingChange> Take(Guid? transactionId)
    {
        var items = this.pending.Where(item => item.TransactionId == transactionId && item.Handle.IsOpen).ToImmutableArray();
        this.pending.RemoveAll(item => item.TransactionId == transactionId || !item.Handle.IsOpen);
        return items;
    }

    /// <summary>One bounded observation, containing no source values or service provider.</summary>
    /// <example><code>var ids = pending.Rows;</code></example>
    public sealed record PendingChange(AuditChangeHandle Handle, Guid? TransactionId, Guid SaveId, ImmutableArray<Guid> Rows);
}
