// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Generates short application correlation identifiers for newly started execution flows.
/// </summary>
/// <remarks>
/// Uses cryptographically random lowercase ASCII letters and digits. Generated identifiers are
/// independent from tracing, execution, and entity identifiers. Preserve supplied correlation
/// identifiers when continuing an existing flow.
/// </remarks>
/// <example><code>var correlationId = CorrelationIdGenerator.Create();</code></example>
public static class CorrelationIdGenerator
{
    /// <summary>
    /// Gets the length of a generated application correlation identifier.
    /// </summary>
    /// <example><code>var length = CorrelationIdGenerator.GeneratedLength;</code></example>
    public const int GeneratedLength = 12;

    /// <summary>
    /// Creates a random lowercase alphanumeric application correlation identifier.
    /// </summary>
    /// <returns>A new 12-character correlation identifier.</returns>
    /// <example><code>using var scope = CorrelationId.BeginScope(CorrelationIdGenerator.Create());</code></example>
    public static string Create() => KeyGenerator.CreateLowercase(GeneratedLength);

    /// <summary>Resolves a transport identifier or creates and stores one at a new execution boundary.</summary>
    /// <param name="properties">The transport metadata to enrich, when available.</param>
    /// <param name="useAmbient">Whether to continue the caller's scope when metadata has no valid identifier. Consumers use false to avoid inheriting unrelated worker context.</param>
    /// <returns>The valid explicit, ambient, or newly generated correlation identifier.</returns>
    /// <example><code>using var scope = CorrelationId.BeginScope(CorrelationIdGenerator.GetOrCreate(message.Properties));</code></example>
    public static string GetOrCreate(IDictionary<string, object> properties, bool useAmbient = true)
    {
        var identifier = CorrelationId.ReadFrom(properties);
        if (identifier is null && useAmbient && CorrelationId.IsValid(CorrelationId.Current))
        {
            identifier = CorrelationId.Current;
        }

        identifier ??= Create();
        if (properties is not null)
        {
            properties[CorrelationId.HeaderName] = identifier;
        }

        return identifier;
    }
}
