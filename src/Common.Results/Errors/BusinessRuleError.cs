// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a general business rule violation.
/// </summary>
public class BusinessRuleError(string message = null) : ResultErrorBase(message ?? "Business rule violation")
{
    public BusinessRuleError() : this(null)
    {
    }
}