// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Enhanced ValidationError that includes property information from FluentValidation.
/// </summary>
public class ValidationError(string message, string propertyName = null, object attemptedValue = null)
    : ResultErrorBase(message ?? "Validation error")
{
    public ValidationError(string message) : this(message, null, null)
    {
    }

    public string PropertyName { get; } = propertyName;
    public object AttemptedValue { get; } = attemptedValue;
}