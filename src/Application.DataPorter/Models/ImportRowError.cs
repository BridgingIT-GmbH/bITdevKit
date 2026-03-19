// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents an error that occurred during import for a specific row.
/// </summary>
public sealed record ImportRowError
{
    /// <summary>
    /// Gets the row number where the error occurred (1-based).
    /// </summary>
    public required int RowNumber { get; init; }

    /// <summary>
    /// Gets the column name where the error occurred.
    /// </summary>
    public required string Column { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the raw value that caused the error.
    /// </summary>
    public string RawValue { get; init; }

    /// <summary>
    /// Gets the severity of the error.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
}
