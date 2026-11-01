// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

/// <summary>Selects buffered defaults or explicit inline durability with finite writer overrides.</summary>
/// <example><code>auditing.WithEntityFrameworkProvider&lt;Store&gt;(options => options.Inline());</code></example>
public sealed class AuditEntityFrameworkOptionsBuilder
{
    private AuditEntityFrameworkOptions options = new();

    /// <summary>Delays audit background work after application startup, before waiting for the host database.</summary>
    /// <example><code>options.StartupDelay(TimeSpan.FromSeconds(2));</code></example>
    public AuditEntityFrameworkOptionsBuilder StartupDelay(TimeSpan delay) { this.options = this.options with { StartupDelay = delay }; return this; }

    /// <summary>Selects the provider-owned bounded writer, preserving unspecified defaults.</summary>
    /// <example><code>options.Buffered(o => o with { QueueRecordLimit = 100 });</code></example>
    public AuditEntityFrameworkOptionsBuilder Buffered(Func<AuditEntityFrameworkOptions, AuditEntityFrameworkOptions> configure = null)
    {
        this.options = (configure?.Invoke(this.options) ?? this.options) with { Buffered = true };
        return this;
    }

    /// <summary>Selects database confirmation during root finalization instead of a queue admission receipt.</summary>
    /// <example><code>options.Inline();</code></example>
    public AuditEntityFrameworkOptionsBuilder Inline() { this.options = this.options with { Buffered = false }; return this; }

    /// <summary>Copies explicit finite policy overrides.</summary>
    /// <example><code>options.Configure(o => o with { RetryCount = 1 });</code></example>
    public AuditEntityFrameworkOptionsBuilder Configure(Func<AuditEntityFrameworkOptions, AuditEntityFrameworkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        this.options = configure(this.options) ?? throw new ArgumentException("Audit EF options are required.");
        return this;
    }

    /// <summary>Validates and returns an immutable configuration.</summary>
    /// <example><code>var configuration = options.Build();</code></example>
    public AuditEntityFrameworkOptions Build() { this.options.Validate(); return this.options with { }; }
}
