// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using System.Text.Json;

/// <summary>Shared defaults captured into endpoint descriptors and per-response executions.</summary>
/// <example><code>services.AddNdjsonStreaming(o => o.MaxRecordBytes = 65536);</code></example>
public sealed class NdjsonStreamingOptions
{
    /// <summary>Maximum JSON bytes, excluding delimiters.</summary>
    /// <example><code>options.MaxRecordBytes = 65536;</code></example>
    public int MaxRecordBytes { get; set; } = NdjsonDefaults.MAX_RECORD_BYTES;

    /// <summary>Declared raw or envelope wire mode.</summary>
    /// <example><code>options.Mode = NdjsonMode.Envelope;</code></example>
    public NdjsonMode Mode { get; set; } = NdjsonMode.Raw;

    /// <summary>Idle envelope activity interval.</summary>
    /// <example><code>options.HeartbeatInterval = TimeSpan.FromSeconds(15);</code></example>
    public TimeSpan HeartbeatInterval { get; set; } = NdjsonDefaults.HeartbeatInterval;

    /// <summary>Deadline for each response start, write, or flush.</summary>
    /// <example><code>options.WriteTimeout = TimeSpan.FromSeconds(30);</code></example>
    public TimeSpan WriteTimeout { get; set; } = NdjsonDefaults.WriteTimeout;

    /// <summary>One asynchronous producer cleanup grace period.</summary>
    /// <example><code>options.CleanupTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan CleanupTimeout { get; set; } = NdjsonDefaults.CleanupTimeout;

    /// <summary>Independent terminal observer wait budget.</summary>
    /// <example><code>options.ObserverTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan ObserverTimeout { get; set; } = NdjsonDefaults.ObserverTimeout;

    /// <summary>Optional overall normal-production deadline.</summary>
    /// <example><code>options.MaxDuration = TimeSpan.FromMinutes(10);</code></example>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>Optional framed-byte batch capacity, paired with a delay.</summary>
    /// <example><code>options.BatchByteLimit = 65536;</code></example>
    public int? BatchByteLimit { get; set; }

    /// <summary>Maximum delay from the first pending batch frame.</summary>
    /// <example><code>options.BatchMaxDelay = TimeSpan.FromMilliseconds(100);</code></example>
    public TimeSpan? BatchMaxDelay { get; set; }

    /// <summary>Explicit host compression opt-in requiring delivery verification.</summary>
    /// <example><code>options.EnableCompression = true;</code></example>
    public bool EnableCompression { get; set; } = false;

    /// <summary>Optional attachment filename; omitted for ordinary SPA consumption.</summary>
    /// <example><code>options.DownloadFileName = "weather.ndjson";</code></example>
    public string DownloadFileName { get; set; }

    /// <summary>Optional application JSON overrides, cloned before use.</summary>
    /// <example><code>options.JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);</code></example>
    public JsonSerializerOptions JsonOptions { get; set; }

    /// <summary>Rejects invalid limits, deadlines, and unsafe presentation settings.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        this.WriterOptions().Validate();
        if (!Enum.IsDefined(this.Mode)) { throw new ArgumentOutOfRangeException(nameof(this.Mode)); }

        NdjsonOptionValidation.Positive(this.HeartbeatInterval, nameof(this.HeartbeatInterval));
        NdjsonOptionValidation.Positive(this.WriteTimeout, nameof(this.WriteTimeout));
        if (this.MaxDuration is { } duration) { NdjsonOptionValidation.Positive(duration, nameof(this.MaxDuration)); }

        if (this.DownloadFileName is { } name && (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\r', '\n', '/', '\\']) >= 0 || name.Any(char.IsControl)))
        {
            throw new ArgumentException("A safe attachment filename is required.", nameof(this.DownloadFileName));
        }
    }

    internal NdjsonWriterOptions WriterOptions() => new()
    {
        MaxRecordBytes = this.MaxRecordBytes,
        BatchByteLimit = this.BatchByteLimit,
        BatchMaxDelay = this.BatchMaxDelay,
        CleanupTimeout = this.CleanupTimeout,
        ObserverTimeout = this.ObserverTimeout
    };

    internal NdjsonStreamingOptions Capture(JsonSerializerOptions json = null)
    {
        this.Validate();
        var snapshot = (NdjsonStreamingOptions)this.MemberwiseClone();
        snapshot.JsonOptions = new JsonSerializerOptions(this.JsonOptions ?? json ?? DefaultJsonSerializerOptions.Create()) { WriteIndented = false };
        return snapshot;
    }

    internal NdjsonStreamingOptions Apply(NdjsonEndpointOptions options)
    {
        var snapshot = this.Capture();
        if (options is null) { return snapshot; }

        if (options.MaxRecordBytes is { } valueMaxRecordBytes) { snapshot.MaxRecordBytes = valueMaxRecordBytes; }

        if (options.Mode is { } valueMode) { snapshot.Mode = valueMode; }

        if (options.HeartbeatInterval is { } valueHeartbeatInterval) { snapshot.HeartbeatInterval = valueHeartbeatInterval; }

        if (options.WriteTimeout is { } valueWriteTimeout) { snapshot.WriteTimeout = valueWriteTimeout; }

        if (options.CleanupTimeout is { } valueCleanupTimeout) { snapshot.CleanupTimeout = valueCleanupTimeout; }

        if (options.ObserverTimeout is { } valueObserverTimeout) { snapshot.ObserverTimeout = valueObserverTimeout; }

        if (options.EnableCompression is { } valueEnableCompression) { snapshot.EnableCompression = valueEnableCompression; }

        if (options.DownloadFileName is { } valueDownloadFileName) { snapshot.DownloadFileName = valueDownloadFileName; }

        if (options.JsonOptions is { } valueJsonOptions) { snapshot.JsonOptions = valueJsonOptions; }

        if (options.ClearMaxDuration && options.MaxDuration.HasValue) { throw new ArgumentException("Duration override and clear are mutually exclusive."); }

        snapshot.MaxDuration = options.ClearMaxDuration ? null : options.MaxDuration ?? snapshot.MaxDuration;
        if (options.DisableBatching)
        {
            if (options.BatchByteLimit.HasValue || options.BatchMaxDelay.HasValue) { throw new ArgumentException("Batch disable and batch settings are mutually exclusive."); }

            snapshot.BatchByteLimit = null;
            snapshot.BatchMaxDelay = null;
        }
        else if (options.BatchByteLimit.HasValue || options.BatchMaxDelay.HasValue)
        {
            snapshot.BatchByteLimit = options.BatchByteLimit;
            snapshot.BatchMaxDelay = options.BatchMaxDelay;
        }

        return snapshot.Capture();
    }
}
