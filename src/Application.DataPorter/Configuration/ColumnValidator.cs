// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents a validator for a column.
/// </summary>
public sealed class ColumnValidator
{
    /// <summary>
    /// Gets or sets the validation function.
    /// </summary>
    public required Func<object, bool> Validate { get; init; }

    /// <summary>
    /// Gets or sets the error message when validation fails.
    /// </summary>
    public required string ErrorMessage { get; init; }
}
