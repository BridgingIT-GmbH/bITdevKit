// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents progress information for an export operation.
/// </summary>
public sealed record ExportProgressReport
{
    /// <summary>
    /// Gets the operation name being reported.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the export format.
    /// </summary>
    public required Format Format { get; init; }

    /// <summary>
    /// Gets the number of processed rows.
    /// </summary>
    public required int ProcessedRows { get; init; }

    /// <summary>
    /// Gets the total row count when it is known.
    /// </summary>
    public int? TotalRows { get; init; }

    /// <summary>
    /// Gets the percentage of completion when it is known.
    /// </summary>
    public double? PercentageComplete { get; init; }

    /// <summary>
    /// Gets the number of bytes written so far.
    /// </summary>
    public required long BytesWritten { get; init; }

    /// <summary>
    /// Gets the number of rows skipped so far.
    /// </summary>
    public int SkippedRows { get; init; }

    /// <summary>
    /// Gets a value indicating whether the export has completed successfully.
    /// </summary>
    public required bool IsCompleted { get; init; }

    /// <summary>
    /// Gets the progress messages describing the current state.
    /// </summary>
    public IReadOnlyList<string> Messages { get; init; } = [];
}
