// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using MessagePack;
using MessagePack.Resolvers;

/// <summary>
///     Provides the standard MessagePack options used by DevKit serializers.
/// </summary>
public static class MessagePackSerializerSettings
{
    /// <summary>
    ///     Gets contractless resolver options that allow serialization of private members.
    /// </summary>
    /// <returns>The shared MessagePack serializer options.</returns>
    public static MessagePackSerializerOptions Create()
    {
        return ContractlessStandardResolverAllowPrivate.Options;
    }
}
