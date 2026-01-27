// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the total number of rows validated.
    /// </summary>
    public required int TotalRows { get; init; }

    /// <summary>
    /// Gets the number of valid rows.
    /// </summary>
    public required int ValidRows { get; init; }

    /// <summary>
    /// Gets the number of invalid rows.
    /// </summary>
    public required int InvalidRows { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ImportRowError> Errors { get; init; } = [];

    /// <summary>
    /// Gets any warnings generated during validation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="totalRows">The total number of rows validated.</param>
    /// <returns>A successful ValidationResult.</returns>
    public static ValidationResult Success(int totalRows) => new()
    {
        IsValid = true,
        TotalRows = totalRows,
        ValidRows = totalRows,
        InvalidRows = 0
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="totalRows">The total number of rows validated.</param>
    /// <param name="validRows">The number of valid rows.</param>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed ValidationResult.</returns>
    public static ValidationResult Failure(int totalRows, int validRows, IReadOnlyList<ImportRowError> errors) => new()
    {
        IsValid = false,
        TotalRows = totalRows,
        ValidRows = validRows,
        InvalidRows = totalRows - validRows,
        Errors = errors
    };
}
