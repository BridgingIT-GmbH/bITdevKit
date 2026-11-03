namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Configures linked-worktree development isolation on a <see cref="DevKitWebApplicationBuilder"/>. Isolation is on by
/// default, so these extensions only change the defaults, turn isolation off or read the resolved context.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args)
///     .AddConfiguration()
///     .AddDevAppInstance(o => o.WithAppId("forecasthub"))
///     .AddLogging();
/// </code>
/// </example>
public static class DevKitWebApplicationBuilderDevAppInstanceExtensions
{
    /// <summary>
    /// The builder property key that holds the <see cref="DevAppInstanceSession"/> with the resolved context.
    /// </summary>
    public const string ContextPropertyKey = "BridgingIT.DevKit.DevAppInstance.Context";

    /// <summary>
    /// The builder property key of the per-builder resolve request customizer used by tests.
    /// </summary>
    internal const string RequestCustomizerPropertyKey = "BridgingIT.DevKit.DevAppInstance.RequestCustomizer";

    /// <summary>
    /// Configures linked-worktree development isolation with defaults only, and activates it now instead of at the
    /// first configuration consumer. Isolation is on by default, so this call is only needed to activate early.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when isolation was already activated.</exception>
    /// <remarks>
    /// When isolation fails in a linked worktree, the host falls back to its configured ports and databases and writes
    /// the failure as a <c>[BDK]</c> warning. The context then reports <see cref="DevAppInstanceInactiveReasons.Fallback"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = DevKitWebApplication.CreateBuilder(args);
    /// builder.Configuration.AddConfiguration();
    /// builder.AddDevAppInstance();
    /// </code>
    /// </example>
    public static DevKitWebApplicationBuilder AddDevAppInstance(this DevKitWebApplicationBuilder builder) =>
        builder.AddDevAppInstance(_ => { });

    /// <summary>
    /// Configures linked-worktree development isolation and activates it now. Isolation is on by default for every
    /// DevKit web host, so call this only to change the defaults or to turn it off with
    /// <see cref="DevAppInstanceBuilder.Enabled"/>. Call after configuration composition and before logging, modules,
    /// DbContext registrations or the build, because the first of those activates the defaults. In the Development
    /// environment the database adapters of referenced EF Core provider packages are found automatically. Without
    /// <see cref="DevAppInstanceBuilder.WithAppId"/>, the application ID derives from the host application name.
    /// Without <see cref="DevAppInstanceBuilder.WithHttpOffset"/>, the registry assigns the port offsets.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <param name="configure">The declaration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when isolation was already activated.</exception>
    /// <remarks>
    /// When isolation fails in a linked worktree, the host falls back to its configured ports and databases and writes
    /// the failure as a <c>[BDK]</c> warning. The context then reports <see cref="DevAppInstanceInactiveReasons.Fallback"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddDevAppInstance(o => o
    ///     .WithAppId("forecasthub")
    ///     .WithHttpOffset(http: 10, https: 11));
    /// </code>
    /// </example>
    public static DevKitWebApplicationBuilder AddDevAppInstance(
        this DevKitWebApplicationBuilder builder,
        Action<DevAppInstanceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        if (builder.Properties.ContainsKey(ContextPropertyKey))
        {
            throw new InvalidOperationException(
                "AddDevAppInstance must be called once, after AddConfiguration and before AddLogging, AddModules, DbContext registrations or Build. Those calls activate the defaults.");
        }

        var declaration = new DevAppInstanceBuilder();
        configure(declaration);
        declaration.EnsureValid(builder.Environment.ApplicationName);
        CancelDefaultActivation(builder);
        ActivateAndReport(builder, declaration);

        return builder;
    }

    /// <summary>
    /// Gets the resolved development instance context. Activates the defaults first when nothing activated isolation
    /// yet.
    /// </summary>
    /// <param name="builder">The DevKit web application builder.</param>
    /// <returns>The context.</returns>
    /// <example>
    /// <code>
    /// var instance = builder.GetDevAppInstance();
    /// if (instance.IsActive) { Console.WriteLine(instance.PublicOrigin); }
    /// </code>
    /// </example>
    public static DevAppInstanceContext GetDevAppInstance(this DevKitWebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.CompleteConfiguration();

        return builder.Properties.TryGetValue(ContextPropertyKey, out var value) && value is DevAppInstanceSession session
            ? session.Context
            : DevAppInstanceContext.Inactive(DevAppInstanceInactiveReasons.NotRegistered);
    }

    /// <summary>
    /// Registers the default activation that runs at the first configuration consumer: logging, modules, a DbContext
    /// registration, <see cref="GetDevAppInstance"/> or the build.
    /// </summary>
    internal static void RegisterDefaultActivation(DevKitWebApplicationBuilder builder)
    {
        var activation = new DevAppInstanceActivation(() =>
        {
            builder.Properties.Remove(DevKitBuilderProperties.ConfigurationCompletion);
            if (builder.Properties.ContainsKey(ContextPropertyKey))
            {
                return;
            }

            var declaration = new DevAppInstanceBuilder();
            declaration.EnsureValid(builder.Environment.ApplicationName);
            ActivateAndReport(builder, declaration);
        });

        builder.Services.AddSingleton(activation);
        builder.Properties[DevKitBuilderProperties.ConfigurationCompletion] = new Action(activation.Run);
    }

    private static void CancelDefaultActivation(DevKitWebApplicationBuilder builder)
    {
        builder.Properties.Remove(DevKitBuilderProperties.ConfigurationCompletion);
        foreach (var activation in builder.Services
            .Where(d => d.ServiceType == typeof(DevAppInstanceActivation))
            .Select(d => d.ImplementationInstance)
            .OfType<DevAppInstanceActivation>())
        {
            activation.Cancel();
        }
    }

    private static void ActivateAndReport(DevKitWebApplicationBuilder builder, DevAppInstanceBuilder declaration)
    {
        var tracker = new ActivationTracker();
        try
        {
            Activate(builder, declaration, tracker);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && !IsResetRun(builder))
        {
            // Isolation never blocks the host: the configured ports and databases apply and the failure is logged.
            var failure = DevAppInstanceException.FromUnexpected(ex, declaration.Application?.AppId);
            if (builder.Properties.TryGetValue(ContextPropertyKey, out var value) && value is DevAppInstanceSession session)
            {
                session.FallBack(failure);

                return;
            }

            tracker.Undo(builder);
            Console.Error.WriteLine(failure.ToFallbackLogLine());
            Register(
                builder,
                new DevAppInstanceSession(
                    new DevAppInstanceResolveRequest { Application = declaration.Application },
                    DevAppInstanceContext.Fallback(failure, declaration.Application?.AppId),
                    []));
        }
        catch (DevAppInstanceException ex)
        {
            Console.Error.WriteLine(ex.ToStartupLogLine());
            throw;
        }
    }

    private static bool IsDevelopment(DevKitWebApplicationBuilder builder) =>
        string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    private static void AddStatusLogService(DevKitWebApplicationBuilder builder)
    {
        if (IsDevelopment(builder) && !builder.Services.Any(d => d.ImplementationType == typeof(DevAppInstanceStatusLogService)))
        {
            builder.Services.AddHostedService<DevAppInstanceStatusLogService>();
        }
    }

    // Tracks what an active instance added to the builder, so a fallback can remove it again.
    private sealed class ActivationTracker
    {
        public DevAppInstanceConfigurationSource Source { get; set; }

        public DevAppInstanceRuntimeState State { get; set; }

        public void Undo(DevKitWebApplicationBuilder builder)
        {
            if (this.Source is not null)
            {
                ((IConfigurationBuilder)builder.Configuration).Sources.Remove(this.Source);
                this.Source = null;
            }

            foreach (var descriptor in builder.Services
                .Where(d => d.ServiceType == typeof(DevAppInstanceRuntimeState) ||
                    (d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DevAppInstanceLifecycleService)))
                .ToList())
            {
                builder.Services.Remove(descriptor);
            }

            this.State?.Dispose();
            this.State = null;
            AddStatusLogService(builder);
        }
    }

    // Reset runs report their own outcome and exit code.
    private static bool IsResetRun(DevKitWebApplicationBuilder builder) =>
        DevAppInstanceArguments.Find(builder.Args ?? [], DevAppInstanceArguments.Reset) is not null ||
        DevAppInstanceArguments.Find(builder.Args ?? [], DevAppInstanceArguments.OrphanReset) is not null;

    private static bool IsModeDisabled(IConfiguration configuration) =>
        string.Equals(configuration[DevAppInstanceResolver.ModeKey]?.Trim(), "Disabled", StringComparison.OrdinalIgnoreCase);

    private static DevAppInstanceContext Activate(DevKitWebApplicationBuilder builder, DevAppInstanceBuilder declaration, ActivationTracker tracker)
    {
        // Mode=Disabled is the escape hatch: nothing else runs, so the host starts with its configured ports and databases.
        if (!declaration.IsEnabled || (IsModeDisabled(builder.Configuration) && !IsResetRun(builder)))
        {
            var disabled = DevAppInstanceContext.Inactive(DevAppInstanceInactiveReasons.Disabled, declaration.Application.AppId);
            Register(builder, new DevAppInstanceSession(new DevAppInstanceResolveRequest { Application = declaration.Application }, disabled, []));
            if (string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddHostedService<DevAppInstanceStatusLogService>();
            }

            return disabled;
        }

        var databaseAdapters = DeferDatabaseAdapters(builder, declaration);
        var request = new DevAppInstanceResolveRequest
        {
            Application = declaration.Application,
            PortPolicy = declaration.PortPolicy,
            ReservedPorts = declaration.ReservedPorts,
            ContentRoot = builder.Environment.ContentRootPath,
            EnvironmentName = builder.Environment.EnvironmentName,
            Configuration = builder.Configuration,
            Resources = declaration.Resources,
            DatabaseAdapters = databaseAdapters,
            SharedKeys = declaration.SharedKeys,
            AuthorityKeys = declaration.AuthorityKeys,
        };
        request = builder.Properties.TryGetValue(RequestCustomizerPropertyKey, out var customizer) &&
            customizer is Func<DevAppInstanceResolveRequest, DevAppInstanceResolveRequest> customize
                ? customize(request) ?? request
                : request;

        var args = builder.Args ?? [];
        var orphanArgument = DevAppInstanceArguments.Find(args, DevAppInstanceArguments.OrphanReset);
        if (orphanArgument is not null)
        {
            ActivateOrphanReset(builder, declaration, request, CollectAdapters(declaration, request.DatabaseAdapters), orphanArgument);

            return DevAppInstanceContext.Inactive(DevAppInstanceInactiveReasons.OrdinaryCheckout, declaration.Application.AppId);
        }

        var resetArgument = DevAppInstanceArguments.Find(args, DevAppInstanceArguments.Reset);
        var context = DevAppInstanceResolver.Resolve(request);

        // Inactive hosts never touch the discovered adapters, so assembly discovery only runs in a linked worktree.
        var adapters = CollectAdapters(declaration, context.IsActive ? request.DatabaseAdapters : declaration.DatabaseAdapters);
        DevAppInstanceConfigurationSource source = null;
        var baseline = context.Resources
            .SelectMany(r => r.SourceKeys ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(k => k, k => builder.Configuration[k], StringComparer.OrdinalIgnoreCase);
        var session = new DevAppInstanceSession(request, context, adapters, baseline, values => source?.Set(values), _ => tracker.Undo(builder));
        Register(builder, session);
        if (!context.IsActive)
        {
            if (resetArgument is not null)
            {
                DevAppInstanceResetRunner.Refuse(
                    new DevAppInstanceException(
                        DevAppInstanceErrorCodes.ConfigurationConflict,
                        $"Reset requires an active development instance (inactiveReason={context.InactiveReason}).",
                        "Run 'bdk dev reset' from a linked worktree in the Development environment.")
                    { AppId = context.AppId },
                    "reset");
            }

            if (string.Equals(request.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddHostedService<DevAppInstanceStatusLogService>();
            }

            return context;
        }

        if (resetArgument is not null &&
            !string.Equals(resetArgument, DevAppInstanceArguments.FormatInstanceToken(context.AppId, context.Slot.Value), StringComparison.Ordinal))
        {
            DevAppInstanceResetRunner.Refuse(
                new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ResourceNotOwned,
                    $"Reset token '{resetArgument}' does not name this instance '{context.InstanceName}'.",
                    "Run 'bdk dev reset' from the worktree that owns the instance.")
                { AppId = context.AppId, Slot = context.Slot },
                "reset");

            return context;
        }

        if (builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                "Explicit endpoint configuration 'Kestrel:Endpoints' conflicts with the managed URLs.",
                "Remove 'Kestrel:Endpoints' for local development, or set DevAppInstance:Mode=Disabled.")
            { AppId = context.AppId, Slot = context.Slot };
        }

        var ordinaryAuthorities = new[] { "Authentication:Authority" }
            .Concat(declaration.AuthorityKeys ?? [])
            .Select(k => builder.Configuration[k])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        var managed = new Dictionary<string, string>(context.ManagedValues, StringComparer.OrdinalIgnoreCase)
        {
            ["Serilog:Properties:DevAppInstanceAppId"] = context.AppId,
            ["Serilog:Properties:DevAppInstanceWorktreeId"] = context.WorktreeId?.ToString(),
            ["Serilog:Properties:DevAppInstanceSlot"] = context.Slot?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        source = new DevAppInstanceConfigurationSource(managed);
        ((IConfigurationBuilder)builder.Configuration).Add(source);
        tracker.Source = source;

        var state = new DevAppInstanceRuntimeState();
        tracker.State = state;
        try
        {
            state.Lease = DevAppInstanceLease.Acquire(
                context,
                TimeSpan.FromSeconds(5),
                attempt => state.Steps.Add(new("[debug] lease busy, retrying (attempt={Attempt}, lease={LeasePath})", [attempt, context.LeasePath])));
        }
        catch (DevAppInstanceException ex) when (resetArgument is not null)
        {
            DevAppInstanceResetRunner.Refuse(ex, "reset");

            return context;
        }

        if (resetArgument is not null)
        {
            builder.OnAfterBuild(app => DevAppInstanceResetRunner.RunInstanceReset(app, session, state));

            return context;
        }

        try
        {
            DevAppInstanceResolver.VerifyPortsAvailable(context, request.PortProbe);

            builder.Services.AddSingleton(state);
            builder.Services.AddHostedService<DevAppInstanceLifecycleService>();
            builder.Services.AddDataProtection();
            builder.Services.AddOptions<DataProtectionOptions>()
                .Configure(o =>
                {
                    if (session.Context.IsActive)
                    {
                        o.ApplicationDiscriminator = context.DataProtectionApplicationName;
                    }
                });
            builder.Services.AddOptions<KeyManagementOptions>()
                .Configure<ILoggerFactory>((o, loggerFactory) =>
                {
                    if (session.Context.IsActive)
                    {
                        o.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(context.KeysRoot), loggerFactory);
                    }
                });
            state.Steps.Add(new("data protection configured (applicationName={ApplicationName}, keys={KeysRoot})", [context.DataProtectionApplicationName, context.KeysRoot]));
            DevAppInstanceCookieIsolation.Apply(builder.Services, context, () => session.Context.IsActive);
            state.Steps.Add(new("cookies isolated (suffix={CookieSuffix})", [DevAppInstanceCookieIsolation.GetSuffix(context)]));
            builder.EnsureHostDescriptorServices();
        }
        catch
        {
            state.Dispose();
            tracker.State = null;
            throw;
        }

        builder.OnBeforeBuild(() => ValidateBeforeBuild(builder, session, source, state, ordinaryAuthorities));

        return context;
    }

    private static void Register(DevKitWebApplicationBuilder builder, DevAppInstanceSession session)
    {
        builder.Properties[ContextPropertyKey] = session;
        builder.Services.AddSingleton(session);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<DevAppInstanceSession>().Context);
    }

    private static IReadOnlyList<IDevAppInstanceDatabaseAdapter> DeferDatabaseAdapters(DevKitWebApplicationBuilder builder, DevAppInstanceBuilder declaration)
    {
        var declared = declaration.DatabaseAdapters.ToList();
        if (!string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return declared;
        }

        return new DeferredList<IDevAppInstanceDatabaseAdapter>(() =>
        {
            declared.AddRange(DevAppInstanceDatabaseAdapterLocator.Locate().Where(a => !declared.Any(d => d.Kind == a.Kind)));

            return declared;
        });
    }

    private static List<IDevAppInstanceResourceAdapter> CollectAdapters(DevAppInstanceBuilder declaration, IEnumerable<IDevAppInstanceDatabaseAdapter> databaseAdapters) =>
        declaration.Resources.Select(r => r.Adapter)
            .Concat(databaseAdapters ?? [])
            .DistinctBy(a => a.Kind)
            .ToList();

    // Defers assembly loading and scanning until a linked worktree actually reads the adapters.
    internal sealed class DeferredList<T>(Func<IReadOnlyList<T>> factory) : IReadOnlyList<T>
    {
        private readonly Lazy<IReadOnlyList<T>> items = new(factory);

        public bool IsMaterialized => this.items.IsValueCreated;

        public int Count => this.items.Value.Count;

        public T this[int index] => this.items.Value[index];

        public IEnumerator<T> GetEnumerator() => this.items.Value.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    private static void ActivateOrphanReset(
        DevKitWebApplicationBuilder builder,
        DevAppInstanceBuilder declaration,
        DevAppInstanceResolveRequest request,
        IReadOnlyList<IDevAppInstanceResourceAdapter> adapters,
        string argument)
    {
        var session = new DevAppInstanceSession(
            request,
            DevAppInstanceContext.Inactive(DevAppInstanceInactiveReasons.OrdinaryCheckout, declaration.Application.AppId),
            adapters);
        Register(builder, session);
        if (!Guid.TryParse(argument, out var worktreeId))
        {
            DevAppInstanceResetRunner.Refuse(
                new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ConfigurationConflict,
                    "The orphan-reset argument does not contain a worktree ID.",
                    "Run 'bdk dev prune --confirm' instead of passing the argument manually.")
                { AppId = declaration.Application.AppId },
                "orphan-reset");

            return;
        }

        DevAppInstanceOrphan orphan;
        try
        {
            orphan = DevAppInstanceResolver.ResolveOrphan(request, worktreeId);
        }
        catch (DevAppInstanceException ex)
        {
            DevAppInstanceResetRunner.Refuse(ex, "orphan-reset");

            return;
        }

        builder.OnAfterBuild(app => DevAppInstanceResetRunner.RunOrphanReset(app, session, orphan.Inventory, orphan.Context));
    }

    private static void ValidateBeforeBuild(
        DevKitWebApplicationBuilder builder,
        DevAppInstanceSession session,
        DevAppInstanceConfigurationSource source,
        DevAppInstanceRuntimeState state,
        IEnumerable<string> ordinaryAuthorities)
    {
        var context = session.Context;
        if (!context.IsActive)
        {
            return;
        }

        try
        {
            if (builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
            {
                throw Conflict(context, "Kestrel:Endpoints");
            }

            foreach (var (key, expected) in source.Values)
            {
                var actual = builder.Configuration[key];
                if (!string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal))
                {
                    throw Conflict(context, key);
                }
            }

            var sources = ((IConfigurationBuilder)builder.Configuration).Sources;
            if (!ReferenceEquals(sources[^1], source))
            {
                sources.Remove(source);
                sources.Add(source);
            }

            var identityStep = DevAppInstanceIdentityAlignment.AlignIdentityProvider(builder.Services, context, ordinaryAuthorities);
            if (identityStep is not null)
            {
                state.Steps.Add(identityStep);
            }

            var adapters = session.Adapters;
            var baseline = session.Baseline;
            var declaredKeys = session.DeclaredKeys;
            foreach (var group in context.Resources.Where(r => !r.Shared).GroupBy(r => r.Kind, StringComparer.Ordinal))
            {
                var adapter = adapters.FirstOrDefault(a => a.Kind == group.Key);
                if (adapter is null)
                {
                    continue;
                }

                var references = adapter.FindOrdinaryReferences(group.ToList(), baseline, builder.Configuration, declaredKeys)?.ToList() ?? [];
                if (references.Count > 0)
                {
                    throw new DevAppInstanceException(
                        DevAppInstanceErrorCodes.ConfigurationConflict,
                        $"Undeclared configuration keys target the ordinary resource: {string.Join(", ", references)}.",
                        "Map each key to its instance resource, declare it shared, or disable it for local development.")
                    { AppId = context.AppId, Slot = context.Slot };
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            session.FallBack(DevAppInstanceException.FromUnexpected(ex, context.AppId, context.Slot));
        }
    }

    private static DevAppInstanceException Conflict(DevAppInstanceContext context, string key) =>
        new(
            DevAppInstanceErrorCodes.ConfigurationConflict,
            $"Configuration key '{key}' overrides a managed development instance value.",
            $"Remove '{key}' from configuration sources added after AddDevAppInstance, or register those sources earlier.")
        { AppId = context.AppId, Slot = context.Slot };
}
