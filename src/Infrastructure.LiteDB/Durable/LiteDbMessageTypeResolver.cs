// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

/// <summary>Maps operational message type identifiers to the canonical tokens stored by LiteDB brokers.</summary>
public static class LiteDbMessageTypeResolver
{
    /// <summary>Resolves CLR identities and unambiguous display names from registered subscriptions.</summary>
    /// <param name="identifier">The canonical token, CLR full name, simple name, or subscription display name.</param>
    /// <param name="registeredTypes">The known subscription types.</param>
    /// <returns>The assembly-qualified storage token.</returns>
    /// <exception cref="ArgumentException">The identifier is empty, unknown, or ambiguous.</exception>
    public static string Resolve(string identifier, IEnumerable<Type> registeredTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentNullException.ThrowIfNull(registeredTypes);
        var resolved = Type.GetType(identifier, throwOnError: false);
        if (resolved is not null) { return resolved.AssemblyQualifiedNameShort(); }

        var matches = registeredTypes.Distinct().Where(type => type.FullName == identifier || type.Name == identifier ||
            type.PrettyName(false) == identifier || type.AssemblyQualifiedNameShort() == identifier).Take(2).ToArray();
        return matches.Length switch
        {
            1 => matches[0].AssemblyQualifiedNameShort(),
            0 => throw new ArgumentException("The message type is not registered or resolvable. Use its assembly-qualified name.", nameof(identifier)),
            _ => throw new ArgumentException("The message type identifier is ambiguous. Use its assembly-qualified name.", nameof(identifier))
        };
    }
}
