// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

/// <summary>Finite persistence and writer budgets for the selected EF audit provider.</summary>
/// <example><code>var options = new AuditEntityFrameworkOptions { Buffered = false };</code></example>
public sealed record AuditEntityFrameworkOptions
{
    /// <summary>Delay after application startup before schema validation and buffered delivery begin.</summary>
    /// <example><code>var delay = options.StartupDelay;</code></example>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.Zero;

    /// <summary>Maximum wait for the optional named host database readiness signal.</summary>
    /// <example><code>var timeout = options.DatabaseReadyTimeout;</code></example>
    public TimeSpan DatabaseReadyTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether finalized records enter the provider-owned asynchronous writer queue.</summary>
    /// <example><code>var buffered = options.Buffered;</code></example>
    public bool Buffered { get; init; } = true;

    /// <summary>Maximum queued plus in-flight finalized records.</summary>
    /// <example><code>var capacity = options.QueueRecordLimit;</code></example>
    public int QueueRecordLimit { get; init; } = 1_000;

    /// <summary>Maximum encoded queued plus in-flight document bytes, including integrity envelopes.</summary>
    /// <example><code>var capacity = options.QueueByteLimit;</code></example>
    public long QueueByteLimit { get; init; } = 67_108_864;

    /// <summary>Maximum admissions processed before checking writer scheduling again.</summary>
    /// <example><code>var batch = options.BatchSize;</code></example>
    public int BatchSize { get; init; } = 100;

    /// <summary>Maximum wait for a partial writer batch.</summary>
    /// <example><code>var interval = options.BatchInterval;</code></example>
    public TimeSpan BatchInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Finite additional attempts for a failed immutable append.</summary>
    /// <example><code>var retries = options.RetryCount;</code></example>
    public int RetryCount { get; init; } = 3;

    /// <summary>Initial exponential retry delay, capped at two seconds between attempts.</summary>
    /// <example><code>var delay = options.RetryDelay;</code></example>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Deadline for one bounded database operation.</summary>
    /// <example><code>var timeout = options.OperationTimeout;</code></example>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Graceful finalization/admission close/drain budget after producers stop.</summary>
    /// <example><code>var timeout = options.ShutdownTimeout;</code></example>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Rejects unbounded or invalid settings before provider startup.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.StartupDelay < TimeSpan.Zero || this.StartupDelay > TimeSpan.FromHours(1) || this.DatabaseReadyTimeout <= TimeSpan.Zero || this.DatabaseReadyTimeout > TimeSpan.FromHours(1))
            throw new ArgumentException("Audit EF startup limits are invalid.");
        if (this.QueueRecordLimit is < 1 or > 100_000 || this.QueueByteLimit is < 128 or > 1_073_741_824 || this.BatchSize < 1 || this.BatchSize > this.QueueRecordLimit ||
            this.BatchInterval <= TimeSpan.Zero || this.BatchInterval > TimeSpan.FromMinutes(1) || this.RetryCount is < 0 or > 10 || this.RetryDelay < TimeSpan.Zero || this.RetryDelay > TimeSpan.FromSeconds(10) ||
            this.OperationTimeout <= TimeSpan.Zero || this.OperationTimeout > TimeSpan.FromMinutes(1) || this.ShutdownTimeout <= TimeSpan.Zero || this.ShutdownTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentException("Audit EF writer limits are invalid.");
    }
}
