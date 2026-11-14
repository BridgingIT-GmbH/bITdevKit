// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using System.Text.Json;

/// <summary>Stores explicit, case-insensitive option paths as independent JSON value snapshots.</summary>
/// <remarks>Use this for serializable configuration data. Services, delegates, polymorphic runtime objects,
/// reference identity, and serializer-specific converters are outside this contract. Applying a patch mutates its target.</remarks>
/// <example><code>var patch = new OptionsPatch(); patch.Set("Retry.Attempts", 2);</code></example>
public sealed class OptionsPatch
{
    private readonly Dictionary<string, JsonElement> values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records an exact property path and snapshots its explicit value using default JSON serialization.</summary>
    /// <typeparam name="T">The JSON-serializable value type.</typeparam>
    /// <param name="path">The dot-separated public option property path.</param>
    /// <param name="value">The value to snapshot, including null or an empty collection.</param>
    /// <example><code>patch.Set("Enabled", false);</code></example>
    public void Set<T>(string path, T value)
    {
        ValidatePath(path);
        this.values[path] = JsonSerializer.SerializeToElement(value);
    }

    /// <summary>Tests whether this exact property path was explicitly assigned, ignoring path case.</summary>
    /// <param name="path">The dot-separated option property path.</param>
    /// <returns>Whether an assignment exists for the exact path.</returns>
    /// <example><code>var hasOverride = patch.Contains("Retry.Attempts");</code></example>
    public bool Contains(string path)
    {
        ValidatePath(path);
        return this.values.ContainsKey(path);
    }

    /// <summary>Appends a string alias once, ignoring alias case and preserving the order of earlier aliases.</summary>
    /// <param name="path">The property path containing a JSON string array.</param>
    /// <param name="alias">The nonempty alias to append.</param>
    /// <example><code>patch.AppendAlias("Handlers", "logging");</code></example>
    public void AppendAlias(string path, string alias)
    {
        ValidatePath(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        var aliases = this.values.TryGetValue(path, out var value) ? value.Deserialize<string[]>() ?? [] : [];
        if (!aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
        {
            this.Set(path, aliases.Append(alias).ToArray());
        }
    }

    /// <summary>Copies another patch below an optional prefix, replacing existing assignments at matching exact paths.</summary>
    /// <param name="prefix">The parent property path, or null or empty to merge at the root.</param>
    /// <param name="child">The patch whose assignments are copied.</param>
    /// <example><code>patch.Merge("Retry", retryBuilder.Patch);</code></example>
    public void Merge(string prefix, OptionsPatch child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!string.IsNullOrEmpty(prefix)) { ValidatePath(prefix); }

        foreach (var (path, value) in child.values)
        {
            this.values[string.IsNullOrEmpty(prefix) ? path : prefix + "." + path] = value.Clone();
        }
    }

    /// <summary>Copies descendants of a property prefix into a new patch, removing that prefix from their paths.</summary>
    /// <param name="prefix">The parent property path to remove.</param>
    /// <returns>An independent patch containing only the selected descendants.</returns>
    /// <example><code>var retryPatch = patch.Extract("Retry");</code></example>
    public OptionsPatch Extract(string prefix)
    {
        ValidatePath(prefix);
        var result = new OptionsPatch();
        foreach (var (path, value) in this.values.Where(p => p.Key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
        {
            result.values[path[(prefix.Length + 1)..]] = value.Clone();
        }

        return result;
    }

    /// <summary>Applies recorded paths to initialized option data and records the supplied source for each exact path.</summary>
    /// <typeparam name="T">The option data type with writable public properties.</typeparam>
    /// <param name="options">The target to mutate; every traversed parent property must already be initialized.</param>
    /// <param name="source">The feature-defined source label to record.</param>
    /// <param name="provenance">The destination for exact property paths and their winning source labels.</param>
    /// <returns>The supplied target after its recorded assignments have been applied.</returns>
    /// <example><code>var options = patch.Apply(new FeatureOptions(), "Module", provenance);</code></example>
    public T Apply<T>(T options, string source, IDictionary<string, string> provenance) where T : class
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(provenance);
        foreach (var (path, value) in this.values)
        {
            var target = (object)options;
            var segments = path.Split('.');
            for (var i = 0; i < segments.Length - 1; i++)
            {
                target = Property(target.GetType(), segments[i]).GetValue(target) ??
                    throw new InvalidOperationException("An option path has no initialized parent.");
            }

            var property = Property(target.GetType(), segments[^1]);
            property.SetValue(target, value.Deserialize(property.PropertyType));
            provenance[path] = source;
        }

        return options;
    }

    /// <summary>Copies serializable option data through default JSON serialization without preserving reference identity.</summary>
    /// <typeparam name="T">The JSON-serializable data type.</typeparam>
    /// <param name="value">The option data to snapshot.</param>
    /// <returns>The deserialized snapshot.</returns>
    /// <example><code>var snapshot = OptionsPatch.Clone(options);</code></example>
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value));

    /// <summary>Resolves a readable and writable public instance property, ignoring case and rejecting indexers.</summary>
    /// <param name="type">The option data type.</param>
    /// <param name="name">The exact property name, ignoring case.</param>
    /// <returns>The matching option property.</returns>
    /// <example><code>var enabled = OptionsPatch.Property(typeof(FeatureOptions), "Enabled");</code></example>
    public static PropertyInfo Property(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return property?.GetMethod?.IsPublic == true && property.SetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0
            ? property : throw new ArgumentException($"Unknown or non-writable option setting '{name}'.", nameof(name));
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Split('.').Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("An option path must contain nonempty property names.", nameof(path));
        }
    }
}