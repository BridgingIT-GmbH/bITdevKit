// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Publishes bounded source-owned boundary configuration without resolving handlers or enabling capture.</summary>
/// <example><code>var catalog = new AuditBoundaryRegistrationCatalog("Requester", "Dispatch", "Handler");</code></example>
public sealed class AuditBoundaryRegistrationCatalog : IAuditBoundaryRegistrationSource
{
    private readonly object gate = new();
    private readonly string feature;
    private ImmutableArray<AuditBoundaryRegistration> registrations;

    /// <summary>Declares a source's supported boundaries with omitted selection until explicitly configured.</summary>
    /// <example><code>var catalog = new AuditBoundaryRegistrationCatalog("Jobs", "Dispatch", "Execution");</code></example>
    public AuditBoundaryRegistrationCatalog(string feature, params string[] boundaries)
    {
        if (boundaries is null || boundaries.Length is < 1 or > 100) throw new ArgumentException("Audit boundary declarations must be bounded.");
        this.feature = feature;
        this.registrations = boundaries.Distinct(StringComparer.Ordinal).Select(boundary => new AuditBoundaryRegistration(feature, boundary, "omitted")).ToImmutableArray();
    }

    /// <summary>Gets the source feature owning these declarations.</summary>
    /// <example><code>var feature = catalog.Feature;</code></example>
    public string Feature => this.feature;

    /// <summary>Reuses a source catalog across additive registration calls without resolving application services.</summary>
    /// <example><code>var catalog = AuditBoundaryRegistrationCatalog.GetOrAdd(services, "Messaging", "Producer", "Consumer");</code></example>
    public static AuditBoundaryRegistrationCatalog GetOrAdd(IServiceCollection services, string feature, params string[] boundaries)
    {
        var catalog = services.Where(item => item.ServiceType == typeof(IAuditBoundaryRegistrationSource))
            .Select(item => item.ImplementationInstance).OfType<AuditBoundaryRegistrationCatalog>().FirstOrDefault(item => item.Feature == feature);
        if (catalog is not null) return catalog;
        catalog = new(feature, boundaries);
        services.AddSingleton<IAuditBoundaryRegistrationSource>(catalog);
        return catalog;
    }

    /// <summary>Declares a configured type or logical client. Additional declarations are capped at 100 and summarized as unknown.</summary>
    /// <example><code>catalog.Declare("Client", "CustomerDocuments", "selected");</code></example>
    public void Declare(string boundary, string typeAlias, string selection)
    {
        var item = new AuditBoundaryRegistration(this.feature, boundary, selection, typeAlias);
        lock (this.gate)
        {
            var index = this.registrations.IndexOf(this.registrations.FirstOrDefault(value => value.Identity == item.Identity));
            if (index >= 0) this.registrations = this.registrations.SetItem(index, item);
            else if (this.registrations.Length < 100) this.registrations = this.registrations.Add(item);
            else this.registrations = this.registrations.SetItem(99, new(this.feature, "AdditionalDeclarations", "unknown"));
        }
    }

    /// <summary>Marks opaque factories as unknown unless a known audit adapter has already been selected.</summary>
    /// <example><code>catalog.MarkUnknown("Consumer");</code></example>
    public void MarkUnknown(string boundary)
    {
        lock (this.gate)
        {
            if (this.registrations.Any(item => item.Boundary == boundary && item.Selection == "selected")) return;
            this.Select(boundary, "unknown");
        }
    }

    /// <summary>Returns an immutable snapshot of declared selections, rather than an assertion of runtime coverage.</summary>
    /// <example><code>var declarations = catalog.Registrations;</code></example>
    public IReadOnlyList<AuditBoundaryRegistration> Registrations { get { lock (this.gate) return this.registrations; } }

    /// <summary>Updates one declared boundary idempotently during source configuration.</summary>
    /// <example><code>catalog.Select("Dispatch");</code></example>
    public void Select(string boundary, string selection = "selected", string typeAlias = null)
    {
        lock (this.gate)
        {
            var index = this.registrations.IndexOf(this.registrations.FirstOrDefault(item => item.Boundary == boundary));
            if (index < 0) throw new ArgumentException("The source did not declare that audit boundary.");
            this.registrations = this.registrations.SetItem(index, new(this.feature, boundary, selection, typeAlias));
        }
    }
}
