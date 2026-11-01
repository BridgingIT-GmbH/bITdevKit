// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Explicit selection of read-only audit routes and console investigation commands.</summary>
/// <example><code>services.AddAuditing().WithEndpoints();</code></example>
public static class AuditingWebServiceCollectionExtensions
{
    /// <summary>Registers the read-only auditing console group without enabling routes, a dashboard, or capture adapters.</summary>
    /// <example><code>services.AddAuditing().AddConsoleCommands();</code></example>
    public static AuditingBuilder AddConsoleCommands(this AuditingBuilder builder, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!enabled) return builder;
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, AuditListConsoleCommand>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, AuditShowConsoleCommand>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, AuditRelatedConsoleCommand>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, AuditHistoryConsoleCommand>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, AuditStatsConsoleCommand>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, AuditDiagnosticsConsoleCommand>());
        return builder;
    }

    /// <summary>Registers public routes under host access configuration without enabling source capture.</summary>
    /// <example><code>services.AddAuditing().WithEndpoints(options => options.RequirePolicy = "AuditReview");</code></example>
    public static AuditingBuilder WithEndpoints(this AuditingBuilder builder, Action<AuditingEndpointsOptions> configure = null)
    {
        if (builder.Services.Any(item => item.ServiceType == typeof(AuditingEndpointsOptions)))
        {
            if (configure is not null) throw new InvalidOperationException("Audit endpoints are already configured.");
            return builder;
        }

        var options = new AuditingEndpointsOptions(); configure?.Invoke(options);
        builder.Services.AddSingleton(options); builder.Services.AddEndpoints<AuditingEndpoints>(); builder.Services.AddNdjsonStreaming();
        return builder;
    }
}
