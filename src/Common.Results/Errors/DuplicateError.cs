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
    public string PropertyName { get; } = propertyName;

    public object AttemptedValue { get; } = attemptedValue;

    public DuplicateError() : this(null, null, null)
    {
    }

    public DuplicateError(string propertyName, object attemptedValue) : this(null, propertyName, attemptedValue)
    {
    }
}