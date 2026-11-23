// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;

/// <summary>Declares isolated native durable-feature collections before the database mapper freezes.</summary>
public static class LiteDbDurableServiceCollectionExtensions
{
    /// <summary>Validates and snapshots a feature's namespace, then reserves its collection names.</summary>
    /// <param name="services">The service collection containing the named database.</param>
    /// <param name="defaultPrefix">The feature's default collection prefix.</param>
    /// <param name="suffixes">All collections owned by the feature.</param>
    /// <param name="configure">Optional settings frozen before the provider is resolved.</param>
    /// <returns>An independent configuration snapshot.</returns>
    public static LiteDbFeatureOptions RegisterLiteDbFeature(this IServiceCollection services, string defaultPrefix, string[] suffixes,
        Action<LiteDbFeatureOptions> configure = null)
    {
        var configured = new LiteDbFeatureOptions { CollectionPrefix = defaultPrefix };
        configure?.Invoke(configured);
        ArgumentException.ThrowIfNullOrWhiteSpace(configured.DatabaseName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configured.MaxMaterializedRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(configured.MaxMaterializedRows, int.MaxValue);
        var registration = LiteDbDatabaseRegistry.GetOrAdd(services).Get(configured.DatabaseName);
        foreach (var suffix in suffixes)
        {
            var name = configured.CollectionPrefix + "_" + suffix;
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,59}$", RegexOptions.CultureInvariant)) { throw new ArgumentException("The durable feature collection name is invalid."); }

            registration.AddEntity(typeof(LiteDbFeatureDatabase), name, _ => { }, "Durable:" + defaultPrefix + ":" + suffix);
        }

        return new LiteDbFeatureOptions
        {
            DatabaseName = configured.DatabaseName, CollectionPrefix = configured.CollectionPrefix,
            MaxMaterializedRows = configured.MaxMaterializedRows, Serializer = configured.Serializer ?? new SystemTextJsonSerializer()
        };
    }
}
