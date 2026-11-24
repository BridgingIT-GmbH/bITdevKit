// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Resolves external web settings from the immutable presence-aware catalog.</summary>
/// <example><code>var exposed = configuration.IsExposed("cards");</code></example>
public sealed class CacheWebConfiguration
{
    private readonly CacheCatalog catalog;

    /// <summary>Applies only registered integration fields and rejects invalid or unsupported HTTP options before native access.</summary>
    public CacheWebConfiguration(CacheCatalog catalog)
    {
        this.catalog = catalog;
        this.Options = new();
        if (catalog.Integrations.TryGetValue("Endpoints", out var values))
        {
            foreach (var pair in values)
            {
                switch (pair.Key.ToUpperInvariant())
                {
                    case "ENABLED": this.Options.Enabled = ParseBoolean(pair.Value); break;
                    case "GROUPPATH": this.Options.GroupPath = pair.Value; break;
                    case "READPOLICY": this.Options.ReadPolicy = pair.Value; break;
                    case "WRITEPOLICY": this.Options.WritePolicy = pair.Value; break;
                    case "INVALIDATIONPOLICY": this.Options.InvalidationPolicy = pair.Value; break;
                    case "CACHEPOLICY":
                        if (!Enum.TryParse<CacheHttpCachePolicy>(pair.Value, true, out var policy) || !Enum.IsDefined(policy)) { throw new ArgumentException("Unknown cache HTTP policy."); }

                        this.Options.CachePolicy = policy;
                        break;
                    case "MAXIMUMMETADATABODYBYTES":
                        this.Options.MaximumMetadataBodyBytes = int.Parse(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "REVEALSENSITIVEMETADATA": this.Options.RevealSensitiveMetadata = ParseBoolean(pair.Value); break;
                    default: throw new ArgumentException("Unknown cache endpoint setting: " + pair.Key);
                }
            }
        }
        else
        {
            this.Options.Enabled = false;
        }

        if (string.IsNullOrWhiteSpace(this.Options.GroupPath) || !this.Options.GroupPath.StartsWith('/') ||
            this.Options.GroupPath.Contains('{') || this.Options.MaximumMetadataBodyBytes is < 1 or > 1024 * 1024)
        {
            throw new ArgumentException("The external cache route or structured-body bound is invalid.");
        }

        foreach (var binding in catalog.Collections.Values)
        {
            if (!binding.Options.Extensions.TryGetValue("Endpoints", out var settings)) { continue; }

            foreach (var setting in settings)
            {
                if (!setting.Key.Equals("Enabled", StringComparison.OrdinalIgnoreCase)) { throw new ArgumentException("Unknown collection HTTP exposure setting."); }

                _ = ParseBoolean(setting.Value);
            }
        }
    }

    /// <summary>Gets the finalized external API settings.</summary>
    public CacheEndpointsOptions Options { get; }

    /// <summary>Tests declared collection exposure without initializing a provider or opening a payload.</summary>
    public bool IsExposed(string collection) => this.catalog.Collections.TryGetValue(collection, out var binding) &&
        (!binding.Options.Extensions.TryGetValue("Endpoints", out var settings) || !settings.TryGetValue("Enabled", out var enabled) || ParseBoolean(enabled));

    /// <summary>Validates every explicit target atomically before any request body or provider access.</summary>
    public BridgingIT.DevKit.Common.Result ValidateTargets(IEnumerable<string> targets)
    {
        var names = targets?.Take(33).ToArray() ?? [];
        return names.Length is < 1 or > 32 || names.Any(name => string.IsNullOrWhiteSpace(name) || !this.IsExposed(name)) ?
            BridgingIT.DevKit.Common.Result.Failure(new CacheError(CacheErrorCode.Validation, "Every explicit target must be a declared HTTP-exposed cache collection.")) :
            BridgingIT.DevKit.Common.Result.Success();
    }

    private static bool ParseBoolean(string value) => bool.TryParse(value, out var parsed) ? parsed :
        throw new ArgumentException("A cache endpoint boolean setting is invalid.");
}