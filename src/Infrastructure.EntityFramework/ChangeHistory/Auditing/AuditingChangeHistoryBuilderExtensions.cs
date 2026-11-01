// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

/// <summary>Opts one existing ChangeHistory source context into copied audit evidence.</summary>
/// <example><code>services.AddChangeHistory().WithAuditing&lt;CustomerDbContext&gt;();</code></example>
public static class AuditingChangeHistoryBuilderExtensions
{
    /// <summary>Installs singleton stateless interceptors with context-local bounded weak observation state.</summary>
    /// <remarks>Source capture, source migrations, audit registration, and audit storage remain independent host choices.</remarks>
    /// <example><code>history.WithAuditing&lt;CustomerDbContext&gt;(options => options with { SourceStoreKey = "customers" });</code></example>
    public static ChangeHistoryBuilderContext WithAuditing<TContext>(this ChangeHistoryBuilderContext builder, Func<AuditChangeHistoryOptions, AuditChangeHistoryOptions> configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(AuditChangeHistoryRegistration<TContext>)))
        {
            if (configure is not null) throw new InvalidOperationException("This ChangeHistory audit source is already configured.");
            return builder;
        }

        var options = new AuditChangeHistoryOptions { SourceStoreKey = typeof(TContext).FullName ?? typeof(TContext).Name };
        if (configure is not null) options = configure(options) ?? throw new ArgumentException("ChangeHistory audit source options are required.");
        options.Validate();
        builder.Services.AddSingleton(new AuditChangeHistoryRegistration<TContext>(options));
        builder.Services.AddSingleton<ChangeHistoryAuditBridge<TContext>>();
        builder.Services.AddSingleton<ChangeHistoryAuditSaveInterceptor<TContext>>();
        builder.Services.AddSingleton<AuditTransactionObserver<TContext>>();
        builder.Services.ConfigureDbContext<TContext>((services, context) => context.AddInterceptors(
            services.GetRequiredService<ChangeHistoryAuditSaveInterceptor<TContext>>(), services.GetRequiredService<AuditTransactionObserver<TContext>>()));
        AuditBoundaryRegistrationCatalog.GetOrAdd(builder.Services, "ChangeHistory", "Bridge").Declare("Bridge", typeof(TContext).Name, "selected");
        return builder;
    }
}
