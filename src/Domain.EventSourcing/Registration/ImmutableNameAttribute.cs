// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

/// <summary>
/// Identifies a declaration with immutable name metadata.
/// </summary>
/// <param name="immutableName">The immutable name used by the operation.</param>
[AttributeUsage(AttributeTargets.Class)]
public class ImmutableNameAttribute(string immutableName) : Attribute
{
    /// <summary>
    /// Gets the immutable name.
    /// </summary>
    public string ImmutableName { get; } = immutableName;
}
