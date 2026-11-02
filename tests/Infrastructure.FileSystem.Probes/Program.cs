namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Runtime.InteropServices;
using BridgingIT.DevKit.Infrastructure.FileSystem;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        var command = args.FirstOrDefault() ?? "qualify";
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        if (args.Length % 2 == 0 && args.Length > 0)
        {
            return 1;
        }

        for (var i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[i], args[i + 1]))
            {
                return 1;
            }
        }

        try
        {
            if (command == "worker")
            {
                return options["--scenario"].StartsWith("production-", StringComparison.Ordinal)
                    ? await ProductionProcessProbe.WorkerAsync(options["--workspace"], options["--scenario"])
                    : await SharedAccessProbe.RunWorkerAsync(options["--workspace"], options["--scenario"], options["--actor"]);
            }

            if (command is not ("qualify" or "local-probe" or "load" or "verify-results" or "recover"))
            {
                return 2;
            }

            var runId = options.GetValueOrDefault("--run-id") ?? Guid.NewGuid().ToString("N");

            if (!Guid.TryParseExact(runId, "N", out _))
            {
                return 1;
            }

            var repository = Directory.GetCurrentDirectory();

            if (!File.Exists(Path.Combine(repository, "bITdevKit.slnx")))
            {
                return 1;
            }

            var evidenceId = options.GetValueOrDefault("--evidence-id") ?? runId;

            if (!Guid.TryParseExact(evidenceId, "N", out _))
            {
                return 1;
            }

            var evidence = Path.Combine(repository, ".audit", "filesystem-storage", evidenceId);
            Directory.CreateDirectory(evidence);
            ProbeResult.Write(Path.Combine(evidence, "source-map.json"), SourceMap.Capture(repository));
            Console.WriteLine($"run={runId}; evidence={evidence}");

            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                ProbeResult.Write(Path.Combine(evidence, "qualification.json"), new ProbeResult("host-applicability", runId, null,
                    [new("host", "unavailable", "Skipped before test-root/share access. Only Windows and Linux are applicable.")], []));

                return 2;
            }

            if (command == "verify-results")
            {
                return FileSystemStorageResultVerifier.Verify(evidence, runId);
            }

            if (command == "recover")
            {
                if (options.GetValueOrDefault("--quiescent") != "true")
                {
                    throw new InvalidDataException("Recovery requires --quiescent true after stopping every participant on every host.");
                }

                var access = Enum.Parse<FileSystemStorageAccessKind>(options.GetValueOrDefault("--access") ?? "Local");
                var store = new FileSystemStore(new() { RootPath = options["--root"], AccessKind = access, CreateRoot = false });
                await store.RecoverQuiescentAsync(true, default);
                ProbeResult.Write(Path.Combine(evidence, "recovery.json"), new ProbeResult("TASK-003-quiescent-recovery", runId, access.ToString(), [new("quiescent-recovery", "passed", "Operator asserted all-host quiescence and drained old server operations. Known markers removed and unreferenced payloads collected. Mutation reconciliation is an external prerequisite, not performed by this command.")], []));

                return 0;
            }

            if (command == "load")
            {
                if (options.GetValueOrDefault("--profile")is { } requested && requested != "local")
                {
                    return await FileSystemSharedLoad.RunAsync(evidence, runId, options);
                }

                if (options.GetValueOrDefault("--processes")is { } processCount && processCount != "1")
                {
                    return await FileSystemLocalProcesses.RunAsync(evidence, runId, options);
                }

                return await FileSystemStorageLoadRunner.RunAsync(evidence, runId, options);
            }

            var cases = new List<ProbeCase>();
            QualificationConfiguration configuration = null;

            try
            {
                configuration = QualificationConfiguration.LoadConfiguration(Environment.GetEnvironmentVariable("BDK_FILESYSTEM_QUALIFICATION_CONFIG"));
                cases.Add(new("configuration", "passed", "Schema and non-secret profile validation passed; roots not accessed."));
            }
            catch (ConfigurationUnavailableException)
            {
                cases.Add(new("configuration", "unavailable", "BDK_FILESYSTEM_QUALIFICATION_CONFIG absent or not readable."));
            }
            catch (Exception exception)when (exception is InvalidDataException or System.Text.Json.JsonException)
            {
                cases.Add(new("configuration", "failed", "Invalid schema/profile or credential-bearing input; values withheld."));
            }

            if (command == "local-probe")
            {
                var workspace = ProbeWorkspace.Open(Path.GetTempPath(), runId, "local", true);

                try
                {
                    var localCases = await SharedAccessProbe.RunAsync(workspace);
                    localCases = localCases.Concat(await ProductionProcessProbe.RunAsync(workspace)).ToArray();
                    ProbeResult.Write(Path.Combine(evidence, "local-process.json"), new ProbeResult("TASK-002-local-subset", runId, "local", localCases, []));
                    cases.AddRange(localCases);
                }
                catch (Exception exception)
                {
                    cases.Add(new("local-process", "failed", exception.GetType().Name + "; diagnostic values withheld."));
                }
                finally
                {
                    // No recursive deletion until the path and exact ownership marker are revalidated.
                    ProbeWorkspace.Open(Path.GetTempPath(), runId, "local", false);
                    ProbeWorkspace.RejectLinks(workspace);
                    Directory.Delete(workspace, true);
                }
            }

            foreach (var target in new[]
            {
                "Windows/Local",
                "Linux/Local",
                "Windows/Smb",
                "Linux/Smb",
                "Linux/Nfs",
                "mixed-Windows-Linux-Smb"
            }
            )
            {
                cases.Add(new(target, "unavailable", "Full qualification not executed. Local exploratory results are separate. Network shares unavailable by operator declaration."));
            }

            var result = new ProbeResult("TASK-001/TASK-002", runId, options.GetValueOrDefault("--profile"), cases, ["source-map.json", "protocol.json"]);
            ProbeResult.Write(Path.Combine(evidence, "qualification.json"), result);
            ProbeResult.Write(Path.Combine(evidence, "protocol.json"), new
            {
                schemaVersion = 1, taskId = "TASK-002", status = "unavailable", runId,
                caseCount = cases.Count, failureDetails = new[] { "Cross-host lock-loss/publication enforcement unverified.", "Durable namespace publication, deletion and buffered-to-durable ordering unverified.", "No power-loss or network failure injection available." },
                artifactPaths = new[] { "qualification.json", "local-process.json" },
                runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
                localCandidate = "Local exclusive handles; shared persistent namespace owners without timed takeover. Linux file/directory fsync and Windows write-through POSIX replacement; durable tombstones, protected stages and delete-sharing readers. Native failures fail closed. See package README for primitives and limitations.",
                negativeControl = "Unchecked rename ignores a separately held authority file. This is a counterexample to automatic fencing, not a qualified publication implementation.",
                productionReady = false, profileCount = configuration?.Profiles.Length ?? 0
            });
            Console.WriteLine($"{result.Status}; evidence={evidence}");

            return result.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetType().Name + "; " + (exception is InvalidDataException ? exception.Message : "Probe failed; input values withheld."));

            return 1;
        }
    }
}