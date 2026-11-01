// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;
using Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

/// <summary>Selects independent audit-DbSet persistence for all three supported EF relational backends.</summary>
/// <example><code>services.AddAuditing().WithEntityFrameworkProvider&lt;CustomerDbContext&gt;();</code></example>
public static class AuditingEntityFrameworkServiceCollectionExtensions
{
    /// <summary>Replaces implicit memory storage with the explicitly configured EF provider and read-only startup check.</summary>
    /// <remarks>The host supplies DbContext options and deploys reviewed migrations, including explicit control-state initialization.</remarks>
    /// <example><code>auditing.WithEntityFrameworkProvider&lt;Store&gt;(options => options.Inline());</code></example>
    public static AuditingBuilder WithEntityFrameworkProvider<TContext>(this AuditingBuilder builder, Action<AuditEntityFrameworkOptionsBuilder> configure = null)
        where TContext : DbContext, IAuditingDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(EntityFrameworkAuditStorageProvider<TContext>)))
        {
            if (configure is not null) throw new InvalidOperationException("Audit EF storage is already configured.");
            return builder;
        }

        var options = new AuditEntityFrameworkOptionsBuilder();
        configure?.Invoke(options);
        var settings = options.Build();
        builder.WithStorageProvider<EntityFrameworkAuditStorageProvider<TContext>>(services => new(services.GetRequiredService<IServiceScopeFactory>(), settings,
            services.GetRequiredService<AuditingOptions>(), services.GetRequiredService<AuditFieldCatalog>(), services.GetRequiredService<AuditIntegrityReadPolicy>(),
            services.GetRequiredService<AuditRetentionOptions>(), services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(services => (EntityFrameworkAuditStorageProvider<TContext>)services.GetRequiredService<IAuditingStorageProvider>());
        builder.Services.AddHostedService<AuditEntityFrameworkReadinessService<TContext>>();
        builder.Services.AddHostedService<AuditWriteBackgroundService<TContext>>();
        return builder;
    }
}
