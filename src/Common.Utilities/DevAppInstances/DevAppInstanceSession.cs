// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Holds the registration-time state of a development instance, so provider registrations such as
/// <c>AddSqlServerDbContext</c> can add their database adapter after <c>AddDevAppInstance</c>.
/// </summary>
/// <remarks>
/// <c>AddDevAppInstance</c> registers the session as a singleton instance. A registration calls
/// <see cref="ResolveConnectionString"/> with its adapter and connection string. When the adapter kind is new, the
/// session discovers and plans its databases, records them in the inventories and applies the managed values to
/// configuration. Without a session, or when the instance is inactive, connection strings stay unchanged.
/// </remarks>
/// <example>
/// <code>
/// options.ConnectionString = DevAppInstanceSession.ResolveConnectionString(
///     services, SqlServerDevAppInstanceAdapter.Instance, options.ConnectionString, typeof(TContext).Name);
/// </code>
/// </example>
public sealed class DevAppInstanceSession
{
    private readonly object sync = new();
    private readonly DevAppInstanceResolveRequest request;
    private readonly Action<IReadOnlyDictionary<string, string>> applyManagedValues;
    private readonly Action<DevAppInstanceException> undoActivation;
    private readonly List<IDevAppInstanceResourceAdapter> adapters;
    private readonly Dictionary<string, string> baseline;
    private readonly HashSet<string> declaredKeys;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevAppInstanceSession"/> class.
    /// </summary>
    /// <param name="request">The request that produced <paramref name="context"/>.</param>
    /// <param name="context">The resolved context.</param>
    /// <param name="adapters">The adapters of the declarations.</param>
    /// <param name="baseline">The baseline values of the declared keys.</param>
    /// <param name="applyManagedValues">Applies new managed values to configuration. <c>null</c> disables late planning.</param>
    /// <param name="undoActivation">Removes what the active instance added to the host when <see cref="FallBack"/> runs. Optional.</param>
    /// <example>
    /// <code>
    /// var session = new DevAppInstanceSession(request, context, declaration.Adapters, baseline, source.Set);
    /// services.AddSingleton(session);
    /// </code>
    /// </example>
    public DevAppInstanceSession(
        DevAppInstanceResolveRequest request,
        DevAppInstanceContext context,
        IEnumerable<IDevAppInstanceResourceAdapter> adapters,
        IReadOnlyDictionary<string, string> baseline = null,
        Action<IReadOnlyDictionary<string, string>> applyManagedValues = null,
        Action<DevAppInstanceException> undoActivation = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        this.request = request;
        this.Context = context;
        this.applyManagedValues = applyManagedValues;
        this.undoActivation = undoActivation;
        this.adapters = [.. (adapters ?? []).DistinctBy(a => a.Kind)];
        this.baseline = new Dictionary<string, string>(baseline ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        this.declaredKeys = new HashSet<string>((context.Resources ?? []).SelectMany(r => r.SourceKeys ?? []), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the current context, including resources of adapters added after resolution.</summary>
    public DevAppInstanceContext Context { get; private set; }

    /// <summary>
    /// Switches the session to the configured defaults after isolation failed. The context becomes inactive with the
    /// reason <see cref="DevAppInstanceInactiveReasons.Fallback"/>, the managed values and the lease are released and the
    /// failure is written to the startup output. Later connection strings stay unchanged.
    /// </summary>
    /// <param name="exception">The failure that stopped isolation.</param>
    /// <example>
    /// <code>
    /// session.FallBack(new DevAppInstanceException(DevAppInstanceErrorCodes.ConfigurationConflict, "Port in use."));
    /// // session.Context.InactiveReason == DevAppInstanceInactiveReasons.Fallback
    /// </code>
    /// </example>
    public void FallBack(DevAppInstanceException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (this.sync)
        {
            if (!this.Context.IsActive && this.Context.InactiveReason == DevAppInstanceInactiveReasons.Fallback)
            {
                return;
            }

            Console.Error.WriteLine(exception.ToFallbackLogLine());
            this.Context = DevAppInstanceContext.Fallback(exception, this.Context.AppId);
            this.undoActivation?.Invoke(exception);
        }
    }

    /// <summary>Gets a snapshot of the declared and late adapters, one per kind.</summary>
    public IReadOnlyList<IDevAppInstanceResourceAdapter> Adapters
    {
        get
        {
            lock (this.sync)
            {
                return [.. this.adapters];
            }
        }
    }

    /// <summary>Gets a snapshot of the baseline values of the managed resource keys. Values may contain secrets.</summary>
    public IReadOnlyDictionary<string, string> Baseline
    {
        get
        {
            lock (this.sync)
            {
                return new Dictionary<string, string>(this.baseline, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Gets a snapshot of the configuration keys of all resources.</summary>
    public ISet<string> DeclaredKeys
    {
        get
        {
            lock (this.sync)
            {
                return new HashSet<string>(this.declaredKeys, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Returns the connection string a provider registration must use. Finds the session in
    /// <paramref name="services"/>, adds the adapter when its kind is new and maps an ordinary database to the
    /// instance database.
    /// </summary>
    /// <param name="services">The service collection that may hold a session.</param>
    /// <param name="adapter">The database adapter of the provider.</param>
    /// <param name="connectionString">The connection string passed to the registration.</param>
    /// <param name="consumer">The registration name used in errors, for example the DbContext type name.</param>
    /// <returns>
    /// The instance connection string, or the unchanged value when no active session exists or the connection string
    /// targets a database that no configuration key declares. In that case the session falls back to the defaults.
    /// </returns>
    /// <example>
    /// <code>
    /// connectionString = DevAppInstanceSession.ResolveConnectionString(
    ///     services, PostgresDevAppInstanceAdapter.Instance, connectionString, nameof(TaskHarborDbContext));
    /// </code>
    /// </example>
    public static string ResolveConnectionString(
        IServiceCollection services,
        IDevAppInstanceDatabaseAdapter adapter,
        string connectionString,
        string consumer)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        var session = FindSession(services);
        if (session is null && services?.LastOrDefault(d => d.ServiceType == typeof(DevAppInstanceActivation))?.ImplementationInstance is DevAppInstanceActivation activation)
        {
            activation.Run();
            session = FindSession(services);
        }

        return session is null ? connectionString : session.ResolveConnectionString(adapter, connectionString, consumer);
    }

    /// <summary>
    /// Returns the connection string a provider registration must use in this session.
    /// </summary>
    /// <param name="adapter">The database adapter of the provider.</param>
    /// <param name="connectionString">The connection string passed to the registration.</param>
    /// <param name="consumer">The registration name used in errors.</param>
    /// <returns>
    /// The instance connection string, or the unchanged value when the instance is inactive or the connection string
    /// targets a database that no configuration key declares. In that case the session falls back to the defaults.
    /// </returns>
    /// <example>
    /// <code>
    /// var value = session.ResolveConnectionString(SqliteDevAppInstanceAdapter.Instance, "Data Source=data/taskharbor.db", "TaskHarborDbContext");
    /// </code>
    /// </example>
    public string ResolveConnectionString(IDevAppInstanceDatabaseAdapter adapter, string connectionString, string consumer)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        lock (this.sync)
        {
            var added = false;
            if (!this.adapters.Any(a => a.Kind == adapter.Kind))
            {
                this.adapters.Add(adapter);
                added = true;
            }

            if (!this.Context.IsActive || this.applyManagedValues is null || string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            try
            {
                return this.ResolveActiveConnectionString(adapter, connectionString, consumer, added);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.FallBack(DevAppInstanceException.FromUnexpected(ex, this.Context.AppId, this.Context.Slot));

                return connectionString;
            }
        }
    }

    private string ResolveActiveConnectionString(IDevAppInstanceDatabaseAdapter adapter, string connectionString, string consumer, bool added)
    {
        if (added)
        {
            this.AddResources(adapter);
        }

        var configuration = this.request.Configuration;
        if (configuration is not null &&
            (this.request.SharedKeys ?? []).Any(k => string.Equals(configuration[k], connectionString, StringComparison.Ordinal)))
        {
            return connectionString;
        }

        if ((this.Context.ManagedValues ?? new Dictionary<string, string>()).Values.Contains(connectionString, StringComparer.Ordinal))
        {
            return connectionString;
        }

        var resolved = adapter.ResolveConnectionString(
            connectionString,
            (this.Context.Resources ?? []).Where(r => r.Kind == adapter.Kind).ToList(),
            this.baseline,
            this.Context);
        if (resolved is not null)
        {
            return resolved;
        }

        throw new DevAppInstanceException(
            DevAppInstanceErrorCodes.ConfigurationConflict,
            $"The connection string passed to '{consumer}' targets a {adapter.Kind} database that no configuration key declares.",
            "Read the connection string from a configuration key whose path contains 'ConnectionString', declare it with ShareConnectionString, or set DevAppInstance:Mode=Disabled.")
        { AppId = this.Context.AppId, Slot = this.Context.Slot };
    }

    private static DevAppInstanceSession FindSession(IServiceCollection services) =>
        services?.LastOrDefault(d => d.ServiceType == typeof(DevAppInstanceSession))?.ImplementationInstance as DevAppInstanceSession;

    private void AddResources(IDevAppInstanceDatabaseAdapter adapter)
    {
        var excluded = new HashSet<string>(this.declaredKeys, StringComparer.OrdinalIgnoreCase);
        excluded.UnionWith(this.request.SharedKeys ?? []);
        var before = this.Context.Resources?.Count ?? 0;
        var context = DevAppInstanceResolver.AddDatabaseResources(this.request, this.Context, adapter, excluded);
        var plans = (context.Resources ?? []).Skip(before).ToList();
        if (plans.Count == 0)
        {
            return;
        }

        var configuration = this.request.Configuration;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in plans)
        {
            foreach (var key in plan.SourceKeys ?? [])
            {
                this.declaredKeys.Add(key);
                this.baseline[key] = configuration?[key];
            }

            foreach (var value in plan.ManagedValues ?? new Dictionary<string, string>())
            {
                values[value.Key] = value.Value;
            }
        }

        this.Context = context;
        this.applyManagedValues(values);
    }
}
