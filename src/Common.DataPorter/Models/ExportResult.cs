// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Represents the result of an export operation.
/// </summary>
public sealed record ExportResult
{
    /// <summary>
    /// Gets the number of bytes written to the output.
    /// </summary>
    public required long BytesWritten { get; init; }

    /// <summary>
    /// Gets the number of rows exported.
    /// </summary>
    public required int RowsExported { get; init; }

    /// <summary>
    /// Gets the duration of the export operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the format used for export.
    /// </summary>
    public required DataPorterFormat Format { get; init; }

    /// <summary>
    /// Gets any warnings generated during export.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets additional metadata about the export operation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
