// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error indicating that a quota, limit, or threshold has been exceeded.
/// </summary>
public class QuotaExceededError(string message, long? currentValue = null, long? maxAllowed = null)
    : ResultErrorBase(message ?? "Quota exceeded")
{
    /// <summary>Gets the observed quota value at the time of failure, when supplied.</summary>
    public long? CurrentValue { get; } = currentValue;

    /// <summary>Gets the maximum permitted value, when supplied.</summary>
    public long? MaxAllowed { get; } = maxAllowed;
}
