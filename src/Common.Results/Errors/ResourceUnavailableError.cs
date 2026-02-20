// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error indicating that a resource is temporarily unavailable.
/// </summary>
public class ResourceUnavailableError(string message = null) : ResultErrorBase(message ?? "Resource is temporarily unavailable")
{
    public ResourceUnavailableError() : this(null)
    {
    }
}