// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>Controls copied source evidence without enabling or changing source ChangeHistory capture.</summary>
/// <example><code>history.WithAuditing&lt;CustomerDbContext&gt;(options => options with { Mode = AuditChangeHistoryMode.ReferenceOnly });</code></example>
public sealed record AuditChangeHistoryOptions
{
    /// <summary>Stable host alias for the source history store, never a connection string.</summary>
    /// <example><code>var options = new AuditChangeHistoryOptions { SourceStoreKey = "customers" };</code></example>
    public string SourceStoreKey { get; init; }

    /// <summary>Whether permitted copied values can be retained after confirmed source commit.</summary>
    /// <example><code>options = options with { Mode = AuditChangeHistoryMode.ReferenceOnly };</code></example>
    public AuditChangeHistoryMode Mode { get; init; } = AuditChangeHistoryMode.Snapshot;

    /// <summary>Maximum tracked row references in one source context observation buffer.</summary>
    /// <example><code>var bound = options.MaxPendingRows;</code></example>
    public int MaxPendingRows { get; init; } = 1_000;

    /// <summary>Independent asynchronous confirmation budget and synchronous command timeout.</summary>
    /// <example><code>var timeout = options.VerificationTimeout;</code></example>
    /// <remarks>Synchronous connection establishment also uses the source provider connection timeout configured by the host.</remarks>
    public TimeSpan VerificationTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Validates bounds before installing any source interceptor.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.SourceStoreKey) || this.SourceStoreKey.Length > 512 || this.SourceStoreKey.Any(char.IsControl) ||
            !Enum.IsDefined(this.Mode) || this.MaxPendingRows is < 1 or > 10_000 || this.VerificationTimeout <= TimeSpan.Zero || this.VerificationTimeout > TimeSpan.FromSeconds(10))
            throw new ArgumentException("ChangeHistory audit bridge options are invalid.");
    }
}

/// <summary>Chooses copied value snapshots or source references only.</summary>
/// <example><code>var mode = AuditChangeHistoryMode.Snapshot;</code></example>
public enum AuditChangeHistoryMode
{
    /// <summary>Copies permitted source values, revealing none until their commit is confirmed.</summary>
    Snapshot,

    /// <summary>Retains source identities and property paths without old or new values.</summary>
    ReferenceOnly
}
