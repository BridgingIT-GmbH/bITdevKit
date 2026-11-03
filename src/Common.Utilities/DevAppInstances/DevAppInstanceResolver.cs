// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Microsoft.Extensions.Configuration;

/// <summary>
/// The input of <see cref="DevAppInstanceResolver.Resolve"/>.
/// </summary>
/// <example>
/// <code>
/// var request = new DevAppInstanceResolveRequest
/// {
///     Application = new DevAppInstanceApplication("forecasthub", httpOffset: 10, httpsOffset: 11),
///     ContentRoot = builder.Environment.ContentRootPath,
///     EnvironmentName = builder.Environment.EnvironmentName,
///     Configuration = builder.Configuration,
/// };
/// </code>
/// </example>
public sealed class DevAppInstanceResolveRequest
{
    /// <summary>Gets the application catalog entry.</summary>
    public DevAppInstanceApplication Application { get; init; }

    /// <summary>Gets the port policy.</summary>
    public DevAppInstancePortPolicy PortPolicy { get; init; } = DevAppInstancePortPolicy.Default;

    /// <summary>Gets additional reserved ports skipped during allocation.</summary>
    public IReadOnlyCollection<int> ReservedPorts { get; init; } = [];

    /// <summary>Gets the host content root used for Git detection.</summary>
    public string ContentRoot { get; init; }

    /// <summary>Gets the host environment name.</summary>
    public string EnvironmentName { get; init; }

    /// <summary>Gets the baseline configuration.</summary>
    public IConfiguration Configuration { get; init; }

    /// <summary>Gets the resource declarations.</summary>
    public IReadOnlyList<DevAppInstanceResourceDeclaration> Resources { get; init; } = [];

    /// <summary>Gets the database adapters that discover connection strings, in registration order.</summary>
    public IReadOnlyList<IDevAppInstanceDatabaseAdapter> DatabaseAdapters { get; init; } = [];

    /// <summary>Gets the connection-string keys that stay unchanged and point every instance at the same database.</summary>
    public IReadOnlyCollection<string> SharedKeys { get; init; } = [];

    /// <summary>Gets the configuration keys that hold additional authorities or issuers.</summary>
    public IReadOnlyList<string> AuthorityKeys { get; init; } = [];

    /// <summary>Gets the optional Git command fallback.</summary>
    public IGitCommandRunner GitRunner { get; init; } = new ProcessGitCommandRunner();

    /// <summary>Gets the port probe.</summary>
    public IDevAppInstancePortProbe PortProbe { get; init; } = new LoopbackPortProbe();

    /// <summary>Gets the environment variable reader.</summary>
    public Func<string, string> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>Gets the time provider.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Gets the registry lock timeout.</summary>
    public TimeSpan RegistryLockTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets a value indicating whether the environment, container and cloud checks are bypassed. Intended for tests.</summary>
    public bool AllowOutsideDevelopment { get; init; }

    /// <summary>Gets an optional build-time override. Intended for tests.</summary>
    public bool? IsBuildTime { get; init; }
}

/// <summary>
/// Resolves the immutable <see cref="DevAppInstanceContext"/> of a host.
/// </summary>
/// <remarks>
/// Eligibility is evaluated first. Ineligible and ordinary-checkout hosts get an inactive context without any file access
/// beyond Git detection. Linked worktrees create or recover their persistent assignment under the registry lock.
/// </remarks>
/// <example>
/// <code>
/// var context = DevAppInstanceResolver.Resolve(request);
/// if (context.IsActive) { builder.Configuration.AddInMemoryCollection(context.ManagedValues); }
/// </code>
/// </example>
public static class DevAppInstanceResolver
{
    /// <summary>Gets the configuration key of the mode.</summary>
    public const string ModeKey = "DevAppInstance:Mode";

    /// <summary>Gets the configuration key of the requested slot.</summary>
    public const string SlotKey = "DevAppInstance:Slot";

    /// <summary>The configuration keys of the URL settings replaced in managed mode.</summary>
    public static readonly IReadOnlyList<string> UrlKeys = ["urls", "http_ports", "https_ports", "https_port"];

    /// <summary>Gets the resource kind of shared connection strings.</summary>
    public const string SharedConnectionStringKind = "connectionstring";

    /// <summary>
    /// Returns whether a resource kind names a database adapter.
    /// </summary>
    /// <param name="kind">The resource kind.</param>
    /// <returns><c>true</c> for database kinds such as <c>sqlserver</c>, <c>postgres</c> or <c>sqlite</c>.</returns>
    /// <example>
    /// <code>
    /// var databases = inventory.Resources.Where(r => !r.Shared &amp;&amp; DevAppInstanceResolver.IsDatabaseKind(r.Kind));
    /// </code>
    /// </example>
    public static bool IsDatabaseKind(string kind) =>
        !string.IsNullOrEmpty(kind) && kind is not SharedConnectionStringKind;

    /// <summary>
    /// Returns the explicit resource declarations followed by the declarations the database adapters discover.
    /// </summary>
    /// <param name="request">The request with declarations, database adapters and shared keys.</param>
    /// <param name="configuration">The baseline configuration.</param>
    /// <returns>The declarations in planning order.</returns>
    /// <example>
    /// <code>
    /// var declarations = DevAppInstanceResolver.DiscoverResources(request, builder.Configuration);
    /// </code>
    /// </example>
    public static IReadOnlyList<DevAppInstanceResourceDeclaration> DiscoverResources(
        DevAppInstanceResolveRequest request,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new List<DevAppInstanceResourceDeclaration>(request.Resources ?? []);
        var excluded = new HashSet<string>(result.SelectMany(r => r.SourceKeys), StringComparer.OrdinalIgnoreCase);
        excluded.UnionWith(request.SharedKeys ?? []);
        foreach (var adapter in request.DatabaseAdapters ?? [])
        {
            foreach (var declaration in adapter.Discover(configuration, excluded))
            {
                result.Add(declaration);
                excluded.UnionWith(declaration.SourceKeys);
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves the context.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The context.</returns>
    /// <exception cref="DevAppInstanceException">Thrown when isolation is required but cannot be resolved safely.</exception>
    public static DevAppInstanceContext Resolve(DevAppInstanceResolveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Application);
        var watch = Stopwatch.StartNew();
        var configuration = request.Configuration ?? new ConfigurationBuilder().Build();
        var application = request.Application;

        // Configuration mistakes only fail inside a linked worktree. Elsewhere the host starts unchanged and logs a warning.
        var modeValid = TryReadMode(configuration, out var mode);
        var reason = DevAppInstanceEligibility.Evaluate(
            mode, request.EnvironmentName, request.GetEnvironmentVariable, request.AllowOutsideDevelopment, request.IsBuildTime);
        if (reason is not null)
        {
            return InactiveWithWarnings(reason, application.AppId, modeValid ? null : InvalidModeWarning(configuration));
        }

        var checkout = GitCheckoutDetector.Detect(request.ContentRoot, request.GitRunner);
        if (!checkout.IsLinkedWorktree)
        {
            return InactiveWithWarnings(
                checkout.Kind == GitCheckoutKind.Ordinary ? DevAppInstanceInactiveReasons.OrdinaryCheckout : DevAppInstanceInactiveReasons.OutsideGit,
                application.AppId,
                modeValid ? null : InvalidModeWarning(configuration),
                string.IsNullOrWhiteSpace(configuration[SlotKey])
                    ? null
                    : new DevAppInstanceLogStep("[warning] {SlotKey} is ignored because the host does not run in a linked worktree (app={AppId})", [SlotKey, application.AppId]),
                checkout.Warning is null
                    ? null
                    : new DevAppInstanceLogStep("[warning] unusable Git metadata ignored, host starts without isolation: {GitWarning} (app={AppId})", [checkout.Warning, application.AppId]));
        }

        if (!modeValid)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"{ModeKey} must be Auto or Disabled.",
                $"Correct {ModeKey}.")
            { AppId = application.AppId };
        }

        var requestedSlot = ReadSlot(configuration, application.AppId);

        if (!string.IsNullOrEmpty(request.GetEnvironmentVariable("ASPNETCORE_IIS_PHYSICAL_PATH")) ||
            !string.IsNullOrEmpty(request.GetEnvironmentVariable("APP_POOL_ID")))
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.UnsupportedHost,
                "Linked-worktree isolation does not support IIS or IIS Express hosting.",
                "Start the host with its project launch profile (dotnet run).")
            { AppId = application.AppId };
        }

        var policy = request.PortPolicy ?? DevAppInstancePortPolicy.Default;
        policy.Validate();
        application.Validate(policy);
        if (requestedSlot is { } rs && (rs < 1 || rs > policy.SlotCount))
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"{SlotKey} must be within 1..{policy.SlotCount}.",
                $"Correct {SlotKey}.")
            { AppId = application.AppId };
        }

        var steps = new List<DevAppInstanceLogStep>
        {
            new("detected (app={AppId}, worktreeRoot={WorktreeRoot})", [application.AppId, checkout.WorktreeRoot]),
        };

        var registryPath = DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory);
        using var registryLock = DevAppInstanceFiles.AcquireLock(
            DevAppInstancePaths.GetRegistryLockFile(checkout.CommonDirectory), request.RegistryLockTimeout);
        var registry = DevAppInstanceRegistry.Load(registryPath);

        application = ValidateCatalog(registry, application, policy);

        var (worktreeId, slot, selection) = ResolveAssignment(request, registry, checkout, application, policy, requestedSlot, steps);
        steps.Add(new("slot {Slot} assigned (app={AppId}, worktreeId={WorktreeId}, selection={Selection})", [slot, application.AppId, worktreeId, selection.ToString().ToLowerInvariant()]));

        var stateRoot = DevAppInstancePaths.GetStateRoot(checkout.WorktreeRoot, application.AppId);
        var context = new DevAppInstanceContext
        {
            IsActive = true,
            AppId = application.AppId,
            WorktreeId = worktreeId,
            Slot = slot,
            SelectionReason = selection,
            WorktreeRoot = checkout.WorktreeRoot,
            CommonDirectory = checkout.CommonDirectory,
            HttpPort = policy.GetPort(slot, application.HttpOffset),
            HttpsPort = policy.GetPort(slot, application.HttpsOffset),
            StateRoot = stateRoot,
            LeasePath = DevAppInstancePaths.GetLeaseFile(checkout.CommonDirectory, worktreeId, application.AppId),
        };

        var managed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var replacedUrls = configuration["urls"];
        managed["urls"] = $"{context.HttpsUrl};{context.HttpUrl}";
        managed["http_ports"] = string.Empty;
        managed["https_ports"] = string.Empty;
        managed["https_port"] = context.HttpsPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var ordinaryAuthority = configuration["Authentication:Authority"];
        if (Uri.TryCreate(ordinaryAuthority, UriKind.Absolute, out var authorityUri) &&
            (authorityUri.Scheme == Uri.UriSchemeHttps || authorityUri.Scheme == Uri.UriSchemeHttp) &&
            authorityUri.IsLoopback)
        {
            managed["Authentication:Authority"] = context.PublicOrigin;
        }

        foreach (var key in request.AuthorityKeys ?? [])
        {
            managed[key] = context.PublicOrigin;
        }

        steps.Add(new("urls resolved (http={HttpUrl}, https={HttpsUrl})", [context.HttpUrl, context.HttpsUrl]));
        if (!string.IsNullOrWhiteSpace(replacedUrls) && !string.Equals(replacedUrls, managed["urls"], StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new("baseline urls replaced (replaced={ReplacedUrls}, source=urls/ASPNETCORE_URLS)", [replacedUrls]));
        }

        var plans = new List<DevAppInstanceResourcePlan>();
        foreach (var declaration in DiscoverResources(request, configuration))
        {
            var plan = declaration.Adapter.Plan(declaration, context, configuration);
            plans.Add(plan);
            foreach (var value in plan.ManagedValues ?? new Dictionary<string, string>())
            {
                managed[value.Key] = value.Value;
            }

            steps.Add(plan.Shared
                ? new("resource shared (resource={ResourceId}, kind={Kind})", [plan.ResourceId, plan.Kind])
                : new("resource resolved (resource={ResourceId}, kind={Kind}, target={Target})", [plan.ResourceId, plan.Kind, plan.Target]));
        }

        foreach (var key in (request.SharedKeys ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            plans.Add(new DevAppInstanceResourcePlan(key, SharedConnectionStringKind, null, null, [key], true, new Dictionary<string, string>()));
            steps.Add(new("resource shared (resource={ResourceId}, kind={Kind})", [key, SharedConnectionStringKind]));
        }

        var inventory = new DevAppInstanceInventory(
            application.AppId,
            worktreeId,
            slot,
            GetProjectPath(checkout.WorktreeRoot, request.ContentRoot),
            plans.Where(p => !p.Shared)
                .Select(p => new DevAppInstanceInventoryResource(p.ResourceId, p.Kind, p.Target, p.ServerIdentity, p.SourceKeys, p.Shared))
                .ToList());
        registry.SetInventory(inventory);
        registry.Save(registryPath);
        new DevAppInstanceLocalAssignment(worktreeId, slot).Write(checkout.WorktreeRoot);
        EnsureStateRoot(context);
        inventory.Write(stateRoot);

        steps.Add(new("resolution completed (durationMs={DurationMs})", [watch.ElapsedMilliseconds]));

        return context with { Resources = plans, ManagedValues = managed, LogSteps = steps };
    }

    /// <summary>
    /// Discovers and plans the databases of an adapter that registers after <see cref="Resolve"/>, for example from a
    /// DbContext registration, and records the new resources in the registry and local inventories.
    /// </summary>
    /// <param name="request">The request that produced <paramref name="context"/>.</param>
    /// <param name="context">The active context.</param>
    /// <param name="adapter">The database adapter.</param>
    /// <param name="excludedKeys">The keys that are already declared, claimed or shared.</param>
    /// <returns>The context with the new resources, managed values and log steps, or the unchanged context when the adapter finds nothing.</returns>
    /// <exception cref="DevAppInstanceException">Thrown when a discovered database cannot be planned safely.</exception>
    /// <example>
    /// <code>
    /// context = DevAppInstanceResolver.AddDatabaseResources(request, context, adapter, excludedKeys);
    /// </code>
    /// </example>
    public static DevAppInstanceContext AddDatabaseResources(
        DevAppInstanceResolveRequest request,
        DevAppInstanceContext context,
        IDevAppInstanceDatabaseAdapter adapter,
        ISet<string> excludedKeys)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(adapter);
        if (!context.IsActive)
        {
            return context;
        }

        var configuration = request.Configuration ?? new ConfigurationBuilder().Build();
        var plans = new List<DevAppInstanceResourcePlan>();
        var managed = new Dictionary<string, string>(context.ManagedValues, StringComparer.OrdinalIgnoreCase);
        var steps = new List<DevAppInstanceLogStep>(context.LogSteps ?? []);
        foreach (var declaration in adapter.Discover(configuration, excludedKeys ?? new HashSet<string>()))
        {
            var plan = declaration.Adapter.Plan(declaration, context, configuration);
            plans.Add(plan);
            foreach (var value in plan.ManagedValues ?? new Dictionary<string, string>())
            {
                managed[value.Key] = value.Value;
            }

            steps.Add(plan.Shared
                ? new("resource shared (resource={ResourceId}, kind={Kind})", [plan.ResourceId, plan.Kind])
                : new("resource resolved (resource={ResourceId}, kind={Kind}, target={Target})", [plan.ResourceId, plan.Kind, plan.Target]));
        }

        if (plans.Count == 0)
        {
            return context;
        }

        var resources = (context.Resources ?? []).Concat(plans).ToList();
        var inventory = new DevAppInstanceInventory(
            context.AppId,
            context.WorktreeId.Value,
            context.Slot.Value,
            GetProjectPath(context.WorktreeRoot, request.ContentRoot),
            resources.Where(p => !p.Shared)
                .Select(p => new DevAppInstanceInventoryResource(p.ResourceId, p.Kind, p.Target, p.ServerIdentity, p.SourceKeys, p.Shared))
                .ToList());
        var registryPath = DevAppInstancePaths.GetRegistryFile(context.CommonDirectory);
        using (DevAppInstanceFiles.AcquireLock(DevAppInstancePaths.GetRegistryLockFile(context.CommonDirectory), request.RegistryLockTimeout))
        {
            var registry = DevAppInstanceRegistry.Load(registryPath);
            registry.SetInventory(inventory);
            registry.Save(registryPath);
        }

        inventory.Write(context.StateRoot);

        return context with { Resources = resources, ManagedValues = managed, LogSteps = steps };
    }

    /// <summary>
    /// Resolves the registry inventory of an orphaned worktree assignment for the orphan-reset mode, without activating
    /// managed mode or writing any state.
    /// </summary>
    /// <param name="request">The resolve request of the host that runs the orphan reset.</param>
    /// <param name="worktreeId">The orphaned worktree ID.</param>
    /// <returns>The orphan context and the registry copy of the application inventory.</returns>
    /// <exception cref="DevAppInstanceException">Thrown when the host is not eligible or the registry has no matching entry.</exception>
    /// <example>
    /// <code>
    /// var orphan = DevAppInstanceResolver.ResolveOrphan(request, worktreeId);
    /// Console.WriteLine(orphan.Context.GetInstanceDatabaseName("ForecastHub"));
    /// </code>
    /// </example>
    public static DevAppInstanceOrphan ResolveOrphan(DevAppInstanceResolveRequest request, Guid worktreeId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Application);
        var configuration = request.Configuration ?? new ConfigurationBuilder().Build();
        var application = request.Application;

        var reason = DevAppInstanceEligibility.Evaluate(
            ReadMode(configuration, application.AppId), request.EnvironmentName, request.GetEnvironmentVariable, request.AllowOutsideDevelopment, request.IsBuildTime);
        if (reason is not null)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Orphan reset requires an eligible development host (inactiveReason={reason}).",
                "Run 'bdk dev prune --confirm' from a development checkout of the repository.")
            { AppId = application.AppId };
        }

        var checkout = GitCheckoutDetector.Detect(request.ContentRoot, request.GitRunner);
        if (checkout.Kind == GitCheckoutKind.OutsideGit)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                "Orphan reset requires a checkout of the repository that owns the registry.",
                "Run 'bdk dev prune --confirm' from a checkout of the repository.")
            { AppId = application.AppId };
        }

        DevAppInstanceAssignment assignment;
        using (DevAppInstanceFiles.AcquireLock(DevAppInstancePaths.GetRegistryLockFile(checkout.CommonDirectory), request.RegistryLockTimeout))
        {
            assignment = DevAppInstanceRegistry.Load(DevAppInstancePaths.GetRegistryFile(checkout.CommonDirectory)).FindByWorktreeId(worktreeId);
        }

        var inventory = assignment?.Applications.FirstOrDefault(a => string.Equals(a.AppId, application.AppId, StringComparison.Ordinal));
        if (inventory is null)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ResourceNotOwned,
                $"The registry records no inventory of application '{application.AppId}' for worktree '{worktreeId}'.",
                "Run 'bdk dev prune' to list the orphaned assignments.")
            { AppId = application.AppId };
        }

        var context = new DevAppInstanceContext
        {
            IsActive = false,
            InactiveReason = DevAppInstanceInactiveReasons.OrdinaryCheckout,
            AppId = application.AppId,
            WorktreeId = worktreeId,
            Slot = assignment.Slot,
            WorktreeRoot = assignment.Path,
            CommonDirectory = checkout.CommonDirectory,
        };

        return new DevAppInstanceOrphan(context, inventory);
    }

    /// <summary>
    /// Verifies that the assigned ports of an active context are free. Call after acquiring the lease.
    /// </summary>
    /// <param name="context">The active context.</param>
    /// <param name="probe">The port probe.</param>
    /// <exception cref="DevAppInstanceException">Thrown with <c>port_in_use</c>.</exception>
    public static void VerifyPortsAvailable(DevAppInstanceContext context, IDevAppInstancePortProbe probe)
    {
        if (!context.IsActive)
        {
            return;
        }

        probe ??= new LoopbackPortProbe();
        foreach (var port in new[] { context.HttpPort!.Value, context.HttpsPort!.Value })
        {
            if (!probe.IsBindable(port))
            {
                var reserved = probe.IsReserved(port);
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.PortInUse,
                    reserved
                        ? $"Port {port} of the assigned slot is in an OS-reserved range."
                        : $"Port {port} of the assigned slot is in use by another process.",
                    "Stop the process that uses the port, or run 'bdk dev release' and start the host again to get a new slot.")
                { AppId = context.AppId, Slot = context.Slot };
            }
        }
    }

    /// <summary>
    /// Determines whether a value is an absolute loopback HTTP or HTTPS URL.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c> for loopback URLs.</returns>
    public static bool IsLoopbackUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.IsLoopback;

    internal static void EnsureStateRoot(DevAppInstanceContext context)
    {
        Directory.CreateDirectory(context.PayloadRoot);
        Directory.CreateDirectory(context.LogsRoot);
        Directory.CreateDirectory(context.KeysRoot);
        var ownerFile = DevAppInstancePaths.GetOwnerFile(context.StateRoot);
        if (!File.Exists(ownerFile))
        {
            DevAppInstanceFiles.WriteJsonAtomic(ownerFile, new System.Text.Json.Nodes.JsonObject
            {
                ["version"] = 1,
                ["appId"] = context.AppId,
                ["worktreeId"] = context.WorktreeId.ToString(),
            });
        }
    }

    internal static string GetProjectPath(string worktreeRoot, string contentRoot)
    {
        if (string.IsNullOrEmpty(contentRoot))
        {
            return string.Empty;
        }

        return Path.GetRelativePath(worktreeRoot, GitCheckoutDetector.Canonicalize(contentRoot)).Replace('\\', '/');
    }

    private static (Guid WorktreeId, int Slot, DevAppInstanceSelectionReason Selection) ResolveAssignment(
        DevAppInstanceResolveRequest request,
        DevAppInstanceRegistry registry,
        GitCheckoutInfo checkout,
        DevAppInstanceApplication application,
        DevAppInstancePortPolicy policy,
        int? requestedSlot,
        List<DevAppInstanceLogStep> steps)
    {
        var appId = application.AppId;
        var local = DevAppInstanceLocalAssignment.Read(checkout.WorktreeRoot);
        var byPath = registry.FindByPath(checkout.WorktreeRoot);

        if (local is not null)
        {
            var byId = registry.FindByWorktreeId(local.WorktreeId);
            if (byId is not null)
            {
                if (!GitCheckoutDetector.PathsEqual(byId.Path, checkout.WorktreeRoot))
                {
                    if (Directory.Exists(byId.Path) || byPath is not null)
                    {
                        throw Invalid(appId, local.Slot, $"The assignment file claims worktree ID {local.WorktreeId}, which the registry records for '{byId.Path}'.",
                            "This file was probably copied from another worktree. Delete .dev/instance.json and start again.");
                    }

                    // the worktree was moved (git worktree move): keep its identity and record the new path
                    registry.RemoveAssignment(byId.WorktreeId);
                    registry.AddAssignment(byId.WorktreeId, byId.Slot, checkout.WorktreeRoot, byId.AssignedAt);
                    foreach (var inventory in byId.Applications)
                    {
                        registry.SetInventory(inventory);
                    }
                }

                if (byId.Slot != local.Slot)
                {
                    throw Invalid(appId, local.Slot, $"The assignment file records slot {local.Slot}, but the registry records slot {byId.Slot}.",
                        "Delete .dev/instance.json to recover the registry assignment, or run 'bdk dev configure --slot'.");
                }

                EnsureRequestedSlot(appId, requestedSlot, byId.Slot);

                return (byId.WorktreeId, byId.Slot, DevAppInstanceSelectionReason.Existing);
            }

            if (byPath is not null)
            {
                throw Invalid(appId, local.Slot, $"The assignment file claims worktree ID {local.WorktreeId}, but the registry records {byPath.WorktreeId} for this path.",
                    "Delete .dev/instance.json to recover the registry assignment.");
            }

            EnsureRequestedSlot(appId, requestedSlot, local.Slot);
            if (local.Slot > policy.SlotCount)
            {
                throw Invalid(appId, local.Slot, $"The assignment file records slot {local.Slot}, outside 1..{policy.SlotCount}.",
                    "Delete .dev/instance.json and start again.");
            }

            if (registry.FindBySlot(local.Slot) is { } owner)
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.SlotInUse,
                    $"Slot {local.Slot} from the assignment file belongs to '{owner.Path}'.",
                    "Delete .dev/instance.json to allocate a new slot.")
                { AppId = appId, Slot = local.Slot };
            }

            registry.AddAssignment(local.WorktreeId, local.Slot, checkout.WorktreeRoot, request.TimeProvider.GetUtcNow());

            return (local.WorktreeId, local.Slot, DevAppInstanceSelectionReason.Recovered);
        }

        if (byPath is not null)
        {
            EnsureRequestedSlot(appId, requestedSlot, byPath.Slot);

            return (byPath.WorktreeId, byPath.Slot, DevAppInstanceSelectionReason.Recovered);
        }

        var catalog = registry.Applications;
        var denied = new HashSet<int>(DevAppInstancePortPolicy.DefaultDeniedPorts);
        denied.UnionWith(request.ReservedPorts ?? []);
        int slot;
        if (requestedSlot is { } requested)
        {
            if (registry.FindBySlot(requested) is { } owner)
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.SlotInUse,
                    $"Slot {requested} belongs to '{owner.Path}'.",
                    "Choose another slot, or release the slot from the other worktree.")
                { AppId = appId, Slot = requested };
            }

            slot = requested;
        }
        else
        {
            slot = Allocate(registry, catalog, policy, denied, request.PortProbe, steps)
                ?? throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.SlotsExhausted,
                    $"All {policy.SlotCount} slots are claimed or blocked.",
                    "Run 'bdk dev prune' for deleted worktrees, or inspect assignments with 'bdk dev status' and release unused ones with 'bdk dev release'.")
                { AppId = appId };
        }

        var worktreeId = Guid.NewGuid();
        registry.AddAssignment(worktreeId, slot, checkout.WorktreeRoot, request.TimeProvider.GetUtcNow());

        return (worktreeId, slot, DevAppInstanceSelectionReason.New);
    }

    private static int? Allocate(
        DevAppInstanceRegistry registry,
        IReadOnlyList<DevAppInstanceApplication> catalog,
        DevAppInstancePortPolicy policy,
        ISet<int> denied,
        IDevAppInstancePortProbe probe,
        List<DevAppInstanceLogStep> steps)
    {
        probe ??= new LoopbackPortProbe();
        var taken = registry.Assignments.Select(a => a.Slot).ToHashSet();
        for (var slot = 1; slot <= policy.SlotCount; slot++)
        {
            if (taken.Contains(slot))
            {
                continue;
            }

            var ports = catalog.SelectMany(a => new[] { policy.GetPort(slot, a.HttpOffset), policy.GetPort(slot, a.HttpsOffset) }).Distinct().ToList();
            var blocked = ports.FirstOrDefault(p => denied.Contains(p) || probe.IsReserved(p) || !probe.IsBindable(p));
            if (blocked != 0)
            {
                steps.Add(new("[debug] slot skipped (slot={Slot}, port={Port})", [slot, blocked]));
                continue;
            }

            return slot;
        }

        return null;
    }

    private static DevAppInstanceApplication ValidateCatalog(DevAppInstanceRegistry registry, DevAppInstanceApplication application, DevAppInstancePortPolicy policy)
    {
        if (registry.PortPolicy is { } recorded && recorded != policy)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Port policy base={policy.BasePort}, block={policy.BlockSize}, slots={policy.SlotCount} differs from the recorded policy base={recorded.BasePort}, block={recorded.BlockSize}, slots={recorded.SlotCount}.",
                "Declare the recorded port policy in every host of this repository.")
            { AppId = application.AppId };
        }

        if (registry.PortPolicy is null)
        {
            registry.SetPortPolicy(policy);
        }

        var recordedApps = registry.Applications;
        var own = recordedApps.FirstOrDefault(a => a.AppId == application.AppId);
        if (own is not null && application.Source is not null && own.Source is not null &&
            !string.Equals(own.Source, application.Source, StringComparison.Ordinal))
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Host '{application.Source}' derives the application ID '{application.AppId}', which host '{own.Source}' already uses.",
                "Declare a distinct ID with WithAppId(appId) in one of the hosts.")
            { AppId = application.AppId };
        }

        if (application.HasAutomaticOffsets)
        {
            if (own is not null)
            {
                return application with { HttpOffset = own.HttpOffset, HttpsOffset = own.HttpsOffset };
            }

            var used = recordedApps.SelectMany(a => new[] { a.HttpOffset, a.HttpsOffset }).ToHashSet();
            var http = Enumerable.Range(0, policy.BlockSize / 2).Select(k => k * 2).FirstOrDefault(o => !used.Contains(o) && !used.Contains(o + 1), -1);
            if (http < 0)
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ConfigurationConflict,
                    $"No free port offset pair is left for application '{application.AppId}' within block size {policy.BlockSize}.",
                    "Remove unused applications with 'bdk dev configure --forget-app', or declare a larger block size.")
                { AppId = application.AppId };
            }

            var assigned = application with { HttpOffset = http, HttpsOffset = http + 1 };
            registry.AddApplication(assigned);

            return assigned;
        }

        if (own is not null && (own.HttpOffset != application.HttpOffset || own.HttpsOffset != application.HttpsOffset))
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"The declaration of '{application.AppId}' (offsets={application.HttpOffset}/{application.HttpsOffset}) differs from the recorded entry (offsets={own.HttpOffset}/{own.HttpsOffset}).",
                $"Run 'bdk dev configure --forget-app {application.AppId}' to record the new declaration.")
            { AppId = application.AppId };
        }

        foreach (var other in recordedApps.Where(a => a.AppId != application.AppId))
        {
            var otherOffsets = new[] { other.HttpOffset, other.HttpsOffset };
            if (otherOffsets.Contains(application.HttpOffset) || otherOffsets.Contains(application.HttpsOffset))
            {
                throw new DevAppInstanceException(
                    DevAppInstanceErrorCodes.ConfigurationConflict,
                    $"Application '{application.AppId}' (offsets={application.HttpOffset}/{application.HttpsOffset}) conflicts with recorded application '{other.AppId}' (offsets={other.HttpOffset}/{other.HttpsOffset}).",
                    "Give each application of the repository a distinct ID and distinct port offsets.")
                { AppId = application.AppId };
            }
        }

        if (own is null)
        {
            registry.AddApplication(application);
        }

        return application;
    }

    private static void EnsureRequestedSlot(string appId, int? requestedSlot, int assignedSlot)
    {
        if (requestedSlot is { } requested && requested != assignedSlot)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"{SlotKey}={requested} differs from the existing assignment slot {assignedSlot}.",
                "Remove the slot setting, or change the assignment with 'bdk dev configure --slot'.")
            { AppId = appId, Slot = assignedSlot };
        }
    }

    private static bool TryReadMode(IConfiguration configuration, out DevAppInstanceMode mode)
    {
        mode = DevAppInstanceMode.Auto;
        var value = configuration[ModeKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<DevAppInstanceMode>(value, true, out var parsed) && Enum.IsDefined(parsed))
        {
            mode = parsed;
            return true;
        }

        return false;
    }

    private static DevAppInstanceLogStep InvalidModeWarning(IConfiguration configuration) =>
        new("[warning] {ModeKey}={Mode} is not Auto or Disabled and is treated as Auto", [ModeKey, configuration[ModeKey]]);

    private static DevAppInstanceContext InactiveWithWarnings(string reason, string appId, params DevAppInstanceLogStep[] warnings)
    {
        var steps = warnings.Where(w => w is not null).ToList();

        return steps.Count == 0
            ? DevAppInstanceContext.Inactive(reason, appId)
            : DevAppInstanceContext.Inactive(reason, appId) with { LogSteps = steps };
    }

    private static DevAppInstanceMode ReadMode(IConfiguration configuration, string appId)
    {
        if (TryReadMode(configuration, out var mode))
        {
            return mode;
        }

        throw new DevAppInstanceException(
            DevAppInstanceErrorCodes.ConfigurationConflict,
            $"{ModeKey} must be Auto or Disabled.",
            $"Correct {ModeKey}.")
        { AppId = appId };
    }

    private static int? ReadSlot(IConfiguration configuration, string appId)
    {
        var value = configuration[SlotKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var slot))
        {
            return slot;
        }

        throw new DevAppInstanceException(
            DevAppInstanceErrorCodes.ConfigurationConflict,
            $"{SlotKey} must be an integer.",
            $"Correct {SlotKey}.")
        { AppId = appId };
    }

    private static DevAppInstanceException Invalid(string appId, int slot, string message, string recovery) =>
        new(DevAppInstanceErrorCodes.AssignmentInvalid, message, recovery) { AppId = appId, Slot = slot };
}

/// <summary>
/// Holds the per-application lifetime lease of a running development instance.
/// </summary>
/// <example>
/// <code>
/// using var lease = DevAppInstanceLease.Acquire(context, TimeSpan.FromSeconds(5));
/// </code>
/// </example>
public sealed class DevAppInstanceLease : IDisposable
{
    private readonly FileStream stream;

    private DevAppInstanceLease(FileStream stream, string path)
    {
        this.stream = stream;
        this.Path = path;
    }

    /// <summary>Gets the lease file path.</summary>
    public string Path { get; }

    /// <summary>
    /// Acquires the lease of an active context.
    /// </summary>
    /// <param name="context">The active context.</param>
    /// <param name="timeout">The retry window.</param>
    /// <param name="onRetry">An optional callback per failed attempt.</param>
    /// <returns>The lease.</returns>
    /// <exception cref="DevAppInstanceException">Thrown with <c>instance_running</c> when another process holds the lease.</exception>
    public static DevAppInstanceLease Acquire(DevAppInstanceContext context, TimeSpan timeout, Action<int> onRetry = null)
    {
        var infoPath = context.LeasePath + ".json";
        try
        {
            var stream = DevAppInstanceFiles.AcquireLock(context.LeasePath, timeout, DevAppInstanceErrorCodes.InstanceRunning, onRetry);
            DevAppInstanceFiles.WriteJsonAtomic(infoPath, new System.Text.Json.Nodes.JsonObject
            {
                ["version"] = 1,
                ["processId"] = Environment.ProcessId,
                ["processStartedAt"] = GetCurrentProcessStartedAt()?.ToString("O"),
                ["httpUrl"] = context.HttpUrl,
                ["httpsUrl"] = context.HttpsUrl,
            });

            return new DevAppInstanceLease(stream, context.LeasePath);
        }
        catch (DevAppInstanceException ex)
        {
            var holder = TryReadHolder(infoPath);
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.InstanceRunning,
                $"Instance {context.InstanceName} is already running{holder}.",
                "Stop the running instance with 'bdk dev stop', or use the running instance.",
                ex)
            { AppId = context.AppId, Slot = context.Slot };
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.stream.Dispose();

    /// <summary>
    /// Gets the stop-request file that <c>bdk dev stop</c> writes next to a lease file. The lease holder watches it and
    /// shuts down gracefully when it appears.
    /// </summary>
    /// <param name="leasePath">The lease file path.</param>
    /// <returns>The stop-request file path.</returns>
    /// <example>
    /// <code>
    /// File.WriteAllText(DevAppInstanceLease.GetStopRequestFile(leasePath), "stop");
    /// </code>
    /// </example>
    public static string GetStopRequestFile(string leasePath) => leasePath + ".stop";

    /// <summary>
    /// Gets the operating system start time of the current process, or <c>null</c> when the platform does not expose it.
    /// </summary>
    /// <returns>The start time.</returns>
    public static DateTimeOffset? GetCurrentProcessStartedAt()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();

            return new DateTimeOffset(process.StartTime);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Determines whether another process currently holds a lease file.
    /// </summary>
    /// <param name="leasePath">The lease file path.</param>
    /// <returns><c>true</c> when the lease is held.</returns>
    /// <example>
    /// <code>
    /// var running = DevAppInstanceLease.IsHeld(DevAppInstancePaths.GetLeaseFile(commonDir, worktreeId, "forecasthub"));
    /// </code>
    /// </example>
    public static bool IsHeld(string leasePath)
    {
        if (string.IsNullOrEmpty(leasePath) || !File.Exists(leasePath))
        {
            return false;
        }

        try
        {
            using var probe = new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);

            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Reads the holder information that the lease owner wrote next to the lease file.
    /// </summary>
    /// <param name="leasePath">The lease file path.</param>
    /// <returns>The holder, or <c>null</c> when absent or unreadable.</returns>
    /// <example>
    /// <code>
    /// var holder = DevAppInstanceLease.ReadHolder(leasePath);
    /// Console.WriteLine(holder?.ProcessId);
    /// </code>
    /// </example>
    public static DevAppInstanceLeaseHolder ReadHolder(string leasePath)
    {
        try
        {
            var node = DevAppInstanceFiles.ReadJsonObject(leasePath + ".json");
            if (node?["processId"] is null)
            {
                return null;
            }

            return new DevAppInstanceLeaseHolder(
                node["processId"].GetValue<int>(),
                DateTimeOffset.TryParse(node["processStartedAt"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startedAt) ? startedAt : null,
                node["httpUrl"]?.GetValue<string>(),
                node["httpsUrl"]?.GetValue<string>());
        }
        catch (Exception ex) when (ex is DevAppInstanceException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static string TryReadHolder(string infoPath)
    {
        try
        {
            var node = DevAppInstanceFiles.ReadJsonObject(infoPath);

            return node is null ? string.Empty : $" (pid={node["processId"]}, url={node["httpsUrl"]})";
        }
        catch (Exception ex) when (ex is DevAppInstanceException or IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// The process that holds a development instance lease, as recorded next to the lease file.
/// </summary>
/// <param name="ProcessId">The process ID.</param>
/// <param name="ProcessStartedAt">The operating system start time of the process, when recorded.</param>
/// <param name="HttpUrl">The HTTP URL of the instance.</param>
/// <param name="HttpsUrl">The HTTPS URL of the instance.</param>
/// <example>
/// <code>
/// var holder = DevAppInstanceLease.ReadHolder(leasePath);
/// </code>
/// </example>
public sealed record DevAppInstanceLeaseHolder(int ProcessId, DateTimeOffset? ProcessStartedAt, string HttpUrl, string HttpsUrl)
{
    /// <summary>
    /// Determines whether the recorded process still runs with the recorded start time, so a reused process ID is refused.
    /// </summary>
    /// <returns><c>true</c> when the same process still runs.</returns>
    public bool IsSameProcessRunning()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(this.ProcessId);
            if (process.HasExited)
            {
                return false;
            }

            return this.ProcessStartedAt is null ||
                (new DateTimeOffset(process.StartTime) - this.ProcessStartedAt.Value).Duration() < TimeSpan.FromSeconds(1);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return false;
        }
    }
}
