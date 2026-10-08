// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Configures bounded generic operation capture and periodic persistence.</summary>
/// <example><code>services.AddProfiling(o => o.Enabled()).WithOperationProfiling();</code></example>
public sealed class OperationProfilingOptions
{
    private int maximumRetainedOperations = 10000;
    private long maximumRetainedBytes = 128L * 1024 * 1024;
    private TimeSpan maximumOperationAge = TimeSpan.FromHours(24);
    private bool countConfigured;
    private bool bytesConfigured;
    private bool ageConfigured;

    /// <summary>Gets or sets explicit operation capture enablement.</summary>
    /// <example><code>var value = options.Enabled;</code></example>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets maximum concurrently recorded roots.</summary>
    /// <example><code>var value = options.MaxActiveOperations;</code></example>
    public int MaxActiveOperations { get; set; } = 1024;

    /// <summary>Gets or sets charged bytes shared by live roots.</summary>
    /// <example><code>var value = options.MaxActiveBytes;</code></example>
    public long MaxActiveBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Gets or sets maximum charged payload per root.</summary>
    /// <example><code>var value = options.MaxRecordBytes;</code></example>
    public long MaxRecordBytes { get; set; } = 64 * 1024;

    /// <summary>Gets or sets maximum distinct structured paths per root.</summary>
    /// <example><code>var value = options.MaxSegmentPaths;</code></example>
    public int MaxSegmentPaths { get; set; } = 128;

    /// <summary>Gets or sets maximum live invocations per root.</summary>
    /// <example><code>var value = options.MaxLiveSegments;</code></example>
    public int MaxLiveSegments { get; set; } = 256;

    /// <summary>Gets or sets maximum nesting depth.</summary>
    /// <example><code>var value = options.MaxSegmentDepth;</code></example>
    public int MaxSegmentDepth { get; set; } = 32;

    /// <summary>Gets or sets maximum entries per metadata collection.</summary>
    /// <example><code>var value = options.MaxMetadataEntries;</code></example>
    public int MaxMetadataEntries { get; set; } = 32;

    /// <summary>Gets or sets maximum key or metadata name length.</summary>
    /// <example><code>var value = options.MaxKeyLength;</code></example>
    public int MaxKeyLength { get; set; } = 128;

    /// <summary>Gets or sets maximum scalar string length.</summary>
    /// <example><code>var value = options.MaxStringLength;</code></example>
    public int MaxStringLength { get; set; } = 256;

    /// <summary>Gets or sets maximum measurement unit length.</summary>
    /// <example><code>var value = options.MaxUnitLength;</code></example>
    public int MaxUnitLength { get; set; } = 32;

    /// <summary>Gets or sets maximum safe failure text length.</summary>
    /// <example><code>var value = options.MaxFailureLength;</code></example>
    public int MaxFailureLength { get; set; } = 1024;

    /// <summary>Gets or sets maximum retained failure categories per summary.</summary>
    /// <example><code>var value = options.MaxFailureCategories;</code></example>
    public int MaxFailureCategories { get; set; } = 8;

    /// <summary>Gets or sets maximum capture duration independent of business execution.</summary>
    /// <example><code>var value = options.MaxRecordingDuration;</code></example>
    public TimeSpan MaxRecordingDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets independent expiry scan interval.</summary>
    /// <example><code>var value = options.CleanupInterval;</code></example>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets maximum queued and in-flight records.</summary>
    /// <example><code>var value = options.QueueCapacity;</code></example>
    public int QueueCapacity { get; set; } = 8192;

    /// <summary>Gets or sets maximum queued and in-flight charged bytes.</summary>
    /// <example><code>var value = options.QueueBytes;</code></example>
    public long QueueBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>Gets or sets periodic writer tick interval.</summary>
    /// <example><code>var value = options.FlushInterval;</code></example>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets maximum records per batch.</summary>
    /// <example><code>var value = options.BatchSize;</code></example>
    public int BatchSize { get; set; } = 512;

    /// <summary>Gets or sets maximum charged bytes per batch.</summary>
    /// <example><code>var value = options.BatchBytes;</code></example>
    public long BatchBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Gets or sets maximum attempts started per tick including retries.</summary>
    /// <example><code>var value = options.MaxBatchesPerFlush;</code></example>
    public int MaxBatchesPerFlush { get; set; } = 8;

    /// <summary>Gets or sets budget for starting additional batches.</summary>
    /// <example><code>var value = options.FlushTimeBudget;</code></example>
    public TimeSpan FlushTimeBudget { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets or sets timeout for a storage attempt.</summary>
    /// <example><code>var value = options.AttemptTimeout;</code></example>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets maximum bounded retry count.</summary>
    /// <example><code>var value = options.MaximumRetries;</code></example>
    public int MaximumRetries { get; set; } = 2;

    /// <summary>Gets or sets maximum shutdown drain time.</summary>
    /// <example><code>var value = options.ShutdownDrainTimeout;</code></example>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets writer permission lifetime.</summary>
    /// <example><code>var value = options.WriterLeaseDuration;</code></example>
    public TimeSpan WriterLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets writer renewal interval.</summary>
    /// <example><code>var value = options.WriterRenewalInterval;</code></example>
    public TimeSpan WriterRenewalInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets default in-memory root retention count.</summary>
    /// <example><code>var value = options.MaximumRetainedOperations;</code></example>
    public int MaximumRetainedOperations { get => this.maximumRetainedOperations; set { this.maximumRetainedOperations = value; this.countConfigured = true; } }

    /// <summary>Gets or sets default in-memory charged retention bytes.</summary>
    /// <example><code>var value = options.MaximumRetainedBytes;</code></example>
    public long MaximumRetainedBytes { get => this.maximumRetainedBytes; set { this.maximumRetainedBytes = value; this.bytesConfigured = true; } }

    /// <summary>Gets or sets default in-memory retained age.</summary>
    /// <example><code>var value = options.MaximumOperationAge;</code></example>
    public TimeSpan MaximumOperationAge { get => this.maximumOperationAge; set { this.maximumOperationAge = value; this.ageConfigured = true; } }

    /// <summary>Applies provider-specific retention defaults while preserving every explicit setting.</summary>
    /// <example><code>var effective = options.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));</code></example>
    public OperationProfilingOptions SnapshotWithRetentionDefaults(int maximumCount, long? maximumBytes, TimeSpan maximumAge)
    {
        var copy = this.Snapshot();
        if (!this.countConfigured) { copy.maximumRetainedOperations = maximumCount; }

        if (!this.bytesConfigured) { copy.maximumRetainedBytes = maximumBytes ?? long.MaxValue; }

        if (!this.ageConfigured) { copy.maximumOperationAge = maximumAge; }

        return copy;
    }

    /// <summary>Freezes current capture options in an independent copy.</summary>
    /// <example><code>var effective = options.Snapshot();</code></example>
    public OperationProfilingOptions Snapshot() => (OperationProfilingOptions)this.MemberwiseClone();

    /// <summary>Validates enabled capture limits.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (!this.Enabled)
        {
            return;
        }

        if (this.MaxActiveOperations <= 0
            || this.MaxActiveBytes <= 0
            || this.MaxRecordBytes <= 0
            || this.MaxSegmentPaths <= 0
            || this.MaxLiveSegments <= 0
            || this.MaxSegmentDepth <= 0
            || this.MaxMetadataEntries <= 0
            || this.MaxKeyLength <= 0
            || this.MaxStringLength <= 0
            || this.MaxUnitLength <= 0
            || this.MaxFailureLength <= 0
            || this.MaxFailureCategories <= 0
            || this.QueueCapacity <= 0
            || this.QueueBytes <= 0
            || this.BatchSize <= 0
            || this.BatchBytes <= 0
            || this.MaxBatchesPerFlush <= 0
            || this.MaximumRetainedOperations <= 0
            || this.MaximumRetainedBytes <= 0
            || this.MaxRecordingDuration <= TimeSpan.Zero || this.MaxRecordingDuration == TimeSpan.MaxValue
            || this.CleanupInterval <= TimeSpan.Zero || this.CleanupInterval == TimeSpan.MaxValue
            || this.FlushInterval <= TimeSpan.Zero || this.FlushInterval == TimeSpan.MaxValue
            || this.FlushTimeBudget <= TimeSpan.Zero || this.FlushTimeBudget == TimeSpan.MaxValue
            || this.AttemptTimeout <= TimeSpan.Zero || this.AttemptTimeout == TimeSpan.MaxValue
            || this.ShutdownDrainTimeout <= TimeSpan.Zero || this.ShutdownDrainTimeout == TimeSpan.MaxValue
            || this.WriterLeaseDuration <= TimeSpan.Zero || this.WriterLeaseDuration == TimeSpan.MaxValue
            || this.WriterRenewalInterval <= TimeSpan.Zero || this.WriterRenewalInterval == TimeSpan.MaxValue
            || this.MaximumOperationAge <= TimeSpan.Zero || this.MaximumOperationAge == TimeSpan.MaxValue
            || this.MaximumRetries < 0 || this.MaximumRetries > 2
            || this.CleanupInterval > this.MaxRecordingDuration
            || this.WriterRenewalInterval >= this.WriterLeaseDuration
            || this.MaxRecordBytes > this.MaxActiveBytes
            || this.BatchSize > this.QueueCapacity || this.BatchBytes > this.QueueBytes
        )
        {
            throw new InvalidOperationException("Operation Profiling limits must be positive, finite and internally consistent.");
        }
    }
}

/// <summary>Configures generic operation recording without HTTP or Runtime dependencies.</summary>
/// <example><code>options.FlushInterval(TimeSpan.FromSeconds(1)).BatchSize(512);</code></example>
public sealed class OperationProfilingOptionsBuilder(OperationProfilingOptions target)
{
    /// <summary>Sets Enabled.</summary>
    /// <example><code>options.Enabled(value);</code></example>
    public OperationProfilingOptionsBuilder Enabled(bool value = true)
    {
        target.Enabled = value;
        return this;
    }

    /// <summary>Sets FlushInterval.</summary>
    /// <example><code>options.FlushInterval(value);</code></example>
    public OperationProfilingOptionsBuilder FlushInterval(TimeSpan value)
    {
        target.FlushInterval = value;
        return this;
    }

    /// <summary>Sets BatchSize.</summary>
    /// <example><code>options.BatchSize(value);</code></example>
    public OperationProfilingOptionsBuilder BatchSize(int value)
    {
        target.BatchSize = value;
        return this;
    }

    /// <summary>Sets MaxBatchesPerFlush.</summary>
    /// <example><code>options.MaxBatchesPerFlush(value);</code></example>
    public OperationProfilingOptionsBuilder MaxBatchesPerFlush(int value)
    {
        target.MaxBatchesPerFlush = value;
        return this;
    }

    /// <summary>Sets FlushTimeBudget.</summary>
    /// <example><code>options.FlushTimeBudget(value);</code></example>
    public OperationProfilingOptionsBuilder FlushTimeBudget(TimeSpan value)
    {
        target.FlushTimeBudget = value;
        return this;
    }

    /// <summary>Sets MaxRecordingDuration.</summary>
    /// <example><code>options.MaxRecordingDuration(value);</code></example>
    public OperationProfilingOptionsBuilder MaxRecordingDuration(TimeSpan value)
    {
        target.MaxRecordingDuration = value;
        return this;
    }

    /// <summary>Sets CleanupInterval.</summary>
    /// <example><code>options.CleanupInterval(value);</code></example>
    public OperationProfilingOptionsBuilder CleanupInterval(TimeSpan value)
    {
        target.CleanupInterval = value;
        return this;
    }

    /// <summary>Configures advanced bounded capture, persistence and retention settings.</summary>
    /// <example><code>options.Configure(o => o.MaxActiveOperations = 512);</code></example>
    public OperationProfilingOptionsBuilder Configure(Action<OperationProfilingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(target);
        return this;
    }
}
