// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Base class of the database adapters that give each linked worktree its own database <c>&lt;database&gt;_wNN</c>.
/// </summary>
/// <remarks>
/// <para>
/// Discovery looks at every configuration key whose path contains <c>ConnectionString</c>. A key is claimed when its value
/// parses as a connection string of the provider and names a database. Keys that point at the same server and database
/// form one resource, so they all get the same instance database.
/// </para>
/// <para>
/// Only the database name changes. Credentials and every other option are kept and never logged.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyDatabaseAdapter : DevAppInstanceDatabaseAdapterBase
/// {
///     public override string Kind => "mydb";
///     // implement TryParse, Rewrite and ResetAsync
/// }
/// </code>
/// </example>
public abstract class DevAppInstanceDatabaseAdapterBase : IDevAppInstanceDatabaseAdapter
{
    /// <inheritdoc />
    public abstract string Kind { get; }

    /// <inheritdoc />
    public virtual bool RequiresOrphanCleanup => true;

    /// <summary>Gets the maximum length of a database identifier, as measured by <see cref="GetIdentifierLength"/>.</summary>
    protected abstract int MaxIdentifierLength { get; }

    /// <summary>Gets how database names are compared.</summary>
    protected virtual StringComparison NameComparison => StringComparison.OrdinalIgnoreCase;

    /// <inheritdoc />
    public virtual IReadOnlyList<DevAppInstanceResourceDeclaration> Discover(IConfiguration configuration, ISet<string> excludedKeys)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var groups = new Dictionary<(string Server, string Target), List<(string Key, string Database)>>();
        foreach (var (key, value) in configuration.AsEnumerable().OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(value) ||
                excludedKeys?.Contains(key) == true ||
                !key.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) ||
                !this.TryParse(value, out var server, out var database) ||
                string.IsNullOrWhiteSpace(database))
            {
                continue;
            }

            var identity = (NormalizeServer(server), this.NormalizeTarget(this.GetTarget(database)));
            if (!groups.TryGetValue(identity, out var keys))
            {
                groups[identity] = keys = [];
            }

            keys.Add((key, database));
        }

        var result = new List<DevAppInstanceResourceDeclaration>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keys in groups.Values)
        {
            var resourceId = this.GetResourceId(keys[0].Database);
            var candidate = resourceId;
            for (var i = 2; !ids.Add(candidate); i++)
            {
                candidate = $"{resourceId}_{i}";
            }

            result.Add(new DevAppInstanceResourceDeclaration(candidate, this, keys.Select(k => k.Key).ToList()));
        }

        return result;
    }

    /// <inheritdoc />
    public DevAppInstanceResourcePlan Plan(DevAppInstanceResourceDeclaration declaration, DevAppInstanceContext context, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configuration);

        var baselineKey = declaration.SourceKeys[0];
        var parsed = this.TryParse(configuration[baselineKey], out var server, out var database);
        if (declaration.Shared)
        {
            return new DevAppInstanceResourcePlan(
                declaration.ResourceId, this.Kind, parsed ? this.GetTarget(database) : null, parsed ? server : null, declaration.SourceKeys, true, new Dictionary<string, string>());
        }

        if (!parsed || string.IsNullOrWhiteSpace(database))
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Configuration key '{baselineKey}' has no {this.Kind} connection string with a database.",
                "Set a development connection string that names a database, or remove the key.")
            { AppId = context.AppId, Slot = context.Slot };
        }

        var instance = this.GetInstanceDatabaseName(database, context);
        if (this.GetIdentifierLength(instance) > this.MaxIdentifierLength)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Instance database name '{instance}' exceeds {this.MaxIdentifierLength} characters.",
                "Use a shorter database name in the development connection string.")
            { AppId = context.AppId, Slot = context.Slot };
        }

        var managed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in declaration.SourceKeys)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!this.TryParse(value, out _, out _))
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ConfigurationConflict,
                    $"Configuration key '{key}' does not contain a valid {this.Kind} connection string.",
                    "Fix the connection string or remove the key.")
                { AppId = context.AppId, Slot = context.Slot };
            }

            managed[key] = this.Rewrite(value, instance);
        }

        return new DevAppInstanceResourcePlan(declaration.ResourceId, this.Kind, this.GetTarget(instance), server, declaration.SourceKeys, false, managed);
    }

    /// <inheritdoc />
    public IEnumerable<string> FindOrdinaryReferences(
        IReadOnlyList<DevAppInstanceResourcePlan> plans,
        IReadOnlyDictionary<string, string> baseline,
        IConfiguration configuration,
        ISet<string> declaredKeys)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var ordinary = new HashSet<(string Server, string Target)>();
        foreach (var plan in (plans ?? []).Where(p => !p.Shared && p.Kind == this.Kind))
        {
            foreach (var key in plan.SourceKeys)
            {
                if (baseline is not null && baseline.TryGetValue(key, out var value) &&
                    this.TryParse(value, out var server, out var database) && !string.IsNullOrEmpty(database))
                {
                    ordinary.Add((NormalizeServer(server), this.NormalizeTarget(this.GetTarget(database))));
                }
            }
        }

        if (ordinary.Count == 0)
        {
            return [];
        }

        var references = new List<string>();
        foreach (var (key, value) in configuration.AsEnumerable())
        {
            if (string.IsNullOrWhiteSpace(value) || declaredKeys?.Contains(key) == true || !value.Contains('='))
            {
                continue;
            }

            if (this.TryParse(value, out var server, out var database) &&
                !string.IsNullOrEmpty(database) &&
                ordinary.Contains((NormalizeServer(server), this.NormalizeTarget(this.GetTarget(database)))))
            {
                references.Add(key);
            }
        }

        return references;
    }

    /// <inheritdoc />
    public virtual string ResolveConnectionString(
        string connectionString,
        IReadOnlyList<DevAppInstanceResourcePlan> plans,
        IReadOnlyDictionary<string, string> baseline,
        DevAppInstanceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(connectionString) ||
            !this.TryParse(connectionString, out var server, out var database) ||
            string.IsNullOrWhiteSpace(database))
        {
            return connectionString;
        }

        var identity = (NormalizeServer(server), this.NormalizeTarget(this.GetTarget(database)));
        foreach (var plan in (plans ?? []).Where(p => p.Kind == this.Kind))
        {
            if (identity == (NormalizeServer(plan.ServerIdentity), this.NormalizeTarget(plan.Target)))
            {
                return connectionString;
            }

            if (plan.Shared)
            {
                continue;
            }

            foreach (var key in plan.SourceKeys ?? [])
            {
                if (baseline is not null && baseline.TryGetValue(key, out var value) &&
                    this.TryParse(value, out var baselineServer, out var baselineDatabase) && !string.IsNullOrEmpty(baselineDatabase) &&
                    identity == (NormalizeServer(baselineServer), this.NormalizeTarget(this.GetTarget(baselineDatabase))))
                {
                    return this.Rewrite(connectionString, this.GetInstanceDatabaseName(database, context));
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public abstract Task<DevAppInstanceResetOutcome> ResetAsync(
        IReadOnlyList<DevAppInstanceInventoryResource> targets,
        DevAppInstanceContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual string ValidateOrphanTarget(DevAppInstanceInventoryResource target, DevAppInstanceContext orphanContext, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(orphanContext);
        ArgumentNullException.ThrowIfNull(configuration);

        if (target.Shared)
        {
            return "Shared resources are never dropped.";
        }

        if (!this.TryGetHostDatabase(target, configuration, out var server, out var database))
        {
            return $"No connection string is configured for database '{target.Target}'.";
        }

        var expected = this.GetTarget(this.GetInstanceDatabaseName(database, orphanContext));
        if (!string.Equals(target.Target, expected, this.NameComparison))
        {
            return $"Database '{target.Target}' does not match the generated name '{expected}' of the orphaned slot.";
        }

        if (!string.Equals(NormalizeServer(server), NormalizeServer(target.Server), StringComparison.Ordinal))
        {
            return $"The configured server does not match the recorded server '{target.Server}' of database '{target.Target}'.";
        }

        if (string.Equals(this.GetTarget(database), target.Target, this.NameComparison))
        {
            return $"Database '{target.Target}' is the host's own database.";
        }

        return null;
    }

    /// <summary>
    /// Returns a design-time connection string that points at the instance database of the linked worktree.
    /// </summary>
    /// <remarks>
    /// Detection is file-based and never starts a Git process. Outside a linked worktree, with
    /// <c>DevAppInstance:Mode=Disabled</c>, or when no application of the worktree manages <paramref name="connectionStringKey"/>,
    /// the connection string is returned unchanged.
    /// </remarks>
    /// <param name="connectionString">The connection string read from configuration.</param>
    /// <param name="connectionStringKey">The configuration key of the connection string.</param>
    /// <param name="startDirectory">The directory used to detect the checkout, usually the project directory.</param>
    /// <param name="configuration">The optional configuration used to honor <c>DevAppInstance:Mode=Disabled</c>.</param>
    /// <returns>The resolved connection string.</returns>
    /// <exception cref="DevAppInstanceException">Thrown when the linked worktree has not started an application yet.</exception>
    /// <example>
    /// <code>
    /// var resolved = SqlServerDevAppInstanceAdapter.Instance.ResolveDesignTime(value, key, Directory.GetCurrentDirectory(), configuration);
    /// </code>
    /// </example>
    public string ResolveDesignTime(string connectionString, string connectionStringKey, string startDirectory, IConfiguration configuration = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(connectionStringKey) || string.IsNullOrWhiteSpace(startDirectory))
        {
            return connectionString;
        }

        if (string.Equals(configuration?[DevAppInstanceResolver.ModeKey], "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var checkout = GitCheckoutDetector.Detect(startDirectory);
        if (!checkout.IsLinkedWorktree)
        {
            return connectionString;
        }

        var stateDirectory = Path.Combine(checkout.WorktreeRoot, DevAppInstancePaths.LocalDirectoryName, "state");
        var inventories = Directory.Exists(stateDirectory)
            ? Directory.EnumerateDirectories(stateDirectory).Select(DevAppInstanceInventory.Read).Where(i => i is not null).ToList()
            : [];
        if (inventories.Count == 0)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.AssignmentInvalid,
                $"No development instance is recorded in linked worktree '{checkout.WorktreeRoot}'.",
                "Start the application once from this worktree, or pass --connection-string=.");
        }

        var inventory = inventories.FirstOrDefault(i => i.Resources.Any(r =>
            r.Kind == this.Kind && !r.Shared && r.SourceKeys?.Contains(connectionStringKey, StringComparer.OrdinalIgnoreCase) == true));
        if (inventory is null || !this.TryParse(connectionString, out _, out var database) || string.IsNullOrWhiteSpace(database))
        {
            return connectionString;
        }

        return this.Rewrite(connectionString, this.GetInstanceDatabaseName(database, new DevAppInstanceContext { Slot = inventory.Slot }));
    }

    /// <summary>
    /// Parses a connection string of the provider.
    /// </summary>
    /// <param name="value">The connection string.</param>
    /// <param name="server">The server identity, or <c>null</c> when the provider has no server.</param>
    /// <param name="database">The database name, or <c>null</c> when the connection string names no database.</param>
    /// <returns><c>true</c> when the value is a connection string of the provider.</returns>
    protected abstract bool TryParse(string value, out string server, out string database);

    /// <summary>
    /// Returns the connection string with its database replaced. All other options stay unchanged.
    /// </summary>
    /// <param name="value">The connection string.</param>
    /// <param name="database">The instance database name.</param>
    /// <returns>The rewritten connection string.</returns>
    protected abstract string Rewrite(string value, string database);

    /// <summary>
    /// Builds the instance database name from the baseline database name. The default is <c>&lt;database&gt;_wNN</c>.
    /// </summary>
    /// <param name="database">The baseline database name.</param>
    /// <param name="context">The context that carries the slot.</param>
    /// <returns>The instance database name.</returns>
    protected virtual string GetInstanceDatabaseName(string database, DevAppInstanceContext context) =>
        context.GetInstanceDatabaseName(database);

    /// <summary>
    /// Returns the sanitized target recorded in inventories. The default is the database name.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <returns>The target.</returns>
    protected virtual string GetTarget(string database) => database;

    /// <summary>
    /// Returns the resource ID of a discovered database. The default is the database name.
    /// </summary>
    /// <param name="database">The baseline database name.</param>
    /// <returns>The resource ID.</returns>
    protected virtual string GetResourceId(string database) => database;

    /// <summary>
    /// Measures a database identifier. The default counts characters.
    /// </summary>
    /// <param name="name">The identifier.</param>
    /// <returns>The length.</returns>
    protected virtual int GetIdentifierLength(string name) => name.Length;

    /// <summary>
    /// Returns the host's own server and database of a recorded target.
    /// </summary>
    /// <param name="target">The recorded target.</param>
    /// <param name="configuration">The host's own unmanaged configuration.</param>
    /// <param name="server">The configured server.</param>
    /// <param name="database">The configured baseline database.</param>
    /// <returns><c>true</c> when one of the source keys holds a connection string with a database.</returns>
    protected bool TryGetHostDatabase(DevAppInstanceInventoryResource target, IConfiguration configuration, out string server, out string database)
    {
        foreach (var key in target.SourceKeys ?? [])
        {
            if (this.TryParse(configuration[key], out server, out database) && !string.IsNullOrWhiteSpace(database))
            {
                return true;
            }
        }

        server = null;
        database = null;

        return false;
    }

    /// <summary>
    /// Normalizes a server identity for comparison.
    /// </summary>
    /// <param name="server">The server identity.</param>
    /// <returns>The normalized identity.</returns>
    protected static string NormalizeServer(string server) =>
        (server ?? string.Empty).Replace("tcp:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private string NormalizeTarget(string target) =>
        this.NameComparison == StringComparison.Ordinal ? target : target?.ToUpperInvariant();
}
