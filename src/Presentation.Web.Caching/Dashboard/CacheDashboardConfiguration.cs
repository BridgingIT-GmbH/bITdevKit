// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Resolves independently enabled caching Dashboard settings without enabling external API routes.</summary>
/// <example><code>var enabled = configuration.Enabled;</code></example>
public sealed class CacheDashboardConfiguration
{
    /// <summary>Captures the immutable catalog's explicit Dashboard integration fields.</summary>
    public CacheDashboardConfiguration(CacheCatalog catalog)
    {
        if (!catalog.Integrations.TryGetValue("Dashboard", out var values)) { return; }

        foreach (var pair in values)
        {
            switch (pair.Key.ToUpperInvariant())
            {
                case "ENABLED": this.Enabled = Boolean(pair.Value); break;
                case "READPOLICY": this.ReadPolicy = pair.Value; break;
                case "INVALIDATIONPOLICY": this.InvalidationPolicy = pair.Value; break;
                case "REVEALSENSITIVEMETADATA": this.RevealSensitiveMetadata = Boolean(pair.Value); break;
                default: throw new ArgumentException("Unknown Application Caching Dashboard setting: " + pair.Key);
            }
        }
    }

    /// <summary>Gets whether this host explicitly enabled the caching Dashboard capability.</summary>
    public bool Enabled { get; }

    /// <summary>Gets an optional read restriction in addition to the Dashboard host's access rules.</summary>
    public string ReadPolicy { get; }

    /// <summary>Gets the mutation policy override; null selects current host default authorization.</summary>
    public string InvalidationPolicy { get; }

    /// <summary>Gets whether this host explicitly reveals sensitive metadata after read authorization.</summary>
    public bool RevealSensitiveMetadata { get; }

    private static bool Boolean(string value) => bool.TryParse(value, out var parsed) ? parsed :
        throw new ArgumentException("An Application Caching Dashboard boolean setting is invalid.");
}

/// <summary>Records explicit Dashboard settings independently of Console, MCP, and external HTTP.</summary>
/// <example><code>cache.WithDashboard(options => options.WithInvalidationPolicy("Cache.Administer"));</code></example>
public sealed class CacheDashboardOptionsBuilder
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) { ["Enabled"] = "true" };

    /// <summary>Sets an optional additional read policy.</summary>
    public CacheDashboardOptionsBuilder WithReadPolicy(string policy) => this.Set("ReadPolicy", policy);

    /// <summary>Sets the host mutation authorization policy.</summary>
    public CacheDashboardOptionsBuilder WithInvalidationPolicy(string policy) => this.Set("InvalidationPolicy", policy);

    /// <summary>Explicitly enables authorized display of sensitive keys and dimensions.</summary>
    public CacheDashboardOptionsBuilder WithSensitiveMetadata(bool enabled = true) => this.Set("RevealSensitiveMetadata", enabled.ToString());

    /// <summary>Disables the feature pages without changing any other management integration.</summary>
    public CacheDashboardOptionsBuilder WithEnabled(bool enabled) => this.Set("Enabled", enabled.ToString());

    /// <summary>Gets a defensive snapshot of supplied settings.</summary>
    public IReadOnlyDictionary<string, string> Build() => new Dictionary<string, string>(this.values, StringComparer.OrdinalIgnoreCase);

    private CacheDashboardOptionsBuilder Set(string key, string value) { this.values[key] = value; return this; }
}