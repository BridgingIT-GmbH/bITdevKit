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
    public string FieldName { get; } = fieldName;

    public object ProvidedValue { get; } = providedValue;

    public InvalidInputError() : this(null, null, null)
    {
    }

    public InvalidInputError(string fieldName, object providedValue) : this(null, fieldName, providedValue)
    {
    }
}