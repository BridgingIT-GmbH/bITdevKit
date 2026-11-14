// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Diagnostics;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs the existing cache contract actors under independent controller and peer supervisors.</summary>
/// <remarks>Local role runs exercise the same transport with OS temporary storage. Shared qualification requires an observed SMB/NFS profile and distinct configured hosts.</remarks>
/// <example><code>cache-shared --local true --role controller --contract hot-invalidation --run-id RUN_ID</code></example>
public static class CacheSharedContractProbe
{
    private static readonly string[] Contracts = ["authority-outage", "owner-loss", "hot-invalidation", "tracked-invalidation", "tracked-clear", "reader-quota", "clear-publication", "unretained-handoff", "source-departure", "cold-load", "disconnect-reconnect"];

    /// <summary>Supervises this host's actors and verifies their selected cache contract.</summary>
    /// <param name="evidence">An existing local evidence directory.</param>
    /// <param name="runId">A fresh shared N-format GUID.</param>
    /// <param name="arguments">The explicit local/profile, role, contract, source-scope and process-count options.</param>
    /// <returns>Zero after actual contract completion, or two when shared qualification was not enabled.</returns>
    /// <example><code>await CacheSharedContractProbe.RunAsync(evidence, runId, options);</code></example>
    public static async Task<int> RunAsync(string evidence, string runId, IReadOnlyDictionary<string, string> arguments)
    {
        var local = arguments.GetValueOrDefault("--local") == "true";
        if (!local && Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_TESTS") != "1")
        {
            ProbeResult.Write(Path.Combine(evidence, "cache-shared.json"), new { status = "unavailable", runId, reason = "Shared qualification is not enabled. No share was accessed." });
            return 2;
        }

        var role = arguments.GetValueOrDefault("--role");
        var contract = arguments.GetValueOrDefault("--contract") ?? "cold-load";
        if (local && contract == "disconnect-reconnect")
        {
            ProbeResult.Write(Path.Combine(evidence, "cache-shared.json"), new { status = "unavailable", runId,
                reason = "Disconnect/reconnect requires an actual configured share and external fault acknowledgments; local transport cannot qualify it." });
            return 2;
        }

        var nodes = int.Parse(arguments.GetValueOrDefault("--nodes") ?? "2", System.Globalization.CultureInfo.InvariantCulture);
        if (role is not ("controller" or "peer") || !Contracts.Contains(contract, StringComparer.Ordinal) || nodes is not (2 or 4) ||
            contract != "cold-load" && nodes != 2 || !Guid.TryParseExact(runId, "N", out _))
        {
            throw new InvalidDataException("Use controller/peer, a supported contract, a fresh run id, and two actors; cold-load also supports four.");
        }

        if (!Enum.TryParse<CacheCoordinationScope>(arguments.GetValueOrDefault("--scope") ?? "SharedStore", out var scope) ||
            scope is not (CacheCoordinationScope.SharedStore or CacheCoordinationScope.NodeLocal) || contract != "cold-load" && scope != CacheCoordinationScope.SharedStore)
        {
            throw new InvalidDataException("Select an explicit supported source scope.");
        }

        var scratch = Path.GetTempPath();
        var profileName = "cache-roles-" + contract;
        var accessKind = "Local";
        if (!local)
        {
            var configuration = QualificationConfiguration.LoadConfiguration(Environment.GetEnvironmentVariable("BDK_FILESYSTEM_QUALIFICATION_CONFIG"));
            var os = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Unsupported";
            var profile = configuration.Profiles.SingleOrDefault(value => value.Id == arguments.GetValueOrDefault("--profile") && value.HostOs == os)
                ?? throw new InvalidDataException("Require a matching native share profile.");
            RemoteFileSystemProbe.Validate(profile);
            if (!string.Equals(Environment.MachineName, role == "controller" ? profile.ReferenceHostId : profile.PeerHostId, StringComparison.OrdinalIgnoreCase) ||
                !profile.Environment.TryGetValue("sharedProfileId", out var group) || !QualificationConfiguration.SafeId(group))
            {
                throw new InvalidDataException("This host must match its configured role and shared profile group.");
            }

            scratch = profile.ScratchRoot;
            accessKind = profile.AccessKind;
            profileName = "cache-roles-" + FileSystemStore.Hash([group])[..32];
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(contract == "disconnect-reconnect" ? 12 : 4));
        var token = deadline.Token;
        var workspace = Path.Combine(scratch, "bdk-filesystem-probes", runId, profileName);
        if (role == "controller")
        {
            ProbeWorkspace.Open(scratch, runId, profileName, true);
            var temporary = Path.Combine(workspace, "session-" + Guid.NewGuid().ToString("N") + ".tmp");
            ProbeResult.Write(temporary, new { runId, contract, nodes, scope = scope.ToString(), accessKind });
            FileSystemDurability.FlushFile(temporary);
            FileSystemDurability.Move(temporary, Path.Combine(workspace, "session.json"), false, true);
        }
        else
        {
            await WaitAsync(Path.Combine(workspace, "session.json"), null, token);
            ProbeWorkspace.Open(scratch, runId, profileName, false);
            using var session = Read(Path.Combine(workspace, "session.json"));
            if (session.RootElement.GetProperty("runId").GetString() != runId || session.RootElement.GetProperty("contract").GetString() != contract ||
                session.RootElement.GetProperty("nodes").GetInt32() != nodes || session.RootElement.GetProperty("scope").GetString() != scope.ToString() ||
                session.RootElement.GetProperty("accessKind").GetString() != accessKind)
            {
                throw new InvalidDataException("Both supervisors must select the identical run contract and native profile.");
            }
        }

        Write(Path.Combine(workspace, role + ".host.json"), new { runId, host = Environment.MachineName, kernel = RemoteFileSystemProbe.KernelIdentity(), local });
        await WaitAsync(Path.Combine(workspace, (role == "controller" ? "peer" : "controller") + ".host.json"), null, token);
        using (var other = Read(Path.Combine(workspace, (role == "controller" ? "peer" : "controller") + ".host.json")))
        {
            var kernel = RemoteFileSystemProbe.KernelIdentity();
            if (other.RootElement.GetProperty("runId").GetString() != runId || other.RootElement.GetProperty("local").GetBoolean() != local || !local &&
                (string.Equals(other.RootElement.GetProperty("host").GetString(), Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
                 kernel is not null && other.RootElement.GetProperty("kernel").GetString() == kernel))
            {
                throw new InvalidDataException("Shared qualification requires distinct host/kernel identities and matching local/shared modes.");
            }
        }

        if (contract == "disconnect-reconnect")
        {
            return await CacheSharedFaultProbe.RunAsync(evidence, runId, role, workspace, accessKind, token);
        }

        var scenario = contract == "cold-load" ? $"cache-local-{scope}-{nodes}" : "cache-contract-" + contract;
        var directory = Path.Combine(workspace, scenario);
        CacheProviderOptions nativeOptions = null;
        if (role == "controller")
        {
            Directory.CreateDirectory(directory);
            await using var bootstrap = contract == "cold-load" ? CacheSharedProbe.CreateLocalServices(directory, scope, accessKind) :
                CacheSharedProbe.CreateContractServices(directory, contract, accessKind);
            await InitializeAsync(bootstrap, token);
            nativeOptions = bootstrap.GetRequiredService<CacheCatalog>().Providers["files"].NativeOptions;
            Write(Path.Combine(workspace, "initialized.json"), new { runId });
        }
        else { await WaitAsync(Path.Combine(workspace, "initialized.json"), null, token); }

        var actors = new Process[nodes];
        var first = role == "controller" ? 0 : nodes / 2;
        var end = first + nodes / 2;
        try
        {
            for (var actor = first; actor < end; actor++)
            {
                var workerScenario = contract == "cold-load" ? $"cache-role-{accessKind}-cold-{scope}-{nodes}-{actor}" : $"cache-role-{accessKind}-case-{contract}-{actor}";
                actors[actor] = ProductionProcessProbe.Start(workspace, workerScenario, RuntimeConfig());
            }

            if (role == "controller")
            {
                if (contract == "cold-load") { await CoordinateColdAsync(directory, actors, nodes, scope, token); }
                else { await CacheSharedProbe.CoordinateContractAsync(directory, contract, nativeOptions, actors, token); }

                await WaitAsync(Path.Combine(workspace, "peer.completed.json"), null, token);
                Write(Path.Combine(workspace, "verified.json"), new { runId });
            }
            else
            {
                for (var actor = first; actor < end; actor++)
                {
                    await actors[actor].WaitForExitAsync(token);
                    if (actors[actor].ExitCode != 0) { throw new InvalidDataException("A peer actor rejected the cache contract."); }
                }

                Write(Path.Combine(workspace, "peer.completed.json"), new { runId });
                await WaitAsync(Path.Combine(workspace, "verified.json"), null, token);
            }

            ProbeResult.Write(Path.Combine(evidence, "cache-shared-" + role + ".json"), new
            {
                status = "passed", runId, role, contract, nodes, scope = scope.ToString(), accessKind,
                qualification = local ? "Local OS TEMP role transport and actual child-process contracts" : "Invoked independent-host mounted share profile",
                powerLossTested = false, disconnectTested = false
            });
            return 0;
        }
        finally
        {
            foreach (var actor in actors.Where(value => value is not null))
            {
                if (!actor.HasExited) { actor.Kill(true); await actor.WaitForExitAsync(); }

                actor.Dispose();
            }
        }
    }

    /// <summary>Executes one actor selected by its validated supervisor through the existing worker host.</summary>
    /// <param name="workspace">The marked run directory.</param>
    /// <param name="scenario">A fixed cache-role protocol, source scope/contract and bounded actor identity.</param>
    /// <returns>The actor's actual contract exit code.</returns>
    /// <example><code>await CacheSharedContractProbe.WorkerAsync(workspace, "cache-role-Local-case-hot-invalidation-1");</code></example>
    public static Task<int> WorkerAsync(string workspace, string scenario)
    {
        var value = scenario.StartsWith("cache-role-", StringComparison.Ordinal) ? scenario[11..] : "";
        var separator = value.IndexOf('-');
        if (separator < 0 || value[..separator] is not ("Local" or "Smb" or "Nfs")) { throw new InvalidDataException("Unknown cache actor authority protocol."); }

        var kind = value[..separator];
        value = value[(separator + 1)..];
        if (value.StartsWith("case-", StringComparison.Ordinal)) { return CacheSharedProbe.LocalContractWorkerAsync(workspace, "cache-local-case-" + value[5..], kind); }

        if (value.StartsWith("cold-", StringComparison.Ordinal)) { return CacheSharedProbe.LocalWorkerAsync(workspace, "cache-local-" + value[5..], kind); }

        throw new InvalidDataException("Unknown cache role actor.");
    }

    private static async Task CoordinateColdAsync(string directory, Process[] actors, int nodes, CacheCoordinationScope scope, CancellationToken token)
    {
        for (var actor = 0; actor < nodes; actor++) { await WaitAsync(Path.Combine(directory, $"ready-{actor}.json"), actors[actor], token); }

        Write(Path.Combine(directory, "start.json"), new { nodes, scope = scope.ToString() });
        for (var actor = 0; actor < nodes; actor++)
        {
            var signal = scope == CacheCoordinationScope.SharedStore && actor != 0 ? $"interest-{actor}.json" : $"source-{actor}.json";
            await WaitAsync(Path.Combine(directory, signal), actors[actor], token);
        }

        Write(Path.Combine(directory, "release.json"), new { nodes });
        var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var actor = 0; actor < nodes; actor++)
        {
            await WaitAsync(Path.Combine(directory, $"result-{actor}.json"), actors[actor], token);
            using var result = Read(Path.Combine(directory, $"result-{actor}.json"));
            var processId = result.RootElement.GetProperty("processId").GetInt32();
            using var ready = Read(Path.Combine(directory, $"ready-{actor}.json"));
            var host = ready.RootElement.GetProperty("host").GetString();
            if (!observed.Add(host + "/" + processId) || actors[actor] is not null && actors[actor].Id != processId ||
                result.RootElement.GetProperty("requests").GetInt32() != 4 || result.RootElement.GetProperty("sourceCallbacks").GetInt32() !=
                (scope == CacheCoordinationScope.SharedStore && actor != 0 ? 0 : 1))
            {
                throw new InvalidDataException("The source scope, completed caller count or independent actor identity was not proven.");
            }
        }

        foreach (var actor in actors.Where(value => value is not null))
        {
            await actor.WaitForExitAsync(token);
            if (actor.ExitCode != 0) { throw new InvalidDataException("A controller actor failed."); }
        }
    }

    private static async Task InitializeAsync(ServiceProvider services, CancellationToken token)
    {
        var catalog = services.GetRequiredService<CacheCatalog>();
        var definitions = catalog.Collections.Values.Select(binding => new CacheProviderCollection("probe", binding.Name,
            binding.Options.ContractVersion, binding.Contract.Alias, binding.Options.Dimensions, binding.Options)).ToArray();
        var result = await services.GetRequiredService<CacheProviderRegistry>().Get("files").InitializeAsync(definitions, token);
        if (result.IsFailure) { throw new InvalidDataException("The selected cache contract could not initialize its native profile."); }
    }

    private static JsonDocument Read(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024) { throw new InvalidDataException("A role control record exceeds its parsing bound."); }

        return JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 8 });
    }

    private static void Write(string path, object value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        ProbeResult.Write(temporary, value);
        FileSystemDurability.FlushFile(temporary);
        FileSystemDurability.Move(temporary, path, true, true);
    }

    private static async Task WaitAsync(string path, Process actor, CancellationToken token)
    {
        while (!File.Exists(path))
        {
            if (actor?.HasExited == true) { throw new InvalidDataException("An actor exited before completing its role barrier."); }

            await Task.Delay(20, token);
        }
    }

    private static string RuntimeConfig()
    {
        var path = Path.Combine(Path.GetDirectoryName(typeof(CacheSharedContractProbe).Assembly.Location), "BridgingIT.DevKit.Infrastructure.UnitTests.runtimeconfig.json");
        return File.Exists(path) ? path : null;
    }
}