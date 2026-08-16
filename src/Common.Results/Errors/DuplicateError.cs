// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a duplicate value error that captures property information and attempted value.
/// </summary>
public class DuplicateError(string message = null, string propertyName = null, object attemptedValue = null)
    : ResultErrorBase(message ?? "Duplicate value")
{
    /// <summary>Gets the name of the property whose value was duplicated, when supplied.</summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>Gets the value rejected as a duplicate, when supplied.</summary>
    public object AttemptedValue { get; } = attemptedValue;

    /// <summary>Initializes a duplicate-value error with the default message and no property details.</summary>
    public DuplicateError() : this(null, null, null)
    {
    }

    /// <summary>Initializes a duplicate-value error for a property and attempted value.</summary>
    /// <param name="propertyName">The property associated with the duplicate value.</param>
    /// <param name="attemptedValue">The value rejected as a duplicate.</param>
    public DuplicateError(string propertyName, object attemptedValue) : this(null, propertyName, attemptedValue)
    {
    }
}
