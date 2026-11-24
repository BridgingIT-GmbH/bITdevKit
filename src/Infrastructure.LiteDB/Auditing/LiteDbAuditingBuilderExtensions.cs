// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.Hosting;

/// <summary>Selects independent native auditing persistence without changing capture or read authorization.</summary>
public static class LiteDbAuditingBuilderExtensions
{
    /// <summary>Registers complete original-document persistence, scoped queries, extraction, and retention.</summary>
    /// <param name="builder">The existing auditing builder.</param>
    /// <param name="configure">Optional named database, collection prefix, and query bounds.</param>
    /// <returns>The same auditing builder.</returns>
    /// <example><code>services.AddAuditing().WithLiteDbProvider();</code></example>
    public static AuditingBuilder WithLiteDbProvider(this AuditingBuilder builder, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = builder.Services.RegisterLiteDbFeature("auditing", ["records", "state"], selected =>
        {
            selected.DatabaseName = "auditing";
            configure?.Invoke(selected);
        });
        if (options.MaxMaterializedRows < 2) { throw new ArgumentException("Auditing requires room for a record and its control state."); }

        var filename = LiteDbDatabaseRegistry.GetOrAdd(builder.Services).Get(options.DatabaseName).Connection.Filename;
        builder.Services.AddSingleton(sp => new LiteDbAuditingStorageProvider(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), options),
            builder.Options, sp.GetRequiredService<AuditFieldCatalog>(), sp.GetRequiredService<AuditIntegrityReadPolicy>(),
            sp.GetRequiredService<AuditRetentionOptions>(), sp.GetRequiredService<TimeProvider>(), filename));
        builder.WithStorageProvider(sp => sp.GetRequiredService<LiteDbAuditingStorageProvider>());
        builder.Services.AddHostedService<LiteDbAuditingInitializationService>();
        return builder;
    }
}
