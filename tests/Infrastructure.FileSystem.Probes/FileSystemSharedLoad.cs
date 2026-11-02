namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Text.Json;
using BridgingIT.DevKit.Infrastructure.FileSystem;

internal static class FileSystemSharedLoad
{
    internal static async Task<int> RunAsync(string evidence, string runId, Dictionary<string, string> arguments)
    {
        if (Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_TESTS") != "1")
        {
            ProbeResult.Write(Path.Combine(evidence, "load.json"), new ProbeResult("TASK-022-shared", runId, arguments["--profile"],
                [new("share-opt-in", "unavailable", "Share load is disabled. Set BDK_FILESYSTEM_SHARE_TESTS=1 and provide external qualification configuration.")], []));

            return 2;
        }

        QualificationConfiguration configuration;

        try
        {
            configuration = QualificationConfiguration.LoadConfiguration(Environment.GetEnvironmentVariable("BDK_FILESYSTEM_QUALIFICATION_CONFIG"));
        }
        catch (ConfigurationUnavailableException)
        {
            throw new InvalidDataException("Enabled share load requires external BDK_FILESYSTEM_QUALIFICATION_CONFIG.");
        }

        var os = OperatingSystem.IsWindows() ? "Windows" : "Linux";

        if (OperatingSystem.IsWindows() && configuration.Profiles.Any(p => p.Id == arguments["--profile"] && p.AccessKind == "Nfs"))
        {
            ProbeResult.Write(Path.Combine(evidence, "load.json"), new ProbeResult("TASK-022-shared", runId, arguments["--profile"],
                [new("host-protocol", "unavailable", "Skipped before share access. Windows NFS is outside the supported matrix.")], []));

            return 2;
        }

        var profile = configuration.Profiles.SingleOrDefault(p => p.Id == arguments["--profile"] && p.HostOs == os)
            ?? throw new InvalidDataException("No matching runtime OS/profile in enabled share configuration.");

        if (profile.AccessKind == "Local")
        {
            throw new InvalidDataException("Shared load requires an SMB or NFS profile.");
        }

        RemoteFileSystemProbe.Validate(profile);
        var role = arguments.GetValueOrDefault("--role");

        if (role is not ("controller" or "peer") || !arguments.ContainsKey("--run-id"))
        {
            throw new InvalidDataException("Shared load requires explicit --role controller/peer and the same --run-id on both hosts.");
        }

        var expectedHost = role == "controller" ? profile.ReferenceHostId : profile.PeerHostId;

        if (!string.Equals(Environment.MachineName, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Configured host identity does not match this role.");
        }

        if (!profile.Environment.TryGetValue("sharedProfileId", out var group) || !QualificationConfiguration.SafeId(group))
        {
            throw new InvalidDataException("Shared profiles require environment.sharedProfileId.");
        }

        var workspaceProfile = "load-" + FileSystemStore.Hash([group])[..32];
        var workspace = Path.Combine(profile.ScratchRoot, "bdk-filesystem-probes", runId, workspaceProfile);
        using var joining = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        if (role == "controller")
        {
            ProbeWorkspace.Open(profile.ScratchRoot, runId, workspaceProfile, true);
        }
        else
        {
            await WaitAsync(Path.Combine(workspace, "run.json"), joining.Token);
            ProbeWorkspace.Open(profile.ScratchRoot, runId, workspaceProfile, false);
        }

        var ready = Path.Combine(workspace, role + ".ready.json");
        var kernelId = RemoteFileSystemProbe.KernelIdentity();
        ProbeResult.Write(ready + ".tmp", new { runId, host = Environment.MachineName, os, profile.AccessKind, kernelId });
        FileSystemDurability.FlushFile(ready + ".tmp");
        FileSystemDurability.Move(ready + ".tmp", ready, false, true);
        var other = role == "controller" ? "peer" : "controller";
        var otherReady = Path.Combine(workspace, other + ".ready.json");
        await WaitAsync(otherReady, joining.Token);
        using (var peer = JsonDocument.Parse(File.ReadAllBytes(otherReady)))
        {
            if (peer.RootElement.GetProperty("runId").GetString() != runId
                || string.Equals(peer.RootElement.GetProperty("host").GetString(), Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Independent-host barrier identity failed.");
            }

            if (kernelId is not null && peer.RootElement.GetProperty("kernelId").ValueKind == JsonValueKind.String
                && peer.RootElement.GetProperty("kernelId").GetString() == kernelId)
            {
                throw new InvalidDataException("Same-kernel containers are not independent client hosts.");
            }
        }

        var local = new Dictionary<string, string>(arguments)
        {
            ["--workspace"] = workspace,
            ["--scratch-root"] = profile.ScratchRoot,
            ["--workspace-profile"] = workspaceProfile,
            ["--access"] = profile.AccessKind,
            ["--actor"] = role + "-" + FileSystemStore.Hash([Environment.MachineName])[..12]
        };
        var result = await FileSystemStorageLoadRunner.RunAsync(evidence, runId, local);
        var sharedArtifacts = Path.Combine(workspace, role + "-artifacts");
        FileSystemDurability.EnsureDirectory(sharedArtifacts);
        using (var localResult = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidence, "load.json"))))
        {
            foreach (var item in localResult.RootElement.GetProperty("artifactPaths").EnumerateArray())
            {
                var artifact = SafeArtifact(item);
                CopyDurable(Path.Combine(evidence, artifact), Path.Combine(sharedArtifacts, artifact));
            }
        }

        var resultPath = Path.Combine(workspace, role + ".result.json");
        CopyDurable(Path.Combine(evidence, "load.json"), resultPath);

        if (role == "peer")
        {
            return result;
        }

        var peerResult = Path.Combine(workspace, "peer.result.json");
        using var finishing = new CancellationTokenSource(TimeSpan.FromHours(24));
        await WaitAsync(peerResult, finishing.Token);
        Directory.CreateDirectory(Path.Combine(evidence, "controller"));
        Directory.CreateDirectory(Path.Combine(evidence, "peer"));
        File.Move(Path.Combine(evidence, "load.json"), Path.Combine(evidence, "controller", "load.json"));
        File.Copy(peerResult, Path.Combine(evidence, "peer", "load.json"));
        using var remote = JsonDocument.Parse(File.ReadAllBytes(peerResult));

        foreach (var participant in new[]
        {
            "controller",
            "peer"
        }
        )
        {
            using var child = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidence, participant, "load.json")));

            foreach (var item in child.RootElement.GetProperty("artifactPaths").EnumerateArray())
            {
                var artifact = SafeArtifact(item);
                File.Copy(Path.Combine(workspace, participant + "-artifacts", artifact), Path.Combine(evidence, participant, artifact));
            }
        }

        var passed = result == 0 && remote.RootElement.GetProperty("status").GetString() == "passed";
        ProbeResult.Write(Path.Combine(evidence, "load.json"), new { schemaVersion = 1, taskId = "TASK-022-shared", status = passed ? "passed" : "failed", runId,
            profileId = profile.Id, processArtifacts = new[] { "controller/load.json", "peer/load.json" }, caseCount = 2,
            artifactPaths = new[] { "controller/load.json", "peer/load.json" }, scope = "Distinct configured OS machine identities; Linux boot identities reject same-kernel containers. Independent physical/VM placement requires external qualification. No power-loss or stale-writer fault qualification is inferred." });

        // Retain marked shared artifacts. Never remove a remote run while its outcome is uncertain.
        return passed ? 0 : 1;
    }

    private static async Task WaitAsync(string path, CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private static string SafeArtifact(JsonElement item)
    {
        var name = item.GetString();

        return name is not null && Path.GetFileName(name) == name && name.EndsWith(".json", StringComparison.Ordinal)
            ? name : throw new InvalidDataException("Invalid shared result artifact name.");
    }

    private static void CopyDurable(string source, string destination)
    {
        File.Copy(source, destination + ".tmp", false);
        FileSystemDurability.FlushFile(destination + ".tmp");
        FileSystemDurability.Move(destination + ".tmp", destination, false, true);
    }
}