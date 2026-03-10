// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Error that occurred during import validation.
/// </summary>
public sealed class ImportValidationError : DataPorterError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportValidationError"/> class.
    /// </summary>
    public ImportValidationError()
        : base("Import validation failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportValidationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ImportValidationError(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportValidationError"/> class.
    /// </summary>
    /// <param name="rowNumber">The row number where the error occurred.</param>
    /// <param name="column">The column name where the error occurred.</param>
    /// <param name="message">The error message.</param>
    /// <param name="rawValue">The raw value that caused the error.</param>
    public ImportValidationError(int rowNumber, string column, string message, string rawValue = null)
        : base($"Row {rowNumber}, Column '{column}': {message}")
    {
        this.RowNumber = rowNumber;
        this.Column = column;
        this.RawValue = rawValue;
    }

    /// <summary>
    /// Gets the row number where the error occurred (1-based).
    /// </summary>
    public int RowNumber { get; }

    /// <summary>
    /// Gets the column name where the error occurred.
    /// </summary>
    public string Column { get; }

    /// <summary>
    /// Gets the raw value that caused the error.
    /// </summary>
    public string RawValue { get; }
}
