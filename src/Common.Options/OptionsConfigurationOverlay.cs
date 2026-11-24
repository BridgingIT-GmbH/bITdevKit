// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections;
using Microsoft.Extensions.Configuration;

/// <summary>Applies present configuration properties to a snapshot and replaces supplied collections in full.</summary>
/// <remarks>This processes an existing configuration tree once. It does not load files, subscribe to reload,
/// or select feature precedence. Use serializable option data with writable public properties.</remarks>
/// <example><code>var effective = OptionsConfigurationOverlay.Apply(options, section, provenance);</code></example>
public static class OptionsConfigurationOverlay
{
    /// <summary>Snapshots option data, applies supplied properties, and records each winning configuration path.</summary>
    /// <typeparam name="T">The JSON-serializable option data type.</typeparam>
    /// <param name="options">The initialized defaults or previously composed data to copy.</param>
    /// <param name="section">The configuration tree containing explicitly supplied properties.</param>
    /// <param name="provenance">The destination for exact property paths and the source label Configuration.</param>
    /// <returns>An independent option snapshot with the present configuration properties applied.</returns>
    /// <remarks>Absent properties keep their prior values. Nested option objects merge recursively.
    /// Arrays, standard list/set shapes, and dictionaries bind as complete replacements. A present collection
    /// with no bound value becomes empty. Unknown or non-writable option properties are rejected.</remarks>
    /// <example><code>var effective = OptionsConfigurationOverlay.Apply(defaults, configuration.GetSection("Reports"), provenance);</code></example>
    public static T Apply<T>(T options, IConfiguration section, IDictionary<string, string> provenance) where T : class
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(provenance);
        options = OptionsPatch.Clone(options);
        Merge(options, section, "", provenance);
        return options;
    }

    private static void Merge(object target, IConfiguration section, string prefix, IDictionary<string, string> provenance)
    {
        foreach (var child in section.GetChildren())
        {
            var property = OptionsPatch.Property(target.GetType(), child.Key);
            var path = prefix + property.Name;
            var type = property.PropertyType;
            if (type != typeof(string) && !type.IsValueType && !typeof(IEnumerable).IsAssignableFrom(type))
            {
                var nested = property.GetValue(target) ?? Activator.CreateInstance(type);
                Merge(nested, child, path + ".", provenance);
                property.SetValue(target, nested);
                continue;
            }

            var value = child.Get(type);
            if (value is null && type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
            {
                value = Empty(type);
            }

            property.SetValue(target, value);
            provenance[path] = "Configuration";
        }
    }

    private static object Empty(Type type)
    {
        if (type.IsArray && type.GetArrayRank() == 1)
        {
            return Array.CreateInstance(type.GetElementType(), 0);
        }

        if (!type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) is not null)
        {
            return Activator.CreateInstance(type);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();
            var implementation = definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>) ? typeof(Dictionary<,>) :
                definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>) ? typeof(HashSet<>) :
                definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) || definition == typeof(IList<>) ||
                definition == typeof(IReadOnlyCollection<>) || definition == typeof(IReadOnlyList<>) ? typeof(List<>) : null;
            if (implementation is not null)
            {
                return Activator.CreateInstance(implementation.MakeGenericType(arguments));
            }
        }

        throw new ArgumentException("An empty value is unsupported for this option collection shape.");
    }
}