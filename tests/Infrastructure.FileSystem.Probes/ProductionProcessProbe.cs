namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Diagnostics;
using System.Reflection;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.FileSystem;

internal static class ProductionProcessProbe
{
    internal static async Task<IReadOnlyList<ProbeCase>> RunAsync(string workspace)
    {
        var cases = new List<ProbeCase>();

        foreach (var point in new[]
        {
            "manifest-staged",
            "before-publication",
            "published"
        }
        )
        {
            var scenario = "production-" + point;
            var root = Path.Combine(workspace, scenario);
            var provider = Provider(root);
            var key = new BlobKey("process", "record");
            FileSystemStorageLoadRunner.Require(await provider.UploadAsync(new() { Key = key, Content = new MemoryStream([1]) }));
            var opened = FileSystemStorageLoadRunner.Require(await provider.DownloadAsync(key));
            await using var oldReader = opened.Content;
            using var child = Start(workspace, scenario);

            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var ready = Path.Combine(root, "boundary.ready");

                while (!File.Exists(ready))
                {
                    await Task.Delay(20, deadline.Token);
                }

                var beforeKill = await provider.UpdatePropertiesAsync(new() { Key = key });

                if (!beforeKill.HasError<BlobStoreTimeoutError>())
                {
                    throw new InvalidDataException("Paused shared namespace owner did not exclude a contender.");
                }

                child.Kill(true);
                await child.WaitForExitAsync(deadline.Token);
                var afterKill = await provider.UpdatePropertiesAsync(new() { Key = key });

                if (!afterKill.HasError<BlobStoreTimeoutError>())
                {
                    throw new InvalidDataException("Crashed shared namespace owner was automatically reclaimed.");
                }

                if (oldReader.ReadByte() != 1)
                {
                    throw new InvalidDataException("Open reader changed generation.");
                }

                await oldReader.DisposeAsync();
                await provider.Store.RecoverQuiescentAsync(true, default);
                var restarted = Provider(root);
                var download = FileSystemStorageLoadRunner.Require(await restarted.DownloadAsync(key));
                await using var content = download.Content;

                if (content.ReadByte() != (point == "published" ? 2 : 1))
                {
                    throw new InvalidDataException("Restart did not preserve the complete old/new publication.");
                }

                cases.Add(new(scenario, "passed", "Independent child paused/killed at production boundary. Persistent owner blocked takeover before/after kill; explicit quiescent recovery preserved complete expected bytes and old reader. Shared protocol on local temp disk, not SMB or power loss."));
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(true);
                    await child.WaitForExitAsync();
                }
            }
        }

        return cases;
    }

    internal static async Task<int> WorkerAsync(string workspace, string scenario)
    {
        if (scenario is not ("production-manifest-staged" or "production-before-publication" or "production-published"))
        {
            throw new InvalidDataException("Invalid production scenario.");
        }

        ProbeWorkspace.RejectLinks(workspace);

        if (!File.Exists(Path.Combine(workspace, "run.json")))
        {
            throw new InvalidDataException("Unmarked production workspace.");
        }

        var root = Path.Combine(workspace, scenario);
        var provider = Provider(root);
        provider.Store.Fault = point =>
        {
            if (point != scenario[11..])
            {
                return;
            }

            File.WriteAllText(Path.Combine(root, "boundary.ready"), point);
            Thread.Sleep(Timeout.Infinite); // The parent deliberately terminates this process at the exact production boundary.
        };
        FileSystemStorageLoadRunner.Require(await provider.UploadAsync(new() { Key = new("process", "record"), Content = new MemoryStream([2]) }));

        return 1;
    }

    private static FileSystemBlobStoreProvider Provider(string root)
    {
        return new(new() { RootPath = root, AccessKind = FileSystemStorageAccessKind.Smb, LockAcquireTimeout = TimeSpan.Zero });
    }

    private static Process Start(string workspace, string scenario)
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
            "owner"
        }
        )
        {
            start.ArgumentList.Add(arg);
        }

        return Process.Start(start) ?? throw new IOException("Production child launch failed.");
    }
}