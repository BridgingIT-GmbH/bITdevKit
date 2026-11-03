// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Collects the declarations of a host that opts in to linked-worktree development isolation.
/// </summary>
/// <remarks>
/// The builder is pure data. Registration evaluates eligibility first, so an inactive host never runs adapter code.
/// </remarks>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithHttpOffset(http: 10, https: 11));
/// </code>
/// </example>
public sealed class DevAppInstanceBuilder
{
    private readonly List<DevAppInstanceResourceDeclaration> resources = [];
    private readonly List<IDevAppInstanceDatabaseAdapter> databaseAdapters = [];
    private readonly HashSet<string> sharedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> authorityKeys = [];
    private readonly HashSet<int> reservedPorts = [];

    /// <summary>Gets a value indicating whether the host takes part in linked-worktree isolation. Defaults to <c>true</c>.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Gets the declared application.</summary>
    public DevAppInstanceApplication Application { get; private set; }

    /// <summary>Gets the declared port policy.</summary>
    public DevAppInstancePortPolicy PortPolicy { get; private set; } = DevAppInstancePortPolicy.Default;

    /// <summary>Gets the additional reserved ports.</summary>
    public IReadOnlyCollection<int> ReservedPorts => this.reservedPorts;

    /// <summary>Gets the additional authority configuration keys.</summary>
    public IReadOnlyList<string> AuthorityKeys => this.authorityKeys;

    /// <summary>Gets the resource declarations.</summary>
    public IReadOnlyList<DevAppInstanceResourceDeclaration> Resources => this.resources;

    /// <summary>Gets the registered database adapters in registration order.</summary>
    public IReadOnlyList<IDevAppInstanceDatabaseAdapter> DatabaseAdapters => this.databaseAdapters;

    /// <summary>Gets the shared connection-string keys.</summary>
    public IReadOnlyCollection<string> SharedKeys => this.sharedKeys;

    /// <summary>Gets every distinct adapter of the declared resources and database adapters.</summary>
    public IReadOnlyList<IDevAppInstanceResourceAdapter> Adapters =>
        this.resources.Select(r => r.Adapter).Concat(this.databaseAdapters).DistinctBy(a => a.Kind).ToList();

    /// <summary>
    /// Turns isolation on or off for this host. Isolation is on by default for DevKit web hosts in the Development
    /// environment, so <c>Enabled(false)</c> keeps the configured ports and databases in a linked worktree too.
    /// </summary>
    /// <param name="enabled"><c>false</c> to keep the host out of linked-worktree isolation.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// builder.AddDevAppInstance(o => o.Enabled(false));
    /// </code>
    /// </example>
    public DevAppInstanceBuilder Enabled(bool enabled = true)
    {
        this.IsEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Declares the application ID. Optional. Without it, registration derives the ID from the host application name,
    /// for example <c>Contoso.ForecastHub.Presentation.Web.Server</c> becomes <c>forecasthub</c>.
    /// </summary>
    /// <param name="appId">The stable application ID, for example <c>forecasthub</c>.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// o.WithAppId("forecasthub");
    /// </code>
    /// </example>
    public DevAppInstanceBuilder WithAppId(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        this.Application = this.CurrentApplication with { AppId = appId, Source = null };

        return this;
    }

    /// <summary>
    /// Declares the HTTP and HTTPS port offsets within a slot block. Optional. Without them, the registry assigns the
    /// lowest free offset pair of the repository on the first start and reuses it afterwards, so a single host gets
    /// <c>0</c> and <c>1</c>. Declare offsets when several hosts share a repository and each needs the same ports in
    /// every clone.
    /// </summary>
    /// <param name="http">The HTTP port offset.</param>
    /// <param name="https">The HTTPS port offset, or <c>null</c> for <paramref name="http"/> + 1.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// o.WithHttpOffset(http: 10, https: 11);
    /// </code>
    /// </example>
    public DevAppInstanceBuilder WithHttpOffset(int http, int? https = null)
    {
        this.Application = this.CurrentApplication with { HttpOffset = http, HttpsOffset = https ?? http + 1, HasAutomaticOffsets = false };

        return this;
    }

    /// <summary>
    /// Declares a repository-wide port policy. Every host of the repository must declare the same policy.
    /// </summary>
    /// <param name="basePort">The base port.</param>
    /// <param name="blockSize">The port block size per slot.</param>
    /// <param name="slotCount">The number of slots.</param>
    /// <returns>The builder.</returns>
    public DevAppInstanceBuilder WithPortPolicy(int basePort, int blockSize, int slotCount)
    {
        this.PortPolicy = new DevAppInstancePortPolicy(basePort, blockSize, slotCount);

        return this;
    }

    /// <summary>
    /// Adds ports that slot allocation skips in addition to the default denylist.
    /// </summary>
    /// <param name="ports">The ports.</param>
    /// <returns>The builder.</returns>
    public DevAppInstanceBuilder WithReservedPorts(params int[] ports)
    {
        this.reservedPorts.UnionWith(ports ?? []);

        return this;
    }

    /// <summary>
    /// Declares an additional configuration key that holds the application's own authority or issuer.
    /// <c>Authentication:Authority</c> is always managed.
    /// </summary>
    /// <param name="configurationKey">The configuration key.</param>
    /// <returns>The builder.</returns>
    public DevAppInstanceBuilder WithAuthorityKey(string configurationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);
        this.authorityKeys.Add(configurationKey);

        return this;
    }

    /// <summary>
    /// Declares a resource handled by a custom adapter.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The builder.</returns>
    public DevAppInstanceBuilder WithResource(DevAppInstanceResourceDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(declaration.Adapter);
        if (declaration.SourceKeys is null || declaration.SourceKeys.Count == 0)
        {
            throw new ArgumentException("A resource declaration needs at least one configuration key.", nameof(declaration));
        }

        if (this.resources.Any(r => string.Equals(r.ResourceId, declaration.ResourceId, StringComparison.OrdinalIgnoreCase) && r.Adapter.Kind == declaration.Adapter.Kind))
        {
            throw new ArgumentException($"Resource '{declaration.ResourceId}' of kind '{declaration.Adapter.Kind}' is declared twice.", nameof(declaration));
        }

        this.resources.Add(declaration);

        return this;
    }

    /// <summary>
    /// Declares a connection-string key that stays unchanged, so every checkout uses the same database.
    /// Database adapters skip the key during discovery.
    /// </summary>
    /// <param name="configurationKey">The configuration key.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// o.ShareConnectionString("ConnectionStrings:Reporting");
    /// </code>
    /// </example>
    public DevAppInstanceBuilder ShareConnectionString(string configurationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);
        this.sharedKeys.Add(configurationKey);

        return this;
    }

    /// <summary>
    /// Registers a database adapter that finds its connection strings in configuration and gives each linked worktree
    /// its own database <c>&lt;database&gt;_wNN</c>. Adapters of the DevKit EF Core provider packages, and adapters
    /// declared with <see cref="DevAppInstanceDatabaseAdapterAttribute"/>, are found automatically. Declare an adapter
    /// here when it is not found automatically, or to run it before the automatically found adapters.
    /// </summary>
    /// <param name="adapter">The database adapter.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// o.WithDatabases(myAdapter);
    /// </code>
    /// </example>
    public DevAppInstanceBuilder WithDatabases(IDevAppInstanceDatabaseAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        if (this.databaseAdapters.Any(a => a.Kind == adapter.Kind))
        {
            throw new ArgumentException($"A database adapter of kind '{adapter.Kind}' is registered twice.", nameof(adapter));
        }

        this.databaseAdapters.Add(adapter);

        return this;
    }

    /// <summary>
    /// Completes the application declaration. A missing ID is derived from <paramref name="applicationName"/>, and a
    /// missing declaration becomes a derived ID with automatic offsets.
    /// </summary>
    /// <param name="applicationName">The host application name, usually the entry assembly name.</param>
    /// <exception cref="InvalidOperationException">Thrown when no ID is declared and no application name is known.</exception>
    /// <example>
    /// <code>
    /// declaration.EnsureValid(builder.Environment.ApplicationName);
    /// </code>
    /// </example>
    public void EnsureValid(string applicationName = null)
    {
        this.Application = this.CurrentApplication;
        if (this.Application.AppId is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new InvalidOperationException("AddDevAppInstance cannot derive an application ID. Declare it with WithAppId(appId).");
        }

        this.Application = this.Application with
        {
            AppId = DevAppInstanceApplication.DeriveAppId(applicationName),
            Source = applicationName,
        };
    }

    private DevAppInstanceApplication CurrentApplication =>
        this.Application ?? new DevAppInstanceApplication(null) { HasAutomaticOffsets = true };
}
