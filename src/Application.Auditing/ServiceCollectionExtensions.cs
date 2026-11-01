// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Auditing;
using Extensions;

/// <summary>Registers auditing services and an optional capture-free injection fallback.</summary>
/// <example><code>services.AddNoOpAuditContext();</code></example>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>Registers bounded capture with memory storage and no implicit source behaviors.</summary>
    /// <example><code>services.AddAuditing(o => o.Application("CustomerPortal"));</code></example>
    public static AuditingBuilder AddAuditing(this IServiceCollection services, Action<AuditingOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.FirstOrDefault(d => d.ServiceType == typeof(AuditingBuilder))?.ImplementationInstance is AuditingBuilder existing)
        {
            if (configure is not null) throw new InvalidOperationException("Auditing is already configured.");
            return existing;
        }

        var optionsBuilder = new AuditingOptionsBuilder();
        configure?.Invoke(optionsBuilder);
        var options = optionsBuilder.Build();
        var builder = new AuditingBuilder(services, options);
        services.AddSingleton(builder);
        services.AddSingleton(options);
        if (options.Enabled) services.TryAddAuditingHealthCheck();
        services.TryAddSingleton(TimeProvider.System);
        services.AddDataProtection();
        services.TryAddSingleton<IAuditExecutionLinkProtector, AuditExecutionLinkProtector>();
        services.TryAddSingleton<IAuditingStorageProvider, InMemoryAuditStorageProvider>();
        services.TryAddSingleton<AuditRecordReaderRegistry>();
        services.TryAddSingleton<AuditFieldCatalog>();
        services.TryAddSingleton(new AuditQueryOptions());
        services.TryAddSingleton<AuditSearchPlanBuilder>();
        services.TryAddSingleton(new AuditExposureOptions());
        services.TryAddSingleton<AuditReadProjection>();
        services.TryAddSingleton<AuditIntegrityReadPolicy>();
        services.TryAddScoped<IAuditQueryService, AuditQueryService>();
        services.TryAddSingleton(new AuditExportOptions());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuditExportFormatter, NdjsonAuditExportFormatter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuditExportFormatter, CsvAuditExportFormatter>());
        services.TryAddSingleton<AuditExportRegistry>();
        services.TryAddSingleton<AuditExportLimiter>();
        services.TryAddScoped<IAuditExportService, AuditExportService>();
        services.AddHostedService<AuditExportValidationService>();
        services.TryAddSingleton(new AuditRetentionOptions());
        services.TryAddScoped<AuditRetentionCoordinator>();
        services.TryAddSingleton(sp => new AuditRuntime(sp.GetRequiredService<AuditingOptions>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IAuditingStorageProvider>(), sp.GetServices<IAuditRecordSealer>()));
        foreach (var fallback in services.Where(d => d.ServiceType == typeof(IAuditContext) &&
            (d.ImplementationType == typeof(NoOpAuditContext) || d.ImplementationInstance is NoOpAuditContext)).ToArray()) services.Remove(fallback);
        services.TryAddScoped<IAuditContext>(sp => new AuditContext(sp, sp.GetRequiredService<AuditRuntime>()));
        services.AddHostedService<AuditRuntimeLifecycleService>();
        services.TryAddScoped<AuditDiagnosticsService>();
        services.TryAddScoped<IAuditExecutionBoundary>(sp => sp.GetRequiredService<IAuditContext>() as IAuditExecutionBoundary ?? NoOpBoundary.Instance);
        return builder;
    }

    /// <summary>Supplies the built-in no-op only when no host context is registered.</summary>
    /// <remarks>This registers no provider, writer, HTTP endpoint, or source behavior.</remarks>
    /// <example><code>services.AddNoOpAuditContext();</code></example>
    public static IServiceCollection AddNoOpAuditContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAuditContext, NoOpAuditContext>();
        return services;
    }

    private sealed class NoOpBoundary : IAuditExecutionBoundary
    {
        /// <summary>Returns the configured new.</summary>
        /// <example><code>var value = component.Instance;</code></example>
        public static NoOpBoundary Instance { get; } = new();

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>using var boundary = runtime.DetachExecution();</code></example>
        public IDisposable DetachExecution() => NoOpAuditScope.Instance;
    }
}
