// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>The finalized transport disposition.</summary>
/// <example><code>var status = NdjsonStreamStatus.Succeeded;</code></example>
public enum NdjsonStreamStatus
{
    /// <summary>Selected output, final flush, and cleanup succeeded.</summary>
    /// <example><code>var status = NdjsonStreamStatus.Succeeded;</code></example>
    Succeeded,
    /// <summary>Source, output, or cleanup failed.</summary>
    /// <example><code>var status = NdjsonStreamStatus.Failed;</code></example>
    Failed,
    /// <summary>Execution was canceled and cleanup succeeded.</summary>
    /// <example><code>var status = NdjsonStreamStatus.Canceled;</code></example>
    Canceled
}

/// <summary>The first execution failure location.</summary>
/// <example><code>var stage = NdjsonStreamFailureStage.Flush;</code></example>
public enum NdjsonStreamFailureStage
{
    /// <summary>Source failure.</summary>
    /// <example><code>var stage = NdjsonStreamFailureStage.Source;</code></example>
    Source,
    /// <summary>Encoding failure.</summary>
    /// <example><code>var stage = NdjsonStreamFailureStage.Encoding;</code></example>
    Encoding,
    /// <summary>Write failure.</summary>
    /// <example><code>var stage = NdjsonStreamFailureStage.Write;</code></example>
    Write,
    /// <summary>Flush failure.</summary>
    /// <example><code>var stage = NdjsonStreamFailureStage.Flush;</code></example>
    Flush,
    /// <summary>Cleanup failure.</summary>
    /// <example><code>var stage = NdjsonStreamFailureStage.Cleanup;</code></example>
    Cleanup
}

/// <summary>Immutable confirmed output prefixes, never recipient acknowledgments.</summary>
/// <example><code>if (outcome.Status == NdjsonStreamStatus.Succeeded) PublishMetadata();</code></example>
/// <param name="Status">Final execution status.</param>
/// <param name="SourceCompleted">Natural exhaustion observed before finalization.</param>
/// <param name="CleanupCompleted">All acquired resources safely released before finalization.</param>
/// <param name="WrittenRecordCount">Fully confirmed destination record writes.</param>
/// <param name="WrittenFramedByteCount">Fully confirmed destination bytes, including delimiters.</param>
/// <param name="FlushedRecordCount">Contiguous record prefix covered by successful flushes.</param>
/// <param name="FlushedFramedByteCount">Contiguous framed-byte prefix covered by successful flushes.</param>
/// <param name="PartialWritePossible">An output operation left transmission uncertain.</param>
/// <param name="FailureStage">The first execution failure location.</param>
/// <param name="ReasonCode">Safe stable failure code.</param>
public sealed record NdjsonStreamOutcome(
    NdjsonStreamStatus Status,
    bool SourceCompleted,
    bool CleanupCompleted,
    long WrittenRecordCount,
    long WrittenFramedByteCount,
    long FlushedRecordCount,
    long FlushedFramedByteCount,
    bool PartialWritePossible,
    NdjsonStreamFailureStage? FailureStage = null,
    string ReasonCode = null);
