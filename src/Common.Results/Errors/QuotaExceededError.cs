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
    public long? CurrentValue { get; } = currentValue;

    public long? MaxAllowed { get; } = maxAllowed;
}