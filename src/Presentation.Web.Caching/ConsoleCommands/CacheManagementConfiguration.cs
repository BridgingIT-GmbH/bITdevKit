// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Validates independent Console/MCP opt-ins and per-collection exposure without accessing providers or source loaders.</summary>
/// <param name="catalog">The immutable startup cache catalog.</param>
/// <example><code>var exposed = configuration.IsEnabled("Mcp", "cards");</code></example>
public sealed class CacheManagementConfiguration(CacheCatalog catalog)
{
    private readonly CacheCatalog catalog = Validate(catalog);

    /// <summary>Gets whether Console discovery was explicitly enabled and the cache feature remains enabled.</summary>
    public bool ConsoleEnabled => this.IsEnabled("ConsoleCommands");

    /// <summary>Gets whether MCP discovery was explicitly enabled and the cache feature remains enabled.</summary>
    public bool McpEnabled => this.IsEnabled("Mcp");

    /// <summary>Checks only the selected transport's global and collection exposure settings.</summary>
    /// <param name="transport">ConsoleCommands or Mcp.</param>
    /// <param name="collection">An optional declared collection; no target means global discovery.</param>
    /// <returns>True when that independent capability permits the selected scope.</returns>
    public bool IsEnabled(string transport, string collection = null)
    {
        if (!this.catalog.Options.Enabled || transport is not ("ConsoleCommands" or "Mcp") ||
            !this.catalog.Integrations.TryGetValue(transport, out var values) || !values.TryGetValue("Enabled", out var enabled) || !bool.Parse(enabled)) { return false; }

        return collection is null || this.catalog.Collections.TryGetValue(collection, out var binding) &&
            (!binding.Options.Extensions.TryGetValue(transport, out var settings) || !settings.TryGetValue("Enabled", out var exposed) || bool.Parse(exposed));
    }

    private static CacheCatalog Validate(CacheCatalog catalog)
    {
        foreach (var transport in new[] { "ConsoleCommands", "Mcp" })
        {
            if (catalog.Integrations.TryGetValue(transport, out var global)) { Check(global); }

            foreach (var binding in catalog.Collections.Values)
            {
                if (binding.Options.Extensions.TryGetValue(transport, out var values)) { Check(values); }
            }
        }

        return catalog;

        static void Check(IReadOnlyDictionary<string, string> settings)
        {
            if (settings.Any(value => !value.Key.Equals("Enabled", StringComparison.OrdinalIgnoreCase) || !bool.TryParse(value.Value, out _)))
            {
                throw new ArgumentException("Console/MCP cache settings support only an explicit boolean Enabled value.");
            }
        }
    }
}