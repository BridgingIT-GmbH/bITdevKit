// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.RegularExpressions;

/// <summary>
/// Defines the configured development instance mode.
/// </summary>
public enum DevAppInstanceMode
{
    /// <summary>Select linked-worktree isolation automatically for eligible local development.</summary>
    Auto = 0,

    /// <summary>Never activate isolation.</summary>
    Disabled = 1,
}

/// <summary>
/// Describes how an active instance obtained its slot.
/// </summary>
public enum DevAppInstanceSelectionReason
{
    /// <summary>No slot was selected because isolation is inactive.</summary>
    None = 0,

    /// <summary>A new slot was allocated.</summary>
    New = 1,

    /// <summary>The existing assignment was reused.</summary>
    Existing = 2,

    /// <summary>The registry assignment was recovered after the local file was missing.</summary>
    Recovered = 3,
}

/// <summary>
/// Defines the inactive reasons reported by an inactive development instance context.
/// </summary>
/// <example>
/// <code>
/// if (context.InactiveReason == DevAppInstanceInactiveReasons.OrdinaryCheckout) { }
/// </code>
/// </example>
public static class DevAppInstanceInactiveReasons
{
    /// <summary>The feature is not registered.</summary>
    public const string NotRegistered = "not_registered";

    /// <summary><c>DevAppInstance:Mode</c> is <c>Disabled</c>.</summary>
    public const string Disabled = "disabled";

    /// <summary>The process is build-time OpenAPI generation.</summary>
    public const string BuildTime = "build_time";

    /// <summary>The host environment is not <c>Development</c>.</summary>
    public const string NonDevelopment = "non_development";

    /// <summary>The process runs in a container.</summary>
    public const string Container = "container";

    /// <summary>The process runs in a cloud host.</summary>
    public const string Cloud = "cloud";

    /// <summary>The content root is in an ordinary checkout.</summary>
    public const string OrdinaryCheckout = "ordinary_checkout";

    /// <summary>The content root is outside Git.</summary>
    public const string OutsideGit = "outside_git";

    /// <summary>Isolation failed in a linked worktree and the host started with its configured defaults.</summary>
    public const string Fallback = "fallback";
}

/// <summary>
/// Describes one application entry of the development instance catalog.
/// </summary>
/// <example>
/// <code>
/// var application = new DevAppInstanceApplication("forecasthub", 10, 11);
/// application.Validate(DevAppInstancePortPolicy.Default);
///
/// var derived = DevAppInstanceApplication.FromApplicationName("Contoso.ForecastHub.Presentation.Web.Server");
/// // derived.AppId == "forecasthub", offsets are assigned by the registry
/// </code>
/// </example>
public sealed partial record DevAppInstanceApplication
{
    /// <summary>The maximum length of an application ID.</summary>
    public const int MaxAppIdLength = 63;

    private static readonly HashSet<string> LayerSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Server", "Web", "Presentation", "Api", "WebApi", "Host", "Service",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DevAppInstanceApplication"/> record.
    /// </summary>
    /// <param name="appId">The stable application ID.</param>
    /// <param name="httpOffset">The HTTP port offset within a slot block.</param>
    /// <param name="httpsOffset">The HTTPS port offset within a slot block.</param>
    public DevAppInstanceApplication(string appId, int httpOffset = 0, int httpsOffset = 1)
    {
        this.AppId = appId;
        this.HttpOffset = httpOffset;
        this.HttpsOffset = httpsOffset;
    }

    /// <summary>Gets the stable application ID.</summary>
    public string AppId { get; init; }

    /// <summary>Gets the HTTP port offset.</summary>
    public int HttpOffset { get; init; }

    /// <summary>Gets the HTTPS port offset.</summary>
    public int HttpsOffset { get; init; }

    /// <summary>
    /// Gets a value indicating whether the registry assigns the port offsets. The first start records the lowest free
    /// offset pair of the repository, and later starts reuse it.
    /// </summary>
    public bool HasAutomaticOffsets { get; init; }

    /// <summary>
    /// Gets the host application name the ID was derived from, or <c>null</c> for a declared ID. The registry uses it
    /// to detect two hosts that derive the same ID.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Creates an entry whose ID derives from the host application name and whose offsets the registry assigns.
    /// </summary>
    /// <param name="applicationName">The host application name, usually the entry assembly name.</param>
    /// <returns>The entry.</returns>
    /// <example>
    /// <code>
    /// var application = DevAppInstanceApplication.FromApplicationName(builder.Environment.ApplicationName);
    /// </code>
    /// </example>
    public static DevAppInstanceApplication FromApplicationName(string applicationName) =>
        new(DeriveAppId(applicationName)) { HasAutomaticOffsets = true, Source = applicationName };

    /// <summary>
    /// Derives an application ID from a host application name. Trailing layer segments such as <c>Presentation</c>,
    /// <c>Web</c>, <c>Server</c>, <c>Api</c> or <c>Host</c> are dropped, the last remaining segment is lowercased, and
    /// characters outside <c>a-z0-9</c> become hyphens. A result longer than <see cref="MaxAppIdLength"/> is cut and
    /// ends with an 8-character hash of the full name.
    /// </summary>
    /// <param name="applicationName">The host application name.</param>
    /// <returns>The application ID.</returns>
    /// <example>
    /// <code>
    /// DevAppInstanceApplication.DeriveAppId("Contoso.ForecastHub.Presentation.Web.Server"); // "forecasthub"
    /// DevAppInstanceApplication.DeriveAppId("TaskHarbor.Api"); // "taskharbor"
    /// </code>
    /// </example>
    public static string DeriveAppId(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var segments = applicationName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var count = segments.Length;
        while (count > 1 && LayerSegments.Contains(segments[count - 1]))
        {
            count--;
        }

        var id = NonAlphanumericRegex().Replace(segments[count - 1].ToLowerInvariant(), "-").Trim('-');
        if (id.Length == 0 || !char.IsAsciiLetterLower(id[0]))
        {
            id = $"app-{id}".TrimEnd('-');
        }

        if (id.Length > MaxAppIdLength)
        {
            var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(applicationName)))[..8];
            id = $"{id[..(MaxAppIdLength - hash.Length - 1)].TrimEnd('-')}-{hash}";
        }

        return id;
    }

    /// <summary>
    /// Validates the entry against identifier rules and the port policy.
    /// </summary>
    /// <param name="policy">The port policy.</param>
    /// <exception cref="DevAppInstanceException">Thrown with <c>configuration_conflict</c> on invalid values.</exception>
    public void Validate(DevAppInstancePortPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(this.AppId) || !AppIdRegex().IsMatch(this.AppId))
        {
            throw Conflict($"Application ID '{this.AppId}' must match ^[a-z][a-z0-9-]{{0,{MaxAppIdLength - 1}}}$.");
        }

        if (this.HasAutomaticOffsets)
        {
            return;
        }

        if (this.HttpOffset == this.HttpsOffset)
        {
            throw Conflict($"Application '{this.AppId}' uses the same HTTP and HTTPS offset {this.HttpOffset}.");
        }

        foreach (var offset in new[] { this.HttpOffset, this.HttpsOffset })
        {
            if (offset < 0 || offset >= policy.BlockSize)
            {
                throw Conflict($"Port offset {offset} of '{this.AppId}' must be within 0..{policy.BlockSize - 1}.");
            }
        }
    }

    private static DevAppInstanceException Conflict(string message) =>
        new(DevAppInstanceErrorCodes.ConfigurationConflict, message, "Correct the AddDevAppInstance declaration.");

    [GeneratedRegex("^[a-z][a-z0-9-]{0,62}$")]
    private static partial Regex AppIdRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();
}

/// <summary>
/// Describes the repository-wide port allocation policy.
/// </summary>
/// <param name="BasePort">The base port. Slot <c>s</c> starts at <c>BasePort + BlockSize * s</c>.</param>
/// <param name="BlockSize">The number of ports per slot.</param>
/// <param name="SlotCount">The number of managed slots, starting at slot 1.</param>
/// <example>
/// <code>
/// var port = DevAppInstancePortPolicy.Default.GetPort(slot: 2, offset: 11); // 5211
/// </code>
/// </example>
public sealed record DevAppInstancePortPolicy(int BasePort = 5000, int BlockSize = 100, int SlotCount = 49)
{
    /// <summary>
    /// Gets the default policy (5000/100/49).
    /// </summary>
    public static DevAppInstancePortPolicy Default { get; } = new();

    /// <summary>
    /// Gets the built-in denylist of well-known development service ports.
    /// </summary>
    public static IReadOnlySet<int> DefaultDeniedPorts { get; } = new HashSet<int> { 5341, 5432, 5672 };

    /// <summary>
    /// Computes the port for a slot and offset.
    /// </summary>
    /// <param name="slot">The slot from 1 to <see cref="SlotCount"/>.</param>
    /// <param name="offset">The offset within the slot block.</param>
    /// <returns>The port number.</returns>
    public int GetPort(int slot, int offset) => this.BasePort + (this.BlockSize * slot) + offset;

    /// <summary>
    /// Validates the policy.
    /// </summary>
    /// <exception cref="DevAppInstanceException">Thrown with <c>configuration_conflict</c> when invalid.</exception>
    public void Validate()
    {
        if (this.BasePort < 1024 || this.BlockSize < 2 || this.SlotCount < 1 || this.GetPort(this.SlotCount, this.BlockSize - 1) > 65535)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Port policy base={this.BasePort}, block={this.BlockSize}, slots={this.SlotCount} is invalid.",
                "Use a base port of at least 1024 and keep the highest port at or below 65535.");
        }
    }
}

/// <summary>
/// Describes one resource declaration. Declarations are pure data and never open connections.
/// </summary>
/// <param name="ResourceId">The resource ID, for example a module ID.</param>
/// <param name="Adapter">The adapter that plans, initializes and resets the resource.</param>
/// <param name="SourceKeys">The configuration keys mapped to this resource.</param>
/// <param name="Shared">Whether the resource is intentionally shared and therefore not rewritten.</param>
/// <param name="Options">Optional adapter-specific options.</param>
/// <example>
/// <code>
/// var declaration = new DevAppInstanceResourceDeclaration("Core", adapter, ["Modules:Core:ConnectionStrings:Default"]);
/// </code>
/// </example>
public sealed record DevAppInstanceResourceDeclaration(
    string ResourceId,
    IDevAppInstanceResourceAdapter Adapter,
    IReadOnlyList<string> SourceKeys,
    bool Shared = false,
    IReadOnlyDictionary<string, string> Options = null);

/// <summary>
/// Describes the resolved target of one declared resource.
/// </summary>
/// <param name="ResourceId">The resource ID.</param>
/// <param name="Kind">The adapter kind, for example <c>sqlserver</c> or <c>filesystem</c>.</param>
/// <param name="Target">The sanitized target, for example a database name or relative path.</param>
/// <param name="ServerIdentity">The sanitized server identity, when applicable.</param>
/// <param name="SourceKeys">The mapped configuration keys.</param>
/// <param name="Shared">Whether the resource is intentionally shared.</param>
/// <param name="ManagedValues">The managed configuration values keyed by configuration key. May contain secrets and is never logged.</param>
/// <example>
/// <code>
/// Console.WriteLine($"{plan.ResourceId}: {plan.Target}");
/// </code>
/// </example>
public sealed record DevAppInstanceResourcePlan(
    string ResourceId,
    string Kind,
    string Target,
    string ServerIdentity,
    IReadOnlyList<string> SourceKeys,
    bool Shared,
    IReadOnlyDictionary<string, string> ManagedValues);

/// <summary>
/// Describes the outcome of a resource reset or drop operation.
/// </summary>
/// <param name="Completed">The sanitized targets that were removed or were already absent.</param>
/// <param name="Remaining">The sanitized targets that could not be removed.</param>
/// <param name="Errors">The sanitized error messages.</param>
/// <example>
/// <code>
/// if (!outcome.Succeeded) { Console.Error.WriteLine(string.Join(", ", outcome.Remaining)); }
/// </code>
/// </example>
public sealed record DevAppInstanceResetOutcome(IReadOnlyList<string> Completed, IReadOnlyList<string> Remaining, IReadOnlyList<string> Errors)
{
    /// <summary>Gets a value indicating whether every target was removed.</summary>
    public bool Succeeded => this.Remaining.Count == 0;

    /// <summary>Gets an empty successful outcome.</summary>
    public static DevAppInstanceResetOutcome Empty { get; } = new([], [], []);
}

/// <summary>
/// The resolved target of an orphan reset: an inactive context that carries the orphaned slot, and the registry copy of
/// the application inventory.
/// </summary>
/// <param name="Context">The inactive orphan context. <see cref="DevAppInstanceContext.GetInstanceDatabaseName"/> yields the orphaned database names.</param>
/// <param name="Inventory">The registry copy of the application inventory.</param>
/// <example>
/// <code>
/// var orphan = DevAppInstanceResolver.ResolveOrphan(request, worktreeId);
/// foreach (var resource in orphan.Inventory.Resources) { Console.WriteLine(resource.Target); }
/// </code>
/// </example>
public sealed record DevAppInstanceOrphan(DevAppInstanceContext Context, DevAppInstanceInventory Inventory);

/// <summary>
/// Plans, guards and resets one kind of isolated development resource.
/// </summary>
/// <remarks>
/// Adapters live in their owning infrastructure package. Core code depends only on this contract.
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyAdapter : IDevAppInstanceResourceAdapter
/// {
///     public string Kind =&gt; "custom";
///     // ...
/// }
/// </code>
/// </example>
public interface IDevAppInstanceResourceAdapter
{
    /// <summary>
    /// Gets the adapter kind written to inventories, for example <c>sqlserver</c>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Plans the managed values and sanitized target for one declaration.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="context">The partially resolved context with slot and state paths.</param>
    /// <param name="configuration">The baseline configuration.</param>
    /// <returns>The plan.</returns>
    DevAppInstanceResourcePlan Plan(DevAppInstanceResourceDeclaration declaration, DevAppInstanceContext context, Microsoft.Extensions.Configuration.IConfiguration configuration);

    /// <summary>
    /// Returns configuration keys whose effective values target an ordinary baseline of the planned resources but are not declared.
    /// </summary>
    /// <param name="plans">The plans produced by this adapter.</param>
    /// <param name="baseline">The baseline values of the declared keys before rewriting.</param>
    /// <param name="configuration">The effective configuration.</param>
    /// <param name="declaredKeys">All declared or shared keys.</param>
    /// <returns>The undeclared configuration keys.</returns>
    IEnumerable<string> FindOrdinaryReferences(
        IReadOnlyList<DevAppInstanceResourcePlan> plans,
        IReadOnlyDictionary<string, string> baseline,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ISet<string> declaredKeys);

    /// <summary>
    /// Removes the given targets. Implementations are idempotent and treat absent targets as completed.
    /// </summary>
    /// <param name="targets">The inventory entries to remove.</param>
    /// <param name="context">The resolved context.</param>
    /// <param name="configuration">The effective configuration used to reach the provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<DevAppInstanceResetOutcome> ResetAsync(
        IReadOnlyList<DevAppInstanceInventoryResource> targets,
        DevAppInstanceContext context,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a value indicating whether resources of this kind outlive a deleted worktree directory and must be dropped by orphan reset.
    /// </summary>
    bool RequiresOrphanCleanup { get; }

    /// <summary>
    /// Validates that an orphaned inventory entry may be dropped through the host's own configuration.
    /// </summary>
    /// <param name="target">The inventory entry from the registry copy.</param>
    /// <param name="orphanContext">A context that carries the application ID and slot of the orphaned assignment.</param>
    /// <param name="configuration">The host's own unmanaged configuration.</param>
    /// <returns><c>null</c> when the target may be dropped; otherwise a sanitized refusal reason.</returns>
    string ValidateOrphanTarget(
        DevAppInstanceInventoryResource target,
        DevAppInstanceContext orphanContext,
        Microsoft.Extensions.Configuration.IConfiguration configuration);
}

/// <summary>
/// A resource adapter that finds its own connection strings in configuration, so hosts do not list configuration keys.
/// </summary>
/// <remarks>
/// The resolver asks every registered database adapter in registration order. A key claimed by an earlier adapter, an
/// explicit declaration or a shared key is never offered to a later adapter.
/// </remarks>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithDatabases(myAdapter));
/// </code>
/// </example>
public interface IDevAppInstanceDatabaseAdapter : IDevAppInstanceResourceAdapter
{
    /// <summary>
    /// Finds the connection strings this adapter handles and groups keys that target the same database.
    /// </summary>
    /// <param name="configuration">The baseline configuration.</param>
    /// <param name="excludedKeys">The keys that are already declared, claimed or shared.</param>
    /// <returns>One declaration per database. Each declaration lists the keys that share the database.</returns>
    IReadOnlyList<DevAppInstanceResourceDeclaration> Discover(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ISet<string> excludedKeys);

    /// <summary>
    /// Returns the connection string a registration, such as a DbContext, must use in the active instance.
    /// </summary>
    /// <param name="connectionString">The connection string passed to the registration.</param>
    /// <param name="plans">The resource plans of the active instance.</param>
    /// <param name="baseline">The baseline values of the declared keys before rewriting.</param>
    /// <param name="context">The active context.</param>
    /// <returns>
    /// The instance connection string when the value targets the ordinary database of a plan. The unchanged value when it
    /// names no database of this provider, an instance database or a shared database. <c>null</c> when it targets a
    /// database that no plan covers.
    /// </returns>
    string ResolveConnectionString(
        string connectionString,
        IReadOnlyList<DevAppInstanceResourcePlan> plans,
        IReadOnlyDictionary<string, string> baseline,
        DevAppInstanceContext context);
}
