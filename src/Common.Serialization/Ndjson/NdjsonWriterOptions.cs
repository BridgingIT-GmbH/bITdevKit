// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Configures bounded sequence output. Values are captured by each writer.</summary>
/// <example><code>var options = new NdjsonWriterOptions { MaxRecordBytes = 65536 };</code></example>
public sealed class NdjsonWriterOptions
{
    /// <summary>Maximum encoded JSON bytes, including envelope overhead, excluding LF.</summary>
    /// <example><code>options.MaxRecordBytes = 65536;</code></example>
    public int MaxRecordBytes { get; set; } = NdjsonDefaults.MAX_RECORD_BYTES;

    /// <summary>Optional framed-byte batch capacity. Supply the delay too.</summary>
    /// <example><code>options.BatchByteLimit = 65536;</code></example>
    public int? BatchByteLimit { get; set; }

    /// <summary>Maximum time since the first staged frame, never reset by later frames.</summary>
    /// <example><code>options.BatchMaxDelay = TimeSpan.FromMilliseconds(100);</code></example>
    public TimeSpan? BatchMaxDelay { get; set; }

    /// <summary>Maximum asynchronous wait for producer settlement and disposal.</summary>
    /// <example><code>options.CleanupTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan CleanupTimeout { get; set; } = NdjsonDefaults.CleanupTimeout;

    /// <summary>Separate maximum wait for terminal observer notification.</summary>
    /// <example><code>options.ObserverTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan ObserverTimeout { get; set; } = NdjsonDefaults.ObserverTimeout;

    /// <summary>Rejects invalid or unbounded options.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxRecordBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(this.MaxRecordBytes, int.MaxValue - 4096);
        NdjsonOptionValidation.Positive(this.CleanupTimeout, nameof(this.CleanupTimeout));
        NdjsonOptionValidation.Positive(this.ObserverTimeout, nameof(this.ObserverTimeout));
        if (this.BatchByteLimit.HasValue != this.BatchMaxDelay.HasValue)
        {
            throw new ArgumentException("Batch byte limit and delay must be configured together.");
        }

        if (this.BatchByteLimit is { } capacity)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
            NdjsonOptionValidation.Positive(this.BatchMaxDelay.Value, nameof(this.BatchMaxDelay));
        }
    }

    internal NdjsonWriterOptions Capture()
    {
        this.Validate();
        return (NdjsonWriterOptions)this.MemberwiseClone();
    }
}

internal static class NdjsonOptionValidation
{
    internal static void Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(name, "A positive finite timer interval is required.");
        }
    }
}
