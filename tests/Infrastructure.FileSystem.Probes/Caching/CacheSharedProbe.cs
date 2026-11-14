// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Diagnostics;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks advanced-cache source sharing across independent local processes or an explicitly configured pair of SMB/NFS hosts.</summary>
/// <remarks>Local cases use the Local profile. Remote cases reuse the existing qualification configuration and independent host barriers. Disconnect, reconnect, mixed-kernel placement and power-loss qualification remain explicit external procedures.</remarks>
/// <example><code>cache-shared --profile smb-profile --role controller --run-id RUN_ID</code></example>
public static class CacheSharedProbe
{
    private static readonly string[] LocalContracts = ["authority-outage", "owner-loss", "hot-invalidation", "tracked-invalidation", "tracked-clear", "reader-quota", "clear-publication", "unretained-handoff", "source-departure"];

    /// <summary>Executes one bounded advanced cache contract through two independent local application-host processes.</summary>
    /// <param name="workspace">An existing absolute marked probe workspace under the operating-system temporary directory.</param>
    /// <param name="contract">One explicit local authority, ownership, hot, tracked-operation, reader, publication, or handoff contract.</param>
    /// <returns>The two distinct observed operating-system process identities, after their exact contract outcomes are verified.</returns>
    /// <remarks>Owner termination and tracked restart advance UTC validity only after the old process has actually exited. This does not qualify a share or another host.</remarks>
    /// <example><code>var processes = await CacheSharedProbe.RunLocalContractAsync(markedWorkspace, "hot-invalidation");</code></example>
    public static async Task<IReadOnlyList<int>> RunLocalContractAsync(string workspace, string contract)
    {
        ValidateLocalWorkspace(workspace);
        if (!LocalContracts.Contains(contract, StringComparer.Ordinal)) { throw new ArgumentException("Unknown local cache contract.", nameof(contract)); }

        var directory = Path.Combine(workspace, "cache-contract-" + contract);
        Directory.CreateDirectory(directory);
        CacheProviderOptions options;
        await using (var bootstrap = CreateContractServices(directory, contract))
        {
            await InitializeContractAsync(bootstrap, default);
            options = bootstrap.GetRequiredService<CacheCatalog>().Providers["files"].NativeOptions;
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var workers = new[]
        {
            ProductionProcessProbe.Start(workspace, "cache-local-case-" + contract + "-0", LocalWorkerRuntimeConfig()),
            ProductionProcessProbe.Start(workspace, "cache-local-case-" + contract + "-1", LocalWorkerRuntimeConfig())
        };
        try { return await CoordinateContractAsync(directory, contract, options, workers, deadline.Token); }
        finally
        {
            foreach (var worker in workers)
            {
                if (!worker.HasExited) { worker.Kill(true); await worker.WaitForExitAsync(); }

                worker.Dispose();
            }
        }
    }

    /// <summary>Coordinates the existing two-actor contract without assuming that both actors belong to this host.</summary>
    /// <param name="directory">The marked contract directory shared by the actors.</param>
    /// <param name="contract">One supported contract name.</param>
    /// <param name="options">The exact native profile used by both actors.</param>
    /// <param name="workers">Actor-zero's local process and actor-one's process, or null when its peer host supervises it.</param>
    /// <param name="cancellationToken">The unchanged overall run deadline.</param>
    /// <returns>The observed process identities, ordered by actor.</returns>
    /// <example><code>await CacheSharedProbe.CoordinateContractAsync(directory, contract, options, actors, token);</code></example>
    public static async Task<IReadOnlyList<int>> CoordinateContractAsync(string directory, string contract, CacheProviderOptions options,
        Process[] workers, CancellationToken cancellationToken)
    {
        for (var actor = 0; actor < workers.Length; actor++) { await WaitLocalAsync(Signal(directory, "ready", actor), workers[actor], cancellationToken); }

        if (contract == "authority-outage")
        {
            using var authority = await FileSystemAuthority.AcquireAsync(new FileSystemCacheOptions(options).AuthorityOptions,
                Path.Combine(options.RootPath, "ledger"), "authority", false, cancellationToken);
            if (authority is null) { throw new InvalidDataException("The parent did not hold actual root-ledger authority."); }

            Write(Signal(directory, "start"), new { contract });
            for (var actor = 0; actor < workers.Length; actor++) { await WaitLocalAsync(Signal(directory, "result", actor), workers[actor], cancellationToken); }
        }
        else
        {
            Write(Signal(directory, "start"), new { contract });
            if (contract == "owner-loss")
            {
                await WaitLocalAsync(Signal(directory, "source", 0), workers[0], cancellationToken);
                workers[0].Kill(true);
                await workers[0].WaitForExitAsync(cancellationToken);
                // The OS has confirmed termination before the test advances the old declared source lease.
                Write(Signal(directory, "advance-clock"), new { terminatedProcess = workers[0].Id, validityAdvanceSeconds = 60 });
                Write(Signal(directory, "replacement-start"), new { contract });
            }
            else if (contract is "tracked-invalidation" or "tracked-clear")
            {
                await WaitLocalAsync(Signal(directory, "checkpoint", 0), workers[0], cancellationToken);
                workers[0].Kill(true);
                await workers[0].WaitForExitAsync(cancellationToken);
                Write(Signal(directory, "advance-clock"), new { terminatedProcess = workers[0].Id, validityAdvanceSeconds = 60 });
                Write(Signal(directory, "replacement-start"), new { contract });
            }
            else if (contract == "unretained-handoff")
            {
                foreach (var phase in new[] { "typed", "binary-rejected", "binary-stored" })
                {
                    await WaitLocalAsync(Signal(directory, "source-" + phase, 0), workers[0], cancellationToken);
                    Write(Signal(directory, "peer-start-" + phase), new { phase });
                    await WaitLocalAsync(Signal(directory, "interest-" + phase, 1), workers[1], cancellationToken);
                    Write(Signal(directory, "release-" + phase), new { phase });
                    for (var actor = 0; actor < workers.Length; actor++) { await WaitLocalAsync(Signal(directory, "phase-" + phase, actor), workers[actor], cancellationToken); }

                    if (phase == "binary-stored")
                    {
                        using var first = JsonDocument.Parse(await File.ReadAllBytesAsync(Signal(directory, "phase-" + phase, 0), cancellationToken));
                        using var second = JsonDocument.Parse(await File.ReadAllBytesAsync(Signal(directory, "phase-" + phase, 1), cancellationToken));
                        var version = first.RootElement.GetProperty("version").GetString();
                        if (string.IsNullOrEmpty(version) || version != second.RootElement.GetProperty("version").GetString())
                        {
                            throw new InvalidDataException("Independent binary readers did not acquire the same confirmed stored version.");
                        }
                    }
                }
            }
            else if (contract == "source-departure")
            {
                await WaitLocalAsync(Signal(directory, "source", 0), workers[0], cancellationToken);
                Write(Signal(directory, "peer-start"), new { contract });
                await WaitLocalAsync(Signal(directory, "interest", 1), workers[1], cancellationToken);
                Write(Signal(directory, "cancel-owner"), new { contract });
                await WaitLocalAsync(Signal(directory, "owner-left", 0), workers[0], cancellationToken);
                Write(Signal(directory, "release"), new { contract });
            }
        }

        var killed = contract is "owner-loss" or "tracked-invalidation" or "tracked-clear";
        for (var actor = killed ? 1 : 0; actor < workers.Length; actor++)
        {
            if (workers[actor] is null) { await WaitLocalAsync(Signal(directory, "result", actor), null, cancellationToken); continue; }

            await workers[actor].WaitForExitAsync(cancellationToken);
            if (workers[actor].ExitCode != 0) { throw new InvalidDataException("A local application host rejected contract " + contract + ", actor " + actor); }
        }

        var identities = new List<int>();
        var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var actor = 0; actor < workers.Length; actor++)
        {
            using var ready = JsonDocument.Parse(await File.ReadAllBytesAsync(Signal(directory, "ready", actor), cancellationToken));
            var processId = ready.RootElement.GetProperty("processId").GetInt32();
            var host = ready.RootElement.GetProperty("host").GetString();
            if (workers[actor] is not null && processId != workers[actor].Id || !observed.Add(host + "/" + processId)) { throw new InvalidDataException("The local contract did not use distinct actual OS processes."); }

            identities.Add(processId);
            if (killed && actor == 0) { continue; }

            using var result = JsonDocument.Parse(await File.ReadAllBytesAsync(Signal(directory, "result", actor), cancellationToken));
            if (result.RootElement.GetProperty("processId").GetInt32() != processId) { throw new InvalidDataException("The local actor's completion identity changed."); }

            var expected = actor == 0 && contract is ("clear-publication" or "source-departure") ? 1 :
                actor == 0 && contract == "unretained-handoff" ? 3 : actor == 1 && contract is ("owner-loss" or "unretained-handoff") ? 1 : 0;
            if (result.RootElement.GetProperty("sourceCallbacks").GetInt32() != expected) { throw new InvalidDataException("Unexpected actual source count for " + contract); }
        }

        return identities.ToArray();
    }

    /// <summary>Runs one explicitly selected advanced local contract actor through ordinary application-cache APIs.</summary>
    /// <param name="workspace">The parent's absolute marked workspace.</param>
    /// <param name="scenario">The controlled cache-local-case contract identifier followed by actor zero or one.</param>
    /// <param name="accessKind">The validated authority protocol, defaulting to Local.</param>
    /// <returns>Zero after that actor's observable contract assertions pass.</returns>
    /// <example><code>await CacheSharedProbe.LocalContractWorkerAsync(workspace, "cache-local-case-hot-invalidation-1");</code></example>
    public static async Task<int> LocalContractWorkerAsync(string workspace, string scenario, string accessKind = "Local")
    {
        ValidateLocalWorkspace(workspace);
        var value = scenario.StartsWith("cache-local-case-", StringComparison.Ordinal) ? scenario[17..] : "";
        var separator = value.LastIndexOf('-');
        if (separator < 0 || !int.TryParse(value[(separator + 1)..], out var actor) || actor is < 0 or > 1 ||
            !LocalContracts.Contains(value[..separator], StringComparer.Ordinal)) { throw new InvalidDataException("Unknown local contract actor."); }

        var contract = value[..separator];
        var directory = Path.Combine(workspace, "cache-contract-" + contract);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var services = CreateContractServices(directory, contract, accessKind);
        var native = await InitializeContractAsync(services, deadline.Token);
        Required(await Items(services).GetAsync("ready", deadline.Token), "typed readiness");
        Required(await Binary(services).OpenReadAsync("ready", cancellationToken: deadline.Token), "binary readiness");
        Write(Signal(directory, "ready", actor), new { processId = Environment.ProcessId, host = Environment.MachineName });
        await WaitAsync(Signal(directory, "start"), deadline.Token);
        var calls = contract switch
        {
            "authority-outage" => await AuthorityOutageAsync(services, deadline.Token),
            "owner-loss" => await OwnerLossAsync(services, directory, actor, deadline.Token),
            "hot-invalidation" => await HotInvalidationAsync(services, directory, actor, deadline.Token),
            "tracked-invalidation" or "tracked-clear" => await TrackedRestartAsync(services, native, directory, contract == "tracked-clear", actor, deadline.Token),
            "reader-quota" => await ReaderQuotaAsync(services, native, directory, actor, deadline.Token),
            "clear-publication" => await ClearPublicationAsync(services, directory, actor, deadline.Token),
            "unretained-handoff" => await UnretainedHandoffAsync(services, native, directory, actor, deadline.Token),
            "source-departure" => await SourceDepartureAsync(services, native, directory, actor, deadline.Token),
            _ => throw new InvalidDataException("Unknown local contract.")
        };
        Write(Signal(directory, "result", actor), new { processId = Environment.ProcessId, sourceCallbacks = calls, contract });
        return 0;
    }

    /// <summary>Starts two or four independent local process hosts and holds their cold source work until the configured ownership scope is proven.</summary>
    /// <param name="workspace">An isolated absolute scratch directory containing the existing probe ownership marker.</param>
    /// <param name="nodes">Exactly two or four local operating-system processes sharing the same native root.</param>
    /// <param name="scope">NodeLocal for one callback per process, or SharedStore for exactly one callback across every process.</param>
    /// <returns>The actual callback count for each process, ordered by its actor number.</returns>
    /// <remarks>Each process issues four ordinary GetOrLoad calls. This is local multi-process evidence and does not qualify SMB/NFS or independent hosts.</remarks>
    /// <example><code>var counts = await CacheSharedProbe.RunLocalAsync(markedWorkspace, 4, CacheCoordinationScope.SharedStore);</code></example>
    public static async Task<IReadOnlyList<int>> RunLocalAsync(string workspace, int nodes, CacheCoordinationScope scope)
    {
        ValidateLocalWorkspace(workspace);
        if (nodes is not (2 or 4) || scope is not (CacheCoordinationScope.NodeLocal or CacheCoordinationScope.SharedStore))
        {
            throw new ArgumentOutOfRangeException(nameof(nodes), "Use two or four processes and an ordinary local/shared-store coordination scope.");
        }

        var scenario = $"cache-local-{scope}-{nodes}";
        var directory = Path.Combine(workspace, scenario);
        Directory.CreateDirectory(directory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using (var bootstrap = CreateLocalServices(directory, scope)) { await InitializeLocalAsync(bootstrap, deadline.Token); }

        var workers = new List<Process>();
        try
        {
            for (var actor = 0; actor < nodes; actor++) { workers.Add(ProductionProcessProbe.Start(workspace, scenario + "-" + actor, LocalWorkerRuntimeConfig())); }

            for (var actor = 0; actor < nodes; actor++) { await WaitLocalAsync(Path.Combine(directory, $"ready-{actor}.json"), workers[actor], deadline.Token); }

            Write(Path.Combine(directory, "start.json"), new { nodes, scope = scope.ToString() });
            await WaitLocalAsync(Path.Combine(directory, "source-0.json"), workers[0], deadline.Token);
            for (var actor = 1; actor < nodes; actor++)
            {
                var signal = scope == CacheCoordinationScope.SharedStore ? $"interest-{actor}.json" : $"source-{actor}.json";
                await WaitLocalAsync(Path.Combine(directory, signal), workers[actor], deadline.Token);
            }

            Write(Path.Combine(directory, "release.json"), new { nodes, scope = scope.ToString() });
            foreach (var worker in workers)
            {
                await worker.WaitForExitAsync(deadline.Token);
                if (worker.ExitCode != 0) { throw new InvalidDataException("An independent local cache host rejected its actual API result or source-count contract."); }
            }

            var callbacks = new List<int>();
            var processIds = new HashSet<int>();
            for (var actor = 0; actor < nodes; actor++)
            {
                using var result = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, $"result-{actor}.json"), deadline.Token));
                var count = result.RootElement.GetProperty("sourceCallbacks").GetInt32();
                if (!processIds.Add(result.RootElement.GetProperty("processId").GetInt32()) || count != (scope == CacheCoordinationScope.SharedStore && actor != 0 ? 0 : 1))
                {
                    throw new InvalidDataException("Distinct local process identities or the exact configured source-count contract were not proven.");
                }

                callbacks.Add(count);
            }

            return callbacks;
        }
        finally
        {
            foreach (var worker in workers)
            {
                if (!worker.HasExited) { worker.Kill(true); await worker.WaitForExitAsync(); }

                worker.Dispose();
            }
        }
    }

    /// <summary>Runs one bounded independent local application-cache host selected by the existing worker dispatcher.</summary>
    /// <param name="workspace">The parent's marked isolated scratch directory.</param>
    /// <param name="scenario">The parent-generated cache-local scope, process count, and actor identity.</param>
    /// <param name="accessKind">The validated authority protocol, defaulting to Local.</param>
    /// <returns>Zero only after all four public API requests return the expected retained value and exact local callback count.</returns>
    /// <example><code>await CacheSharedProbe.LocalWorkerAsync(workspace, "cache-local-SharedStore-4-1");</code></example>
    public static async Task<int> LocalWorkerAsync(string workspace, string scenario, string accessKind = "Local")
    {
        ValidateLocalWorkspace(workspace);
        var parts = scenario.StartsWith("cache-local-", StringComparison.Ordinal) ? scenario[12..].Split('-') : [];
        if (parts.Length != 3 || !Enum.TryParse<CacheCoordinationScope>(parts[0], out var scope) ||
            scope is not (CacheCoordinationScope.NodeLocal or CacheCoordinationScope.SharedStore) ||
            !int.TryParse(parts[1], out var nodes) || nodes is not (2 or 4) || !int.TryParse(parts[2], out var actor) || actor < 0 || actor >= nodes)
        {
            throw new InvalidDataException("Invalid bounded local cache worker scenario.");
        }

        var directory = Path.Combine(workspace, $"cache-local-{scope}-{nodes}");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(80));
        await using var services = CreateLocalServices(directory, scope, accessKind);
        var native = await InitializeLocalAsync(services, deadline.Token);
        var cache = services.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        Write(Path.Combine(directory, $"ready-{actor}.json"), new { processId = Environment.ProcessId, host = Environment.MachineName });
        await WaitAsync(Path.Combine(directory, "start.json"), deadline.Token);
        CacheOwnershipRequest interest = null;
        CacheOwnershipObservation joined = null;
        if (scope == CacheCoordinationScope.SharedStore && actor != 0)
        {
            await WaitAsync(Path.Combine(directory, "source-0.json"), deadline.Token);
            interest = new(new("probe", "items", "1", "shared-cold-key"), "local-peer-" + actor, Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow.AddSeconds(70), TimeSpan.FromSeconds(30));
            var acquired = await native.AcquireOwnershipAsync(interest, deadline.Token);
            if (acquired.IsFailure || acquired.Value.State != CacheOwnershipState.Pending)
            {
                throw new InvalidDataException("The independent local peer did not confirm the owner's shared flight interest.");
            }

            joined = acquired.Value;
        }

        var calls = 0;
        CacheLoadDelegate<string> source = async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            if (scope == CacheCoordinationScope.SharedStore && actor != 0) { throw new InvalidDataException("An independent shared peer invoked a duplicate source callback."); }

            Write(Path.Combine(directory, $"source-{actor}.json"), new { processId = Environment.ProcessId });
            await WaitAsync(Path.Combine(directory, "release.json"), token);
            return Result<CacheLoadResult<string>>.Success(new("shared-value"));
        };
        var pending = Enumerable.Range(0, 4).Select(_ => cache.GetOrLoadAsync(new("shared-cold-key"), source, deadline.Token).AsTask()).ToArray();
        if (joined is not null) { Write(Path.Combine(directory, $"interest-{actor}.json"), new { processId = Environment.ProcessId, state = joined.State.ToString() }); }

        try
        {
            var completed = await Task.WhenAll(pending);
            if (completed.Any(result => result.IsFailure || !result.Value.Found || result.Value.Value != "shared-value") || calls != (scope == CacheCoordinationScope.SharedStore && actor != 0 ? 0 : 1))
            {
                throw new InvalidDataException("The independent process's actual application-cache API calls or source counts failed: callbacks=" + calls +
                    "; errors=" + string.Join(',', completed.SelectMany(result => result.Errors).OfType<CacheError>().Select(error => error.Code)));
            }

            Write(Path.Combine(directory, $"result-{actor}.json"), new { processId = Environment.ProcessId, sourceCallbacks = calls, requests = completed.Length });
            return 0;
        }
        finally
        {
            if (joined is not null)
            {
                var released = await native.ReleaseInterestAsync(interest.Identity, joined.FlightId, interest.InterestId, default);
                if (released.IsFailure) { throw new InvalidDataException("The independent local peer could not release its confirmed manual fixture interest."); }
            }
        }
    }

    /// <summary>Coordinates one controller source callback and a source-free peer request using the real shared provider.</summary>
    /// <param name="evidence">The existing local evidence directory.</param>
    /// <param name="runId">The same explicit generated run identity used on both hosts.</param>
    /// <param name="arguments">The configured profile, controller/peer role, and explicit run identity.</param>
    /// <returns>Zero for the actual two-host case, two when qualification is unavailable, or one through the existing CLI error boundary.</returns>
    public static async Task<int> RunAsync(string evidence, string runId, IReadOnlyDictionary<string, string> arguments)
    {
        if (arguments.ContainsKey("--role") && (arguments.GetValueOrDefault("--local") == "true" || arguments.ContainsKey("--contract") || arguments.ContainsKey("--nodes")))
        {
            return await CacheSharedContractProbe.RunAsync(evidence, runId, arguments);
        }

        if (arguments.GetValueOrDefault("--local") == "true")
        {
            var contract = arguments.GetValueOrDefault("--contract");
            if (!LocalContracts.Contains(contract, StringComparer.Ordinal)) { throw new InvalidDataException("Select an explicit local cache contract."); }

            var local = ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-local-" + contract, true);
            var processes = await RunLocalContractAsync(local, contract);
            ProbeResult.Write(Path.Combine(evidence, "cache-shared-local.json"), new { runId, contract, processes, workspace = local,
                scope = "Local OS TEMP, distinct application-host processes. No network-share, independent-host, or power-loss qualification." });
            return 0;
        }

        if (Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_TESTS") != "1")
        {
            ProbeResult.Write(Path.Combine(evidence, "cache-shared.json"), new { status = "unavailable", runId,
                reason = "Set BDK_FILESYSTEM_SHARE_TESTS=1 and provide the existing external BDK_FILESYSTEM_QUALIFICATION_CONFIG. No share was accessed." });
            return 2;
        }

        var configuration = QualificationConfiguration.LoadConfiguration(Environment.GetEnvironmentVariable("BDK_FILESYSTEM_QUALIFICATION_CONFIG"));
        var os = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Unsupported";
        var profileId = arguments.GetValueOrDefault("--profile");
        var profile = configuration.Profiles.SingleOrDefault(profile => profile.Id == profileId && profile.HostOs == os)
            ?? throw new InvalidDataException("A matching configured native OS/share profile is required.");
        RemoteFileSystemProbe.Validate(profile);
        var role = arguments.GetValueOrDefault("--role");
        if (role is not ("controller" or "peer") || !arguments.ContainsKey("--run-id")) { throw new InvalidDataException("Use explicit controller/peer roles and the same --run-id on both hosts."); }

        var expectedHost = role == "controller" ? profile.ReferenceHostId : profile.PeerHostId;
        if (!string.Equals(Environment.MachineName, expectedHost, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("This machine does not match the configured qualification role."); }

        if (!profile.Environment.TryGetValue("sharedProfileId", out var group) || !QualificationConfiguration.SafeId(group)) { throw new InvalidDataException("The shared profile requires environment.sharedProfileId."); }

        var workspaceProfile = "cache-" + FileSystemStore.Hash([group])[..32];
        var workspace = Path.Combine(profile.ScratchRoot, "bdk-filesystem-probes", runId, workspaceProfile);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        if (role == "controller") { ProbeWorkspace.Open(profile.ScratchRoot, runId, workspaceProfile, true); }
        else { await WaitAsync(Path.Combine(workspace, "run.json"), deadline.Token); ProbeWorkspace.Open(profile.ScratchRoot, runId, workspaceProfile, false); }

        var kernelId = RemoteFileSystemProbe.KernelIdentity();
        Write(Path.Combine(workspace, role + ".ready.json"), new { runId, host = Environment.MachineName, os, kernelId, profile.AccessKind });
        var other = role == "controller" ? "peer" : "controller";
        await WaitAsync(Path.Combine(workspace, other + ".ready.json"), deadline.Token);
        using (var peer = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(workspace, other + ".ready.json"), deadline.Token)))
        {
            if (peer.RootElement.GetProperty("runId").GetString() != runId || string.Equals(peer.RootElement.GetProperty("host").GetString(), Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
                kernelId is not null && peer.RootElement.GetProperty("kernelId").ValueKind == JsonValueKind.String && peer.RootElement.GetProperty("kernelId").GetString() == kernelId)
            {
                throw new InvalidDataException("The shared probe requires distinct configured host identities and distinct Linux kernel boot identities.");
            }
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithFileSystemProvider().WithOptions(options => options.With(x => x.Namespace, "probe"))
            .WithProvider("files", provider => provider.With(x => x.Kind, "FileSystem").With(x => x.RootPath, Path.Combine(workspace, "store")).With(x => x.AccessKind, profile.AccessKind))
            .WithValueContract<string>("text").WithCollection<string>("items", collection => collection.With(x => x.Provider, "files")
                .With(x => x.Loading.CoordinationScope, CacheCoordinationScope.SharedStore).With(x => x.Loading.SourceTimeout, TimeSpan.FromMinutes(2)).With(x => x.Loading.WaitTimeout, TimeSpan.FromMinutes(2)));
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var calls = 0;
        Result<CacheLookup<string>> result;
        if (role == "controller")
        {
            result = await cache.GetOrLoadAsync(new("shared-cold-key"), async (_, token) =>
            {
                Interlocked.Increment(ref calls);
                Write(Path.Combine(workspace, "owner.entered.json"), new { runId });
                await WaitAsync(Path.Combine(workspace, "peer.interested.json"), token);
                return Result<CacheLoadResult<string>>.Success(new("shared-value"));
            }, deadline.Token);
        }
        else
        {
            await WaitAsync(Path.Combine(workspace, "owner.entered.json"), deadline.Token);
            var native = provider.GetRequiredService<CacheProviderRegistry>().Get("files");
            var binding = provider.GetRequiredService<CacheCatalog>().Collections["items"];
            var ready = await native.InitializeAsync([new("probe", binding.Name, binding.Options.ContractVersion, binding.Contract.Alias, binding.Options.Dimensions, binding.Options)], deadline.Token);
            if (ready.IsFailure) { throw new InvalidDataException("The peer's production provider did not initialize the shared contract."); }

            var interest = new CacheOwnershipRequest(new("probe", "items", "1", "shared-cold-key"), "probe-peer", Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddMinutes(2), TimeSpan.FromSeconds(30));
            var joined = await native.AcquireOwnershipAsync(interest, deadline.Token);
            if (joined.IsFailure || joined.Value.State != CacheOwnershipState.Pending) { throw new InvalidDataException("The remote peer did not confirm the controller's shared ownership."); }

            var pending = cache.GetOrLoadAsync(new("shared-cold-key"), (_, _) =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "A shared peer invoked a duplicate callback.")));
            }, deadline.Token).AsTask();

            Write(Path.Combine(workspace, "peer.interested.json"), new { runId });
            try { result = await pending; }
            finally { await native.ReleaseInterestAsync(interest.Identity, joined.Value.FlightId, interest.InterestId, default); }
        }

        if (result.IsFailure || !result.Value.Found || result.Value.Value != "shared-value" || calls != (role == "controller" ? 1 : 0))
        {
            throw new InvalidDataException("The actual shared provider source-count or retained result contract failed.");
        }

        Write(Path.Combine(workspace, role + ".result.json"), new { runId, role, passed = true, sourceCallbacks = calls });
        if (role == "controller") { await WaitAsync(Path.Combine(workspace, "peer.result.json"), deadline.Token); }

        ProbeResult.Write(Path.Combine(evidence, "cache-shared.json"), new { status = "passed", runId, profileId, role, sourceCallbacks = calls,
            actual = "Verified mounted SMB/NFS profile, distinct configured host identities, one controller source callback, source-free peer, confirmed shared interest and retained result.",
            unavailable = new[] { "Disconnect/reconnect fault injection was not performed by this command.", "Power-loss and independent physical/VM placement require external qualification.", "Only the invoked client/server/mount/durability profile is evidence; no matrix-wide qualification is inferred." } });
        return 0;
    }

    private static async Task WaitAsync(string path, CancellationToken token)
    {
        while (!File.Exists(path)) { await Task.Delay(50, token); }
    }

    private static async Task<int> AuthorityOutageAsync(ServiceProvider services, CancellationToken token)
    {
        var calls = 0;
        var result = await Items(services).GetOrLoadAsync(new("outage"), (_, _) =>
        {
            calls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("forbidden")));
        }, token);
        if (!Unavailable(result) || calls != 0) { throw new InvalidDataException("Unavailable native authority started or downgraded shared source work."); }

        return calls;
    }

    private static async Task<int> OwnerLossAsync(ServiceProvider services, string directory, int actor, CancellationToken token)
    {
        if (actor == 0)
        {
            await Items(services).GetOrLoadAsync(new("owner-killed"), async (_, owned) =>
            {
                Write(Signal(directory, "source", 0), new { processId = Environment.ProcessId, sourceCallbacks = 1 });
                await WaitAsync(Signal(directory, "never-release"), owned);
                return Result<CacheLoadResult<string>>.Success(new("obsolete"));
            }, token);
            throw new InvalidDataException("The terminated source actor unexpectedly continued.");
        }

        await WaitAsync(Signal(directory, "replacement-start"), token);
        var calls = 0;
        var result = Required(await Items(services).GetOrLoadAsync(new("owner-killed"), (_, _) =>
        {
            calls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("fresh")));
        }, token), "replacement after actual old-process termination");
        if (!result.Found || result.Value != "fresh" || result.StorageOutcome != CacheStorageOutcome.Stored || calls != 1)
        {
            throw new InvalidDataException("The replacement did not acquire and publish its own fresh value.");
        }

        await ExpectAsync(Items(services), "owner-killed", "fresh", token);
        return calls;
    }

    private static async Task<int> HotInvalidationAsync(ServiceProvider services, string directory, int actor, CancellationToken token)
    {
        var items = Items(services);
        if (actor == 0)
        {
            Stored(await items.SetAsync(new("hot"), "old", cancellationToken: token));
            Write(Signal(directory, "old"), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "warm", 1), token);
            Stored(await items.SetAsync(new("hot"), "new", cancellationToken: token));
            Write(Signal(directory, "new"), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "new-read", 1), token);
            var options = services.GetRequiredService<CacheCatalog>().Providers["files"].NativeOptions;
            using (var authority = await FileSystemAuthority.AcquireAsync(new FileSystemCacheOptions(options).AuthorityOptions,
                Path.Combine(options.RootPath, "ledger"), "authority", false, token))
            {
                if (authority is null) { throw new InvalidDataException("The hot validation outage did not hold real authority."); }

                Write(Signal(directory, "outage"), new { processId = Environment.ProcessId });
                await WaitAsync(Signal(directory, "outage-read", 1), token);
            }

            if ((await items.RemoveAsync("hot", cancellationToken: token)).IsFailure) { throw new InvalidDataException("The remote hot removal failed."); }

            Write(Signal(directory, "removed"), new { processId = Environment.ProcessId });
        }
        else
        {
            await WaitAsync(Signal(directory, "old"), token);
            for (var read = 0; read < 3; read++) { await ExpectAsync(items, "hot", "old", token); }

            if (services.GetRequiredService<CacheRuntimeResources>().HotResident.Used <= 0) { throw new InvalidDataException("The reader did not actually retain decoded hot data."); }

            Write(Signal(directory, "warm", 1), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "new"), token);
            for (var read = 0; read < 3; read++) { await ExpectAsync(items, "hot", "new", token); }

            Write(Signal(directory, "new-read", 1), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "outage"), token);
            if (!Unavailable(await items.GetAsync("hot", token))) { throw new InvalidDataException("A hot graph escaped failed native validation."); }

            Write(Signal(directory, "outage-read", 1), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "removed"), token);
            if (Required(await items.GetAsync("hot", token), "removed hot lookup").Found) { throw new InvalidDataException("Strict hot reuse ignored another process's removal."); }
        }

        return 0;
    }

    private static async Task<int> TrackedRestartAsync(ServiceProvider services, ICacheStoreProvider native, string directory, bool clear, int actor, CancellationToken token)
    {
        var items = Items(services);
        var binding = services.GetRequiredService<CacheCatalog>().Collections["items"];
        var runner = services.GetRequiredService<CacheOperationRunner>();
        if (actor == 0)
        {
            for (var index = 0; index < 9; index++) { Stored(await items.SetAsync(new("old:" + index), "old", cancellationToken: token)); }

            var submitted = Required(await services.GetRequiredService<CacheOperationService>()
                .SubmitAsync(new(["items"], CacheSelection.Pattern("*"), Clear: clear, Background: true), token), "tracked submit");
            var child = Required(submitted.Single(), "tracked collection child");
            var status = Required(await runner.RunUnitAsync(binding, child.Handle.Id, token), "initial tracked unit");
            if (status.Outcome.Completed) { throw new InvalidDataException("The bounded initial unit did not retain continuation work."); }

            Write(Signal(directory, "checkpoint", 0), new { processId = Environment.ProcessId, operationId = child.Handle.Id, status.Cutoff, status.Outcome.AffectedCount });
            await WaitAsync(Signal(directory, "never-release"), token);
            throw new InvalidDataException("The checkpoint actor unexpectedly survived parent termination.");
        }

        await WaitAsync(Signal(directory, "replacement-start"), token);
        using var checkpoint = JsonDocument.Parse(await File.ReadAllBytesAsync(Signal(directory, "checkpoint", 0), token));
        var id = checkpoint.RootElement.GetProperty("operationId").GetString();
        var cutoff = checkpoint.RootElement.GetProperty("cutoff").GetDateTimeOffset();
        Stored(await items.SetAsync(new("fresh"), "after cutoff", cancellationToken: token));
        var final = await CompleteOperationAsync(services, id, token);
        if (final.Cutoff != cutoff || final.Outcome.AffectedCount != 9) { throw new InvalidDataException("Restart changed the operation cutoff or lost its cumulative effects."); }

        for (var index = 0; index < 9; index++)
        {
            if (Required(await items.GetAsync("old:" + index, token), "old tracked lookup").Found) { throw new InvalidDataException("An old entry survived the resumed operation."); }
        }

        await ExpectAsync(items, "fresh", "after cutoff", token);
        if (Required(await native.GetQuotaAsync(token), "post-operation quota").CurrentEntries != 1) { throw new InvalidDataException("Tracked restart did not preserve the one post-cutoff publication."); }

        return 0;
    }

    private static async Task<int> ReaderQuotaAsync(ServiceProvider services, ICacheStoreProvider native, string directory, int actor, CancellationToken token)
    {
        var binary = Binary(services);
        var original = Enumerable.Range(0, 128).Select(value => (byte)value).ToArray();
        if (actor == 0)
        {
            using (var input = new MemoryStream(original, writable: false)) { Stored(await binary.SetAsync(new("pinned"), input, cancellationToken: token)); }

            Write(Signal(directory, "old"), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "pinned", 1), token);
            using (var input = new MemoryStream(new byte[256], writable: false)) { Stored(await binary.SetAsync(new("pinned"), input, cancellationToken: token)); }

            var quota = Required(await native.GetQuotaAsync(token), "pinned shared quota");
            if (quota.CurrentEntries != 1 || quota.RetiredBytes <= 0) { throw new InvalidDataException("The replaced pinned generation was not separately charged from the single current entry."); }

            Write(Signal(directory, "replaced"), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "reader-done", 1), token);
        }
        else
        {
            await WaitAsync(Signal(directory, "old"), token);
            var opened = Required(await binary.OpenReadAsync("pinned", cancellationToken: token), "old binary snapshot");
            if (!opened.Found) { throw new InvalidDataException("The old binary snapshot was missing."); }

            await using (var reader = opened.Value)
            {
                Write(Signal(directory, "pinned", 1), new { processId = Environment.ProcessId });
                await WaitAsync(Signal(directory, "replaced"), token);
                using var extra = new MemoryStream(new byte[64], writable: false);
                var rejected = await binary.SetAsync(new("other"), extra, cancellationToken: token);
                if (rejected.IsSuccess && rejected.Value.StorageOutcome == CacheStorageOutcome.Stored) { throw new InvalidDataException("Another process bypassed the shared Reject-mode current-entry cap."); }

                using var destination = new MemoryStream();
                await reader.Stream.CopyToAsync(destination, token);
                if (!destination.ToArray().SequenceEqual(original) || Required(await reader.WaitForCompletionAsync(token), "old binary integrity") != CacheReadCompletion.Validated)
                {
                    throw new InvalidDataException("Remote replacement changed or invalidated an already acquired old snapshot.");
                }
            }

            var replacement = Required(await binary.OpenReadAsync("pinned", cancellationToken: token), "new binary snapshot");
            await using (var reader = replacement.Value)
            {
                using var destination = new MemoryStream();
                await reader.Stream.CopyToAsync(destination, token);
                if (!replacement.Found || destination.Length != 256 || Required(await reader.WaitForCompletionAsync(token), "replacement integrity") != CacheReadCompletion.Validated)
                {
                    throw new InvalidDataException("New readers did not acquire the remotely published replacement.");
                }
            }

            if (Required(await binary.OpenReadAsync("other", cancellationToken: token), "rejected key lookup").Found) { throw new InvalidDataException("The rejected extra key became visible."); }

            Write(Signal(directory, "reader-done", 1), new { processId = Environment.ProcessId });
        }

        return 0;
    }

    private static async Task<int> ClearPublicationAsync(ServiceProvider services, string directory, int actor, CancellationToken token)
    {
        var items = Items(services);
        Task<Result<CacheLookup<string>>> pending = null;
        var calls = 0;
        if (actor == 0)
        {
            pending = items.GetOrLoadAsync(new("held"), async (_, owned) =>
            {
                calls++;
                Write(Signal(directory, "source", 0), new { processId = Environment.ProcessId });
                await WaitAsync(Signal(directory, "fresh"), owned);
                return Result<CacheLoadResult<string>>.Success(new("obsolete"));
            }, token).AsTask();
        }

        await WaitAsync(Signal(directory, "source", 0), token);
        var submitted = Required(await services.GetRequiredService<CacheOperationService>()
            .SubmitAsync(new(["items"], CacheSelection.None, Clear: true, Background: true), token), "concurrent clear submit");
        await CompleteOperationAsync(services, Required(submitted.Single(), "clear child").Handle.Id, token);
        Write(Signal(directory, "cleared", actor), new { processId = Environment.ProcessId });
        await WaitAsync(Signal(directory, "cleared", 1 - actor), token);
        if (actor == 1)
        {
            Stored(await items.SetAsync(new("held"), "fresh", cancellationToken: token));
            Write(Signal(directory, "fresh"), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "source-done", 0), token);
        }
        else
        {
            var late = await pending;
            if (late.IsSuccess && late.Value.StorageOutcome == CacheStorageOutcome.Stored) { throw new InvalidDataException("A pre-clear source published after two completed clear boundaries."); }

            await ExpectAsync(items, "held", "fresh", token);
            Write(Signal(directory, "source-done", 0), new { processId = Environment.ProcessId });
        }

        await ExpectAsync(items, "held", "fresh", token);
        return calls;
    }

    private static async Task<CacheProviderOperation> CompleteOperationAsync(ServiceProvider services, string id, CancellationToken token)
    {
        var binding = services.GetRequiredService<CacheCatalog>().Collections["items"];
        var runner = services.GetRequiredService<CacheOperationRunner>();
        for (var unit = 0; unit < 128; unit++)
        {
            var status = Required(await runner.RunUnitAsync(binding, id, token), "tracked continuation unit");
            if (status.State == CacheOperationState.Completed && status.Outcome.Completed) { return status; }

            if (status.State is CacheOperationState.Failed or CacheOperationState.Cancelled) { throw new InvalidDataException("A retained tracked operation failed before convergence."); }
        }

        throw new InvalidDataException("The bounded local operation did not converge within its declared fixture unit limit.");
    }

    private static async Task<int> UnretainedHandoffAsync(ServiceProvider services, ICacheStoreProvider native, string directory, int actor, CancellationToken token)
    {
        var calls = 0;
        foreach (var phase in new[] { "typed", "binary-rejected", "binary-stored" })
        {
            (CacheOwnershipRequest Request, CacheOwnershipObservation Flight)? interest = null;
            if (actor == 1)
            {
                await WaitAsync(Signal(directory, "peer-start-" + phase), token);
                interest = await ConfirmInterestAsync(services, native, phase == "typed" ? "items" : "binary", phase, token);
            }

            string version = null;
            try
            {
                if (phase == "typed")
                {
                    var pending = Items(services).GetOrLoadAsync(new(phase), async (_, owned) =>
                    {
                        calls++;
                        if (actor == 1) { return Result<CacheLoadResult<string>>.Success(new("remote owned graph")); }

                        Write(Signal(directory, "source-" + phase, 0), new { processId = Environment.ProcessId });
                        await WaitAsync(Signal(directory, "release-" + phase), owned);
                        return Result<CacheLoadResult<string>>.Success(new("local graph", new() { SuppressStorage = true }));
                    }, token).AsTask();
                    if (actor == 1)
                    {
                        await WaitAsync(Signal(directory, "actual-interest-" + phase), token);
                        Write(Signal(directory, "interest-" + phase, 1), new { processId = Environment.ProcessId });
                    }

                    var result = await pending;
                    if (actor == 0)
                    {
                        var value = Required(result, "local suppressed typed value");
                        if (!value.Found || value.Value != "local graph" || value.StorageOutcome != CacheStorageOutcome.Suppressed || value.Version is not null)
                        {
                            throw new InvalidDataException("The owner did not receive its explicitly unretained typed value.");
                        }
                    }
                    else
                    {
                        var value = Required(result, "fresh authoritative typed handoff");
                        if (!value.Found || value.Value != "remote owned graph" || value.StorageOutcome != CacheStorageOutcome.Stored || value.Version is null)
                        {
                            throw new InvalidDataException("The remote process received the old graph instead of publishing its own newly authorized source result.");
                        }
                    }

                    if (actor == 0) { await WaitAsync(Signal(directory, "phase-" + phase, 1), token); }

                    await ExpectAsync(Items(services), phase, "remote owned graph", token);
                }
                else
                {
                    var pending = Binary(services).GetOrLoadAsync(new(phase), async (_, output, owned) =>
                    {
                        calls++;
                        if (actor != 0) { throw new InvalidDataException("A remote binary follower executed a duplicate source."); }

                        Write(Signal(directory, "source-" + phase, 0), new { processId = Environment.ProcessId });
                        await WaitAsync(Signal(directory, "release-" + phase), owned);
                        await output.WriteAsync(new byte[] { 1, 2, 3 }, owned);
                        return Result<CacheBinaryLoadResult>.Success(phase == "binary-rejected" ? new(new() { SuppressStorage = true }) : new());
                    }, token).AsTask();
                    if (actor == 1)
                    {
                        await WaitAsync(Signal(directory, "actual-interest-" + phase), token);
                        Write(Signal(directory, "interest-" + phase, 1), new { processId = Environment.ProcessId });
                    }

                    var result = await pending;
                    if (phase == "binary-rejected")
                    {
                        if (result.IsSuccess) { throw new InvalidDataException("Rejected binary publication delivered a success or replay handle."); }

                        if (Required(await Binary(services).OpenReadAsync(phase, cancellationToken: token), "rejected binary lookup").Found)
                        {
                            throw new InvalidDataException("Rejected binary staging became visible.");
                        }
                    }
                    else
                    {
                        var value = Required(result, "confirmed binary handoff");
                        if (!value.Found || value.Version is null || value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("Binary handoff was not backed by confirmed publication."); }

                        version = value.Version.Value.Value;
                        await using var reader = value.Value;
                        using var output = new MemoryStream();
                        await reader.Stream.CopyToAsync(output, token);
                        if (!output.ToArray().SequenceEqual(new byte[] { 1, 2, 3 }) || Required(await reader.WaitForCompletionAsync(token), "binary handoff integrity") != CacheReadCompletion.Validated)
                        {
                            throw new InvalidDataException("An independent binary reader did not return its complete validated published bytes.");
                        }
                    }
                }
            }
            finally
            {
                if (interest is { } joined)
                {
                    var released = await native.ReleaseInterestAsync(joined.Request.Identity, joined.Flight.FlightId, joined.Request.InterestId, default);
                    if (released.IsFailure) { throw new InvalidDataException("The fixture could not release its confirmed extra interest."); }
                }
            }

            Write(Signal(directory, "phase-" + phase, actor), new { processId = Environment.ProcessId, version });
        }

        return calls;
    }

    private static async Task<int> SourceDepartureAsync(ServiceProvider services, ICacheStoreProvider native, string directory, int actor, CancellationToken token)
    {
        var calls = 0;
        if (actor == 0)
        {
            using var leaving = CancellationTokenSource.CreateLinkedTokenSource(token);
            var pending = Items(services).GetOrLoadAsync(new("departure"), async (_, owned) =>
            {
                calls++;
                Write(Signal(directory, "source", 0), new { processId = Environment.ProcessId });
                await WaitAsync(Signal(directory, "release"), owned);
                return Result<CacheLoadResult<string>>.Success(new("remote interest"));
            }, leaving.Token).AsTask();
            await WaitAsync(Signal(directory, "cancel-owner"), token);
            leaving.Cancel();
            try { await pending; throw new InvalidDataException("The cancelled owner caller continued waiting."); }
            catch (OperationCanceledException) when (leaving.IsCancellationRequested) { }

            Write(Signal(directory, "owner-left", 0), new { processId = Environment.ProcessId });
            await WaitAsync(Signal(directory, "remote-done", 1), token);
            await ExpectAsync(Items(services), "departure", "remote interest", token);
        }
        else
        {
            await WaitAsync(Signal(directory, "peer-start"), token);
            var joined = await ConfirmInterestAsync(services, native, "items", "departure", token);
            try
            {
                var pending = Items(services).GetOrLoadAsync(new("departure"), (_, _) =>
                {
                    calls++;
                    throw new InvalidDataException("A remote interested caller restarted the owner's source.");
                }, token).AsTask();
                await WaitAsync(Signal(directory, "actual-interest-departure"), token);
                Write(Signal(directory, "interest", 1), new { processId = Environment.ProcessId });
                var result = Required(await pending, "remote interest after owner caller departure");
                if (!result.Found || result.Value != "remote interest" || result.StorageOutcome != CacheStorageOutcome.Stored)
                {
                    throw new InvalidDataException("Leaving one caller cancelled or duplicated source work still required by the other process.");
                }
            }
            finally
            {
                var released = await native.ReleaseInterestAsync(joined.Request.Identity, joined.Flight.FlightId, joined.Request.InterestId, default);
                if (released.IsFailure) { throw new InvalidDataException("Remote departure interest could not be released."); }
            }

            Write(Signal(directory, "remote-done", 1), new { processId = Environment.ProcessId });
        }

        return calls;
    }

    private static async Task<(CacheOwnershipRequest Request, CacheOwnershipObservation Flight)> ConfirmInterestAsync(ServiceProvider services,
        ICacheStoreProvider native, string collection, string key, CancellationToken token)
    {
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var request = new CacheOwnershipRequest(new("probe", collection, "1", key), "fixture-peer", Guid.NewGuid().ToString("N"), now.AddSeconds(110), TimeSpan.FromSeconds(30));
        var flight = Required(await native.AcquireOwnershipAsync(request, token), "manual fixture interest");
        if (flight.State != CacheOwnershipState.Pending) { throw new InvalidDataException("The peer did not confirm the existing owned native flight."); }

        return (request, flight);
    }

    private static string Signal(string directory, string name, int? actor = null) => Path.Combine(directory, name + (actor.HasValue ? "-" + actor.Value : "") + ".json");

    private static ICacheCollection<string> Items(IServiceProvider services) => services.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

    private static ICacheBinaryCollection Binary(IServiceProvider services) => services.GetRequiredService<IApplicationCache>().GetBinaryCollection("binary");

    private static T Required<T>(Result<T> result, string stage)
    {
        if (result.IsFailure) { throw new InvalidDataException(stage + ": " + string.Join(',', result.Errors.OfType<CacheError>().Select(error => error.Code))); }

        return result.Value;
    }

    private static bool Unavailable<T>(Result<T> result) => result.IsFailure && result.Errors.OfType<CacheError>().Any(error => error.Code == CacheErrorCode.ProviderUnavailable);

    private static void Stored(Result<CacheWriteOutcome> result)
    {
        if (Required(result, "publication").StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("The requested test publication was not confirmed."); }
    }

    private static async Task ExpectAsync(ICacheCollection<string> items, string key, string expected, CancellationToken token)
    {
        var read = Required(await items.GetAsync(key, token), "typed retained value");
        if (!read.Found || read.Value != expected || read.Version is null) { throw new InvalidDataException("The retained typed value or acquired version was incorrect."); }
    }

    /// <summary>Creates the existing bounded application host for an explicit contract and authority protocol.</summary>
    /// <param name="directory">An owned marked scenario directory.</param>
    /// <param name="contract">The supported cache contract.</param>
    /// <param name="accessKind">Local, Smb, or Nfs, selected by the validated controller.</param>
    /// <returns>The independently owned service provider.</returns>
    /// <example><code>await using var host = CreateContractServices(directory, "hot-invalidation", "Local");</code></example>
    public static ServiceProvider CreateContractServices(string directory, string contract, string accessKind = "Local")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new LocalValidityClock(directory));
        var quota = contract == "reader-quota" ? 1L : 1000L;
        services.AddApplicationCaching().WithProviderKind("FileSystem", FileSystemCacheStoreProvider.CapabilitiesFor, (providerServices, name, options) =>
            new LocalObservedProvider(new FileSystemCacheStoreProvider(name, options, providerServices.GetRequiredService<TimeProvider>(),
                providerServices.GetRequiredService<CacheRuntimeResources>(), providerServices.GetRequiredService<CacheCatalog>().Options.Diagnostics), directory))
            .WithOptions(options => options.With(x => x.Namespace, "probe"))
            .WithProvider("files", provider => provider.With(x => x.Kind, "FileSystem").With(x => x.RootPath, Path.Combine(directory, "store")).With(x => x.AccessKind, accessKind)
                .With(x => x.MaxEntries, quota).With(x => x.MaxStorageBytes, 256 * CacheDefaults.MiB))
            .WithValueContract<string>("text", value => value.WithImmutableSharing(text => 64L + (text?.Length ?? 0) * 2L)).WithBinaryContract("raw")
            .Defaults(collection => collection.With(x => x.Provider, "files").With(x => x.Eviction, CacheEvictionMode.Reject)
                .With(x => x.Limits.MaxStorageBytes, 128 * CacheDefaults.MiB).With(x => x.Limits.MaxEntries, quota)
                .With(x => x.Limits.MaxEncodedBytes, 64 * CacheDefaults.KiB).With(x => x.Limits.MaxLogicalBytes, 64 * CacheDefaults.KiB)
                .With(x => x.Limits.MetadataPageSize, 2).With(x => x.Limits.MaxMetadataPageSize, 2)
                .With(x => x.Loading.CoordinationScope, CacheCoordinationScope.SharedStore).With(x => x.Loading.SourceTimeout, TimeSpan.FromSeconds(120))
                .With(x => x.Loading.WaitTimeout, TimeSpan.FromSeconds(120)))
            .WithCollection<string>("items", collection => collection.With(x => x.HotEntries.Enabled, contract == "hot-invalidation")
                .With(x => x.HotEntries.Representation, CacheHotEntryRepresentation.Decoded))
            .WithBinaryCollection("binary");
        return services.BuildServiceProvider();
    }

    private static async Task<ICacheStoreProvider> InitializeContractAsync(ServiceProvider services, CancellationToken token)
    {
        var native = services.GetRequiredService<CacheProviderRegistry>().Get("files");
        var catalog = services.GetRequiredService<CacheCatalog>();
        var definitions = catalog.Collections.Values.Select(binding => new CacheProviderCollection("probe", binding.Name,
            binding.Options.ContractVersion, binding.Contract.Alias, binding.Options.Dimensions, binding.Options)).ToArray();
        var ready = await native.InitializeAsync(definitions, token);
        if (ready.IsFailure) { throw new InvalidDataException("The local contract provider did not initialize."); }

        return native;
    }

    private sealed class LocalValidityClock(string directory) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + (File.Exists(Signal(directory, "advance-clock")) ? TimeSpan.FromMinutes(1) : TimeSpan.Zero);
    }

    private sealed class LocalObservedProvider(ICacheStoreProvider inner, string directory) : CacheStoreProviderBehavior(inner)
    {
        /// <inheritdoc />
        public override async ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default)
        {
            var result = await base.AcquireOwnershipAsync(request, cancellationToken);
            if (result.IsSuccess && result.Value.State == CacheOwnershipState.Pending && Guid.TryParseExact(request.NodeId, "N", out _))
            {
                var path = Signal(directory, "actual-interest-" + request.Identity.Key.ToLowerInvariant());
                if (File.Exists(path)) { throw new InvalidDataException("A local source group acquired duplicate remote observation interests."); }

                Write(path, new { processId = Environment.ProcessId, request.InterestId });
            }

            return result;
        }
    }

    /// <summary>Creates the existing cold-source host for a local or qualified shared authority protocol.</summary>
    /// <param name="directory">An owned marked scenario directory.</param>
    /// <param name="scope">The source coordination scope.</param>
    /// <param name="accessKind">Local, Smb, or Nfs, selected by the validated controller.</param>
    /// <param name="sourceTimeout">An optional explicit source/wait budget for externally controlled faults.</param>
    /// <returns>The independently owned service provider.</returns>
    /// <example><code>await using var host = CreateLocalServices(directory, CacheCoordinationScope.SharedStore, "Local");</code></example>
    public static ServiceProvider CreateLocalServices(string directory, CacheCoordinationScope scope, string accessKind = "Local", TimeSpan? sourceTimeout = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithFileSystemProvider().WithOptions(options => options.With(x => x.Namespace, "probe"))
            .WithProvider("files", provider => provider.With(x => x.Kind, "FileSystem").With(x => x.RootPath, Path.Combine(directory, "store")).With(x => x.AccessKind, accessKind))
            .WithValueContract<string>("text").WithCollection<string>("items", collection => collection.With(x => x.Provider, "files")
                .With(x => x.Loading.CoordinationScope, scope).With(x => x.Loading.SourceTimeout, sourceTimeout ?? TimeSpan.FromSeconds(70)).With(x => x.Loading.WaitTimeout, sourceTimeout ?? TimeSpan.FromSeconds(70)));
        return services.BuildServiceProvider();
    }

    private static async Task<ICacheStoreProvider> InitializeLocalAsync(ServiceProvider services, CancellationToken token)
    {
        var native = services.GetRequiredService<CacheProviderRegistry>().Get("files");
        var binding = services.GetRequiredService<CacheCatalog>().Collections["items"];
        var ready = await native.InitializeAsync([new("probe", binding.Name, binding.Options.ContractVersion, binding.Contract.Alias, binding.Options.Dimensions, binding.Options)], token);
        if (ready.IsFailure) { throw new InvalidDataException("The local process's real filesystem provider did not initialize its declared contract."); }

        return native;
    }

    private static async Task WaitLocalAsync(string path, Process worker, CancellationToken token)
    {
        while (!File.Exists(path))
        {
            if (worker?.HasExited == true) { throw new InvalidDataException("The cache actor exited before its requested barrier."); }

            await Task.Delay(20, token);
        }
    }

    private static void ValidateLocalWorkspace(string workspace)
    {
        if (!Path.IsPathFullyQualified(workspace)) { throw new InvalidDataException("An absolute marked scratch workspace is required."); }

        ProbeWorkspace.RejectLinks(workspace);
        var marker = Path.Combine(workspace, "run.json");
        if (!File.Exists(marker) || !File.ReadAllText(marker).StartsWith("bdk-filesystem-probes/1/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The local process probe ownership marker is missing.");
        }
    }

    private static string LocalWorkerRuntimeConfig()
    {
        var candidate = Path.Combine(Path.GetDirectoryName(typeof(CacheSharedProbe).Assembly.Location), "BridgingIT.DevKit.Infrastructure.UnitTests.runtimeconfig.json");
        return File.Exists(candidate) ? candidate : null;
    }

    private static void Write(string path, object value)
    {
        ProbeResult.Write(path + ".tmp", value);
        FileSystemDurability.FlushFile(path + ".tmp");
        FileSystemDurability.Move(path + ".tmp", path, false, true);
    }
}