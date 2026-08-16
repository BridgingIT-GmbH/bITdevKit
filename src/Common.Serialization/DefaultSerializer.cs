// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Provides the shared default serializer instance.
/// </summary>
public static class DefaultSerializer
{
    /// <summary>
    ///     Gets the default serializer.
    /// </summary>
    /// <value>
    ///     Create a new serializer
    /// </value>
    public static ISerializer Create { get; } = new MessagePackSerializer();
}
