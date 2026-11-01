// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Weak, bounded transaction observation ownership that never retains source contexts, rows, or scoped services.</summary>
/// <example><code>handle.Confirm(committedSourceRowIds);</code></example>
public sealed class AuditChangeHandle
{
    private readonly WeakReference<AuditCollector> owner;

    /// <summary>Creates a handle for an already admitted change group.</summary>
    /// <example><code>var handle = new AuditChangeHandle(collector, groupId);</code></example>
    public AuditChangeHandle(AuditCollector owner, Guid id)
    {
        this.owner = new(owner ?? throw new ArgumentNullException(nameof(owner)));
        this.Id = id;
    }

    /// <summary>The admitted audit-owned group identity.</summary>
    /// <example><code>var id = handle.Id;</code></example>
    public Guid Id { get; }

    /// <summary>Whether the owner can still accept a confirmed source outcome.</summary>
    /// <example><code>if (handle.IsOpen) handle.Confirm(ids);</code></example>
    public bool IsOpen => this.owner.TryGetTarget(out var collector) && collector.CanCapture;

    /// <summary>Promotes only source rows whose presence after a confirmed commit was checked.</summary>
    /// <remarks>Late confirmation cannot rewrite a finalized record.</remarks>
    /// <example><code>handle.Confirm(new HashSet&lt;string&gt; { sourceRowId });</code></example>
    public void Confirm(IReadOnlySet<string> committedSourceRowIds)
    {
        if (this.owner.TryGetTarget(out var collector)) collector.ConfirmChange(this.Id, committedSourceRowIds);
    }

    /// <summary>Discards values from a source operation known to have rolled back or failed.</summary>
    /// <example><code>handle.RollBack();</code></example>
    public void RollBack()
    {
        if (this.owner.TryGetTarget(out var collector)) collector.DiscardChange(this.Id);
    }
}
