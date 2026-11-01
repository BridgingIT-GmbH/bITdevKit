// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Bounds one export attempt, its source reads, formatted expansion and retained metadata.</summary>
/// <example><code>auditing.WithExports(options => options with { RecordLimit = 1000 });</code></example>
public sealed record AuditExportOptions
{
    /// <summary>Maximum whole source records per chunk.</summary>
    /// <example><code>var limit = options.RecordLimit;</code></example>
    public int RecordLimit { get; init; } = 10_000;

    /// <summary>Maximum response bytes, including every delimiter and control frame.</summary>
    /// <example><code>var limit = options.ResponseByteLimit;</code></example>
    public long ResponseByteLimit { get; init; } = 67_108_864;

    /// <summary>Maximum formatted expansion of one source graph.</summary>
    /// <example><code>var limit = options.GraphByteLimit;</code></example>
    public int GraphByteLimit { get; init; } = 8_388_608;

    /// <summary>Space reserved for the initial format framing before data admission.</summary>
    /// <example><code>var reserved = options.MetadataByteReserve;</code></example>
    public int MetadataByteReserve { get; init; } = 32_768;

    /// <summary>Space reserved for bounded terminal framing before data admission.</summary>
    /// <example><code>var reserved = options.CompletionByteReserve;</code></example>
    public int CompletionByteReserve { get; init; } = 32_768;

    /// <summary>Total chunk deadline covering source reads, formatting and output.</summary>
    /// <example><code>var deadline = options.MaxDuration;</code></example>
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum requested duration of each provider read.</summary>
    /// <example><code>var timeout = options.ProviderReadTimeout;</code></example>
    public TimeSpan ProviderReadTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Independent cleanup budget, without extending the output deadline.</summary>
    /// <example><code>var timeout = options.CleanupTimeout;</code></example>
    public TimeSpan CleanupTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Independent terminal publication budget.</summary>
    /// <example><code>var timeout = options.ObserverTimeout;</code></example>
    public TimeSpan ObserverTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum simultaneously owned attempts in this process.</summary>
    /// <example><code>var limit = options.ActiveExportLimit;</code></example>
    public int ActiveExportLimit { get; init; } = 4;

    /// <summary>Maximum retained attempt descriptors in this process.</summary>
    /// <example><code>var limit = options.DescriptorLimit;</code></example>
    public int DescriptorLimit { get; init; } = 100;

    /// <summary>Maximum serialized descriptor bytes.</summary>
    /// <example><code>var limit = options.DescriptorByteLimit;</code></example>
    public int DescriptorByteLimit { get; init; } = 32_768;

    /// <summary>Terminal metadata retention after attempt termination.</summary>
    /// <example><code>var retention = options.MetadataLifetime;</code></example>
    public TimeSpan MetadataLifetime { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Absolute lifetime assigned to newly issued progress tokens.</summary>
    /// <example><code>var lifetime = options.TokenLifetime;</code></example>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Maximum protected continuation-token characters.</summary>
    /// <example><code>var limit = options.TokenCharacterLimit;</code></example>
    public int TokenCharacterLimit { get; init; } = 8_192;

    /// <summary>Rejects non-finite or excessive budgets before opening a source window.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.RecordLimit is < 1 or > 1_000_000 || this.GraphByteLimit is < 256 or > 8_388_608 ||
            this.MetadataByteReserve is < 256 or > 32_768 || this.CompletionByteReserve is < 256 or > 32_768 ||
            this.ResponseByteLimit <= (long)this.MetadataByteReserve + this.CompletionByteReserve || this.ResponseByteLimit > 1_073_741_824 ||
            this.ActiveExportLimit is < 1 or > 100 || this.DescriptorLimit is < 1 or > 10_000 || this.DescriptorByteLimit is < 256 or > 32_768 ||
            this.TokenCharacterLimit is < 256 or > 8_192) throw new ArgumentException("Invalid bounded audit export limits.");
        foreach (var duration in new[] { this.MaxDuration, this.ProviderReadTimeout, this.CleanupTimeout, this.ObserverTimeout, this.MetadataLifetime, this.TokenLifetime })
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(1)) throw new ArgumentException("Audit export durations must be positive and finite.");
        if (this.ProviderReadTimeout > this.MaxDuration) throw new ArgumentException("A provider read cannot extend the chunk deadline.");
    }
}
