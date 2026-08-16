// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Adds custom key-value tags to activities created for a class or method.
/// </summary>
/// <param name="extraAttributes">Tags in <c>key:value</c> form; keys must be unique.</param>
/// <exception cref="ArgumentException">A tag does not contain exactly one separator or a key is duplicated.</exception>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class ActivityAttributesAttribute(params string[] extraAttributes) : Attribute
{
    /// <summary>
    ///     Gets the parsed activity tags keyed by tag name.
    /// </summary>
    public IDictionary<string, string> Attributes { get; } =
        TraceAttributesInputsFormat.ActivityAttributesStringsToDictionary(extraAttributes);
}

internal static class TraceAttributesInputsFormat
{
    internal static IDictionary<string, string> ActivityAttributesStringsToDictionary(params string[] attributes)
    {
        if (!attributes.Any())
        {
            return new Dictionary<string, string>();
        }

        var attributeFragements = attributes.Select(x => x.Split(":")).ToArray();

        EnsureAttributeSyntax(attributes, attributeFragements);

        EnsureUniqueKeys(attributes, attributeFragements);

        return attributeFragements.ToDictionary(x => x[0], x => x[1]);
    }

    private static void EnsureUniqueKeys(string[] attributes, string[][] attributeFragements)
    {
        var attributeKeys = attributeFragements.Select(x => x[0]).ToArray();
        if (attributeKeys.Length != attributeKeys.Distinct().ToArray().Length)
        {
            throw new ArgumentException(
                $"Attribute keys must be unique. Provided value was: {string.Join(',', attributes)}");
        }
    }

    private static void EnsureAttributeSyntax(string[] attributes, string[][] attributeFragements)
    {
        var illegalFragments = attributeFragements.Where(x => x.Length != 2);
        if (illegalFragments.Any())
        {
            throw new ArgumentException($"Illegal attribute values provided in value:{attributes}" +
                " The correct syntax is \"key:value\"");
        }
    }
}
