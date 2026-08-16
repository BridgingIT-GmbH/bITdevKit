// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an enhanced validation error that captures property information and attempted value.
/// </summary>
public class ValidationError(string message, string propertyName = null, object attemptedValue = null)
    : ResultErrorBase(message ?? "Validation not satisfied")
{
    // public ValidationError(string message) : this(message, null, null)
    // {
    // }

    /// <summary>Gets the name of the property that failed validation, when supplied.</summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>Gets the value rejected by validation, when supplied.</summary>
    public object AttemptedValue { get; } = attemptedValue;
}
