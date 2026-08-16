// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error for missing required fields or parameters.
/// </summary>
public class RequiredFieldError(string fieldName, string message = null)
    : ResultErrorBase(message ?? $"Required field '{fieldName}' is missing or empty")
{
    /// <summary>Gets the name of the required field that was missing or empty.</summary>
    public string FieldName { get; } = fieldName;

    /// <summary>Initializes an error with a generated message naming the required field.</summary>
    /// <param name="fieldName">The name of the required field that was missing or empty.</param>
    public RequiredFieldError(string fieldName) : this(fieldName, null)
    {
    }
}
