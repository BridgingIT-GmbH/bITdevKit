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
    public string FieldName { get; } = fieldName;

    public RequiredFieldError(string fieldName) : this(fieldName, null)
    {
    }
}