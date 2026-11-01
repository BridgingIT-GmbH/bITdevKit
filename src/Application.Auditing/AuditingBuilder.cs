// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Owns audit policy/provider configuration, never a second source-feature integration list.</summary>
/// <example><code>var auditing = services.AddAuditing();</code></example>
public sealed class AuditingBuilder
{
    private Type selectedProvider;

    /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
    /// <example><code>var value = component.AuditingBuilder;</code></example>
    public AuditingBuilder(IServiceCollection services, AuditingOptions options) { this.Services = services; this.Options = options; }

    /// <summary>The host service collection, for compatible infrastructure/presentation extensions.</summary>
    /// <example><code>var services = builder.Services;</code></example>
    public IServiceCollection Services { get; }

    /// <summary>The validated runtime options snapshot.</summary>
    /// <example><code>var enabled = builder.Options.Enabled;</code></example>
    public AuditingOptions Options { get; }

    /// <summary>Registers an explicitly selected scoped metadata contributor idempotently.</summary>
    /// <example><code>services.AddAuditing().WithContributor&lt;ProcessAuditContributor&gt;();</code></example>
    public AuditingBuilder WithContributor<T>() where T : class, IAuditContributor
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuditContributor, T>());
        return this;
    }

    /// <summary>Registers an explicit typed projection, without enabling its source boundary.</summary>
    /// <example><code>services.AddAuditing().WithDetailsProvider&lt;Customer, CustomerAuditDetailsProvider&gt;();</code></example>
    public AuditingBuilder WithDetailsProvider<T, TProvider>() where TProvider : class, IAuditDetailsProvider<T>
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuditDetailsProvider<T>, TProvider>());
        return this;
    }

    /// <summary>Registers an explicit typed detail in the approved query field catalog.</summary>
    /// <example><code>builder.WithSearchableDetail(new() { Name = "tenantId" });</code></example>
    public AuditingBuilder WithSearchableDetail(AuditDetailField detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        _ = new AuditFieldCatalog([detail]);
        if (this.Services.Any(d => d.ServiceType == typeof(AuditDetailField) && d.ImplementationInstance is AuditDetailField existing && existing.Name.Equals(detail.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An audit detail field is already registered.");
        this.Services.AddSingleton(detail);
        return this;
    }

    /// <summary>Selects a host read-scope resolver; its failure always closes access before provider reads.</summary>
    /// <example><code>builder.WithReadScope((services, ct) => Task.FromResult(Result&lt;AuditReadScope&gt;.Success(AuditReadScope.Unrestricted())));</code></example>
    public AuditingBuilder WithReadScope(Func<IServiceProvider, CancellationToken, Task<Result<AuditReadScope>>> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        if (this.Services.Any(d => d.ServiceType == typeof(AuditReadScopeResolver))) throw new InvalidOperationException("An audit read scope is already configured.");
        this.Services.AddSingleton(new AuditReadScopeResolver(resolve));
        return this;
    }

    /// <summary>Configures finite event-range and query execution limits.</summary>
    /// <example><code>builder.WithQueries(new() { MaxRange = TimeSpan.FromDays(7) });</code></example>
    public AuditingBuilder WithQueries(AuditQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.Services.Replace(ServiceDescriptor.Singleton(options));
        return this;
    }

    /// <summary>Sets current response restrictions without changing historical source documents.</summary>
    /// <example><code>builder.WithExposure(new() { Revision = "v2", ExposeChangeValues = true });</code></example>
    public AuditingBuilder WithExposure(AuditExposureOptions exposure)
    {
        ArgumentNullException.ThrowIfNull(exposure);
        _ = AuditStructuralValidation.Required(exposure.Revision, 128, nameof(exposure.Revision));
        this.Services.Replace(ServiceDescriptor.Singleton(exposure));
        return this;
    }

    /// <summary>Replaces the implicit memory provider with one explicit singleton provider choice.</summary>
    /// <example><code>builder.WithStorageProvider&lt;CustomAuditStorageProvider&gt;();</code></example>
    public AuditingBuilder WithStorageProvider<TProvider>() where TProvider : class, IAuditingStorageProvider => this.WithStorageProvider<TProvider>(sp => ActivatorUtilities.CreateInstance<TProvider>(sp));

    /// <summary>Registers one explicit provider factory; incompatible second choices fail before host startup.</summary>
    /// <example><code>builder.WithStorageProvider(sp => new CustomAuditStorageProvider());</code></example>
    public AuditingBuilder WithStorageProvider<TProvider>(Func<IServiceProvider, TProvider> create) where TProvider : class, IAuditingStorageProvider
    {
        ArgumentNullException.ThrowIfNull(create);
        if (this.selectedProvider is not null && this.selectedProvider != typeof(TProvider)) throw new InvalidOperationException("An audit storage provider is already selected.");
        if (this.selectedProvider == typeof(TProvider)) return this;
        this.selectedProvider = typeof(TProvider);
        this.Services.RemoveAll<IAuditingStorageProvider>();
        this.Services.AddSingleton<IAuditingStorageProvider>(sp => create(sp));
        return this;
    }

    /// <summary>Configures shared age thresholds and opts into a bounded maintenance worker.</summary>
    /// <example><code>builder.WithRetention(new() { ArchiveAfter = TimeSpan.FromDays(30), RemoveAfter = TimeSpan.FromDays(365) });</code></example>
    public AuditingBuilder WithRetention(AuditRetentionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.Services.Replace(ServiceDescriptor.Singleton(options));
        this.Services.AddHostedService<AuditRetentionWorker>();
        return this;
    }
}
