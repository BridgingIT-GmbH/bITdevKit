namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

internal static class FileSystemLocalProcesses
{
    internal static async Task<int> RunAsync(string evidence, string runId, Dictionary<string, string> arguments)
    {
        if (!int.TryParse(arguments["--processes"], out var count) || count is < 2 or > 8)
        {
            throw new InvalidDataException("Local processes must be between 2 and 8.");
        }

        var workspace = ProbeWorkspace.Open(Path.GetTempPath(), runId, "load", true);
        var processes = new List<Process>();
        var artifacts = new List<string>();

        try
        {
            for (var i = 0; i < count; i++)
            {
                var childId = Guid.NewGuid().ToString("N");
                var childEvidence = Path.Combine(Directory.GetCurrentDirectory(), ".audit", "filesystem-storage", childId);
                var start = new ProcessStartInfo(Environment.ProcessPath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                }

                start.ArgumentList.Add("load");

                foreach (var argument in arguments.Where(p => p.Key is not ("--processes" or "--run-id" or "--workspace" or "--actor")))
                {
                    start.ArgumentList.Add(argument.Key);
                    start.ArgumentList.Add(argument.Value);
                }

                foreach (var argument in new[]
                {
                    "--run-id",
                    runId,
                    "--workspace",
                    workspace,
                    "--actor",
                    "process-" + i,
                    "--evidence-id",
                    childId
                }
                )
                {
                    start.ArgumentList.Add(argument);
                }

                var process = Process.Start(start) ?? throw new IOException("Load child launch failed.");
                process.OutputDataReceived += (_, line) =>
                {
                    if (line.Data is not null)
                    {
                        Console.WriteLine(line.Data);
                    }
                };
                process.ErrorDataReceived += (_, line) =>
                {
                    if (line.Data is not null)
                    {
                        Console.Error.WriteLine(line.Data);
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                processes.Add(process);
                artifacts.Add(Path.GetRelativePath(evidence, Path.Combine(childEvidence, "load.json")).Replace('\\', '/'));
            }

            ProbeResult.Write(Path.Combine(evidence, "process-index.json"), new { runId, processIds = processes.Select(p => p.Id).ToArray(), processArtifacts = artifacts });
            await Task.WhenAll(processes.Select(p => p.WaitForExitAsync()));
            var passed = processes.All(p => p.ExitCode == 0);
            ProbeResult.Write(Path.Combine(evidence, "load.json"), new { schemaVersion = 1, taskId = "TASK-022-local-processes", status = passed ? "passed" : "failed", runId,
                profileId = (OperatingSystem.IsWindows() ? "Windows" : "Linux") + "-Local", processCount = count, caseCount = count,
                processArtifacts = artifacts, failureDetails = passed ? Array.Empty<string>() : ["One or more child processes failed."],
                artifactPaths = artifacts, scope = "Independent local processes sharing one store. These are not independent hosts." });

            return passed ? 0 : 1;
        }
        finally
        {
            foreach (var process in processes)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync();
                }

                process.Dispose();
            }

            ProbeWorkspace.Open(Path.GetTempPath(), runId, "load", false);

            foreach (var file in Directory.EnumerateFileSystemEntries(workspace, "*", SearchOption.AllDirectories))
            {
                ProbeWorkspace.RejectLinks(file);
            }

            Directory.Delete(workspace, true);
        }
    }
}