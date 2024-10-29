// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Validation error for collection items that includes the index of the failed item.
/// </summary>
public class CollectionValidationError(
    string message,
    int index,
    string propertyName = null,
    object attemptedValue = null)
    : ValidationError(message, propertyName, attemptedValue)
{
    public int Index { get; } = index;
}