// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that indicates a requested resource was not found.
/// </summary>
public class NotFoundError(string message = null) : ResultErrorBase(message ?? "Not found")
{
    public NotFoundError() : this(null)
    {
    }
}