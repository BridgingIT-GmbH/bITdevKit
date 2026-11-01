// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Configures HTTP-independent bounded exports and explicit optional formatter profiles.</summary>
/// <example><code>services.AddAuditing().WithExports(options => options with { RecordLimit = 500 });</code></example>
public static class AuditingExportBuilderExtensions
{
    /// <summary>Copies and validates export budgets before service resolution.</summary>
    /// <example><code>auditing.WithExports(options => options with { MaxDuration = TimeSpan.FromSeconds(20) });</code></example>
    public static AuditingBuilder WithExports(this AuditingBuilder builder, Func<AuditExportOptions, AuditExportOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var current = builder.Services.FirstOrDefault(item => item.ServiceType == typeof(AuditExportOptions))?.ImplementationInstance as AuditExportOptions ?? new();
        var next = configure(current) ?? throw new ArgumentException("Audit export options are required.");
        next.Validate();
        builder.Services.Replace(ServiceDescriptor.Singleton(next));
        return builder;
    }

    /// <summary>Registers an optional formatter; duplicate format identifiers fail validation rather than silently replacing output.</summary>
    /// <example><code>auditing.WithExportFormatter&lt;CustomerExportFormatter&gt;();</code></example>
    public static AuditingBuilder WithExportFormatter<TFormatter>(this AuditingBuilder builder) where TFormatter : class, IAuditExportFormatter
    {
        builder.Services.AddSingleton<IAuditExportFormatter, TFormatter>();
        return builder;
    }

    /// <summary>Registers host-supplied immutable formatter configuration without adding source integrations.</summary>
    /// <example><code>auditing.WithExportFormatter(services => new CustomerExportFormatter(settings));</code></example>
    public static AuditingBuilder WithExportFormatter<TFormatter>(this AuditingBuilder builder, Func<IServiceProvider, TFormatter> factory) where TFormatter : class, IAuditExportFormatter
    {
        builder.Services.AddSingleton<IAuditExportFormatter>(services => factory(services));
        return builder;
    }
}
