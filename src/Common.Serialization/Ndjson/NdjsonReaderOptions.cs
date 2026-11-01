// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Configures strict bounded NDJSON framing.</summary>
/// <example><code>var options = new NdjsonReaderOptions { AllowUnterminatedFinalRecord = true };</code></example>
public sealed class NdjsonReaderOptions
{
    /// <summary>Maximum original JSON bytes, excluding LF or CRLF.</summary>
    /// <example><code>options.MaxRecordBytes = 65536;</code></example>
    public int MaxRecordBytes { get; set; } = NdjsonDefaults.MAX_RECORD_BYTES;

    /// <summary>Explicit compatibility option to skip empty lines.</summary>
    /// <example><code>options.SkipEmptyLines = true;</code></example>
    public bool SkipEmptyLines { get; set; }

    /// <summary>Explicit compatibility option to accept a final record without a delimiter.</summary>
    /// <example><code>options.AllowUnterminatedFinalRecord = true;</code></example>
    public bool AllowUnterminatedFinalRecord { get; set; }

    /// <summary>Rejects invalid or unbounded limits.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxRecordBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(this.MaxRecordBytes, int.MaxValue - 4096);
    }

    internal NdjsonReaderOptions Capture()
    {
        this.Validate();
        return (NdjsonReaderOptions)this.MemberwiseClone();
    }
}
