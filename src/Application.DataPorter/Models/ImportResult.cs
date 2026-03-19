// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the result of an import operation.
/// </summary>
/// <typeparam name="T">The type of the imported data.</typeparam>
public sealed record ImportResult<T>
    where T : class
{
    /// <summary>
    /// Gets the successfully imported data items.
    /// </summary>
    public required IReadOnlyList<T> Data { get; init; }

    /// <summary>
    /// Gets the total number of rows processed.
    /// </summary>
    public required int TotalRows { get; init; }

    /// <summary>
    /// Gets the number of successfully imported rows.
    /// </summary>
    public required int SuccessfulRows { get; init; }

    /// <summary>
    /// Gets the number of failed rows.
    /// </summary>
    public required int FailedRows { get; init; }

    /// <summary>
    /// Gets the number of rows skipped by interceptors.
    /// </summary>
    public int SkippedRows { get; init; }

    /// <summary>
    /// Gets the duration of the import operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the errors encountered during import.
    /// </summary>
    public IReadOnlyList<ImportRowError> Errors { get; init; } = [];

    /// <summary>
    /// Gets any warnings generated during import.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether there are any errors.
    /// </summary>
    public bool HasErrors => this.Errors.Count > 0;
}
