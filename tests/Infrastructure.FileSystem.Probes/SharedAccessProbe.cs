namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Diagnostics;
using System.Reflection;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.FileSystem;

// Disposable candidate experiments. These methods are never a production coordinator.
internal static class SharedAccessProbe
{
    internal static async Task<IReadOnlyList<ProbeCase>> RunAsync(string workspace)
    {
        var cases = new List<ProbeCase>();

        foreach (var scenario in new[]
        {
            "create",
            "etag",
            "provider-create",
            "provider-shared-create",
            "provider-shared-etag"
        }
        )
        {
            var directory = Path.Combine(workspace, scenario);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "current.json"), "initial");

            if (scenario == "provider-shared-etag")
            {
                var provider = new FileSystemBlobStoreProvider(new() { RootPath = directory, AccessKind = FileSystemStorageAccessKind.Smb });
                var seed = FileSystemStorageLoadRunner.Require(await provider.UploadAsync(new() { Key = new("qualification", "race"), Content = new MemoryStream([1]) }));
                File.WriteAllText(Path.Combine(directory, "seed-etag"), seed.ETag);
            }

            using var first = Start(workspace, scenario, "a");
            using var second = Start(workspace, scenario, "b");
            await WaitForAsync(Path.Combine(directory, "a.ready"));
            await WaitForAsync(Path.Combine(directory, "b.ready"));
            File.WriteAllText(Path.Combine(directory, "go"), "go");
            await FinishAsync(first);
            await FinishAsync(second);
            var results = new[]
            {
                File.ReadAllText(Path.Combine(directory, "a.result")),
                File.ReadAllText(Path.Combine(directory, "b.result"))
            };
            cases.Add(new(scenario + "-two-process-race", results.Count(r => r == "committed") == 1
                && results.Count(r => r == "conflict") == 1 ? "passed" : "failed", "Two child processes on local temp disk; exactly one commit and one conflict required. Declared shared ownership here is a protocol experiment, not network qualification."));
        }

        var held = Path.Combine(workspace, "held");
        Directory.CreateDirectory(held);
        using (var owner = Start(workspace, "hold", "owner"))
        {
            try
            {
                await WaitForAsync(Path.Combine(held, "owner.ready"));
                var watch = Stopwatch.StartNew();
                using var zero = await AcquireAsync(Path.Combine(held, "authority"), TimeSpan.Zero, default);
                cases.Add(new("zero-wait", zero is null ? "passed" : "failed", "No unlocked fallback while another process owns authority."));
                using var bounded = await AcquireAsync(Path.Combine(held, "authority"), TimeSpan.FromMilliseconds(150), default);
                cases.Add(new("bounded-wait", bounded is null && watch.ElapsedMilliseconds >= 150 ? "passed" : "failed", "Interruptible polling budget exhausted while owner remains alive."));
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

                try
                {
                    using var unexpected = await AcquireAsync(Path.Combine(held, "authority"), TimeSpan.FromSeconds(5), cancellation.Token);
                    cases.Add(new("cancel-wait", "failed", "Expected OperationCanceledException."));
                }
                catch (OperationCanceledException)
                {
                    cases.Add(new("cancel-wait", "passed", "Cancellation interrupted polling, not an outstanding OS call."));
                }

                using var distinct = await AcquireAsync(Path.Combine(held, "other-authority"), TimeSpan.Zero, default);
                cases.Add(new("distinct-key-progress", distinct is not null ? "passed" : "failed", "Separate key can acquire while owner is held."));
            }
            finally
            {
                if (!owner.HasExited)
                {
                    owner.Kill(true);
                }

                await owner.WaitForExitAsync();
            }
        }

        using var reacquired = await AcquireAsync(Path.Combine(held, "authority"), TimeSpan.FromSeconds(5), default);
        cases.Add(new("crashed-owner-reacquisition", reacquired is not null ? "passed" : "failed", "Process kill only; does not test power loss or remote lease revocation."));
        // Negative control: holding a separate file does not authorize or fence replacement.
        var manifest = Path.Combine(held, "current.json");
        File.WriteAllText(manifest, "new-owner");
        using (var stale = Start(workspace, "unchecked-rename", "stale"))
        {
            await FinishAsync(stale);
        }

        cases.Add(new("separate-lock-does-not-fence-rename", File.ReadAllText(manifest) == "obsolete-owner" ? "passed" : "failed",
            "Negative control reproduced unchecked publication despite another process holding authority. Candidate is NOT qualified."));

        return cases;
    }

    internal static async Task<int> RunWorkerAsync(string workspace, string scenario, string actor)
    {
        if (!Path.IsPathFullyQualified(workspace) || scenario is not ("create" or "etag" or "provider-create" or "provider-shared-create" or "provider-shared-etag" or "hold" or "unchecked-rename")
            || actor is not ("a" or "b" or "owner" or "stale"))
        {
            throw new InvalidDataException("Invalid worker arguments.");
        }

        ProbeWorkspace.RejectLinks(workspace);

        if (!File.Exists(Path.Combine(workspace, "run.json")))
        {
            throw new InvalidDataException("Unmarked worker workspace.");
        }

        var directory = Path.Combine(workspace, scenario is "hold" or "unchecked-rename" ? "held" : scenario);

        if (scenario == "unchecked-rename")
        {
            File.WriteAllText(Path.Combine(directory, "stale.json"), "obsolete-owner");
            File.Move(Path.Combine(directory, "stale.json"), Path.Combine(directory, "current.json"), true);

            return 0;
        }

        if (scenario == "hold")
        {
            using var authority = await AcquireAsync(Path.Combine(directory, "authority"), TimeSpan.FromSeconds(5), default) ?? throw new IOException("Owner could not acquire.");
            File.WriteAllText(Path.Combine(directory, actor + ".ready"), "ready");
            await Task.Delay(TimeSpan.FromMinutes(1));

            return 1;
        }

        File.WriteAllText(Path.Combine(directory, actor + ".ready"), "ready");
        await WaitForAsync(Path.Combine(directory, "go"));

        if (scenario.StartsWith("provider-", StringComparison.Ordinal))
        {
            var provider = new FileSystemBlobStoreProvider(new() { RootPath = directory,
                AccessKind = scenario == "provider-create" ? FileSystemStorageAccessKind.Local : FileSystemStorageAccessKind.Smb });
            using var source = new MemoryStream([1, 2, 3]);
            var result = scenario == "provider-shared-etag"
                ? await provider.UpdatePropertiesAsync(new() { Key = new("qualification", "race"), IfMatchETag = File.ReadAllText(Path.Combine(directory, "seed-etag")), Properties = new() { ["actor"] = actor } })
                : await provider.UploadAsync(new() { Key = new("qualification", "race"), Content = source, OverwriteMode = BlobOverwriteMode.FailIfExists });
            File.WriteAllText(Path.Combine(directory, actor + ".result"), result.IsSuccess ? "committed" : result.HasError<BlobStoreConflictError>() ? "conflict" : "failed");

            return 0;
        }

        using var handle = await AcquireAsync(Path.Combine(directory, "authority"), TimeSpan.FromSeconds(5), default) ?? throw new IOException("Contender could not acquire.");
        var manifest = Path.Combine(directory, "current.json");
        var canCommit = scenario == "create" ? !File.Exists(Path.Combine(directory, "created")) : File.ReadAllText(manifest) == "initial";

        if (canCommit)
        {
            File.WriteAllText(Path.Combine(directory, "created"), actor);
            File.WriteAllText(Path.Combine(directory, actor + ".json"), actor);
            File.Move(Path.Combine(directory, actor + ".json"), manifest, true);
        }

        File.WriteAllText(Path.Combine(directory, actor + ".result"), canCommit ? "committed" : "conflict");

        return 0;
    }

    private static Process Start(string workspace, string scenario, string actor)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }

        foreach (var arg in new[]
        {
            "worker",
            "--workspace",
            workspace,
            "--scenario",
            scenario,
            "--actor",
            actor
        }
        )
        {
            start.ArgumentList.Add(arg);
        }

        return Process.Start(start) ?? throw new IOException("Child launch failed.");
    }

    private static async Task FinishAsync(Process process)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            await process.WaitForExitAsync();
            throw new IOException("Child deadline exceeded.");
        }

        if (process.ExitCode != 0)
        {
            throw new IOException("Child probe failed.");
        }
    }

    private static async Task WaitForAsync(string path)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!File.Exists(path))
        {
            await Task.Delay(20, deadline.Token);
        }
    }

    private static async Task<FileStream> AcquireAsync(string path, TimeSpan budget, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)when ((exception.HResult & 0xffff)is 32 or 33 or 11)
            {
                if (watch.Elapsed >= budget)
                {
                    return null;
                }

                await Task.Delay(10, cancellationToken);
            }
        }
        while (true);
    }
}