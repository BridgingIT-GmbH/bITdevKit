// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an invalid input error that captures field information and provided value.
/// </summary>
public class InvalidInputError(string message = null, string fieldName = null, object providedValue = null)
    : ResultErrorBase(message ?? "Invalid input provided")
{
    /// <summary>Gets the name of the input field that was invalid, when supplied.</summary>
    public string FieldName { get; } = fieldName;

    /// <summary>Gets the rejected input value, when supplied.</summary>
    public object ProvidedValue { get; } = providedValue;

    /// <summary>Initializes an invalid-input error with the default message and no field details.</summary>
    public InvalidInputError() : this(null, null, null)
    {
    }

    /// <summary>Initializes an invalid-input error for a field and rejected value.</summary>
    /// <param name="fieldName">The name of the invalid input field.</param>
    /// <param name="providedValue">The value rejected by validation.</param>
    public InvalidInputError(string fieldName, object providedValue) : this(null, fieldName, providedValue)
    {
    }
}
