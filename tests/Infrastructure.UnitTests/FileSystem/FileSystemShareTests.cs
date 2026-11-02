namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;
using Infrastructure.FileSystem.Probes;

public sealed class FileSystemShareFactAttribute : FactAttribute
{
    public FileSystemShareFactAttribute(string protocol, bool fault = false)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            this.Skip = "Requires Windows or Linux.";
        }
        else if (OperatingSystem.IsWindows() && protocol == "Nfs")
        {
            this.Skip = "Windows NFS is outside the supported matrix.";
        }
        else if (Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_TESTS") != "1")
        {
            this.Skip = "Set BDK_FILESYSTEM_SHARE_TESTS=1 and external qualification configuration to enable share tests.";
        }
        else if (fault && Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_FAULT_TESTS") != "1")
        {
            this.Skip = "Destructive infrastructure scenarios require separate BDK_FILESYSTEM_SHARE_FAULT_TESTS=1 opt-in.";
        }
    }
}

[Trait("Category", "FileSystemShare")]
public sealed class FileSystemShareTests
{
    [FileSystemShareFact("Smb")]
    public Task Smb_SingleHostClients_ConditionalRaceAndReaderLifetime()
    {
        return RunSingleHostAsync("Smb");
    }

    [FileSystemShareFact("Nfs")]
    public Task Nfs_SingleHostClients_ConditionalRaceAndReaderLifetime()
    {
        return RunSingleHostAsync("Nfs");
    }

    [FileSystemShareFact("Smb")]
    public Task Smb_Retention_ExtendedExpirationIsPreserved()
    {
        return RunRetentionAsync("Smb");
    }

    [FileSystemShareFact("Nfs")]
    public Task Nfs_Retention_ExtendedExpirationIsPreserved()
    {
        return RunRetentionAsync("Nfs");
    }

    [FileSystemShareFact("Smb")]
    public Task Smb_IndependentHosts_CreateOnlyRace()
    {
        return RunIndependentHostsAsync("Smb");
    }

    [FileSystemShareFact("Nfs")]
    public Task Nfs_IndependentHosts_CreateOnlyRace()
    {
        return RunIndependentHostsAsync("Nfs");
    }

    [FileSystemShareFact("Smb", true)]
    public Task Smb_Reconnect_ExternalFaultDriverIsRequired()
    {
        return RunFaultAsync("Smb");
    }

    [FileSystemShareFact("Nfs", true)]
    public Task Nfs_Reconnect_ExternalFaultDriverIsRequired()
    {
        return RunFaultAsync("Nfs");
    }

    [FileSystemShareFact("Smb", true)]
    public Task Smb_ServerRestart_ExternalDisposableFaultDriverIsRequired()
    {
        return RunFaultAsync("Smb", "server-restart");
    }

    [FileSystemShareFact("Nfs", true)]
    public Task Nfs_ServerRestart_ExternalDisposableFaultDriverIsRequired()
    {
        return RunFaultAsync("Nfs", "server-restart");
    }

    private static QualificationProfile Select(string protocol)
    {
        QualificationConfiguration configuration;

        try
        {
            configuration = QualificationConfiguration.LoadConfiguration(Environment.GetEnvironmentVariable("BDK_FILESYSTEM_QUALIFICATION_CONFIG"));
        }
        catch (ConfigurationUnavailableException)
        {
            throw new InvalidOperationException("Opted-in share tests require readable external BDK_FILESYSTEM_QUALIFICATION_CONFIG. No share was accessed.");
        }
        catch (Exception exception)when (exception is InvalidDataException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Opted-in BDK_FILESYSTEM_QUALIFICATION_CONFIG is invalid or unreadable; values withheld. No share was accessed.");
        }

        var os = OperatingSystem.IsWindows() ? "Windows" : "Linux";
        var matches = configuration.Profiles.Where(p => p.HostOs == os && p.AccessKind == protocol).ToArray();
        Assert.True(matches.Length > 0, $"Opted-in {os}/{protocol} requires a matching qualification profile.");
        RemoteFileSystemProbe.Validate(matches[0]);

        return matches[0];
    }

    private static async Task RunSingleHostAsync(string protocol)
    {
        var profile = Select(protocol); // Validate before any share access. Never substitute a temp root.
        var run = Guid.NewGuid().ToString("N");
        var path = ProbeWorkspace.Open(profile.ScratchRoot, run, profile.Id, true);
        var options = new FileSystemStorageOptions
        {
            RootPath = path,
            AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol)
        };
        var first = new FileSystemBlobStoreProvider(options);
        var second = new FileSystemBlobStoreProvider(options);
        var key = new BlobKey("qualification", "race");
        var results = await Task.WhenAll(first.UploadAsync(new() { Key = key, Content = new MemoryStream([1]), OverwriteMode = BlobOverwriteMode.FailIfExists }),
            second.UploadAsync(new() { Key = key, Content = new MemoryStream([2]), OverwriteMode = BlobOverwriteMode.FailIfExists }));
        // These are same-host clients. Independent process/host qualification remains separate.
        results.Count(r => r.IsSuccess).ShouldBe(1, $"{profile.HostOs}/{protocol} must implement qualified publication before this test can pass.");
        results.Count(r => r.HasError<BlobStoreConflictError>()).ShouldBe(1);
        var winner = results.Single(r => r.IsSuccess).Value;
        var download = await first.DownloadAsync(key);
        await using var reader = download.Value.Content;
        var updates = await Task.WhenAll(first.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = winner.ETag }),
            second.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = winner.ETag }));
        updates.Count(r => r.IsSuccess).ShouldBe(1);
        updates.Count(r => r.HasError<BlobStoreConflictError>()).ShouldBe(1);
        (await second.DeleteAsync(key)).IsSuccess.ShouldBeTrue();
        new[]
        {
            1,
            2
        }.ShouldContain(reader.ReadByte());
        var firstDocuments = new FileSystemDocumentStoreProvider(options);
        var secondDocuments = new FileSystemDocumentStoreProvider(options);
        var type = DocumentTypeIdentity.For<ShareDocument>();
        var documentKey = new DocumentKey("qualification", "conditional-race");
        var creates = await Task.WhenAll(firstDocuments.UpsertAsync(type, new() { Key = documentKey, Content = [1], Options = new() { CreateOnly = true } }),
            secondDocuments.UpsertAsync(type, new() { Key = documentKey, Content = [2], Options = new() { CreateOnly = true } }));
        creates.Count(r => r.IsSuccess).ShouldBe(1);
        creates.Count(r => r.HasError<DocumentStoreConflictError>()).ShouldBe(1);
        var etag = creates.Single(r => r.IsSuccess).Value.ETag;
        var changes = await Task.WhenAll(firstDocuments.UpdatePropertiesAsync(type, new(documentKey) { IfMatchETag = etag }, null, true),
            secondDocuments.UpdatePropertiesAsync(type, new(documentKey) { IfMatchETag = etag }, null, true));
        changes.Count(r => r.IsSuccess).ShouldBe(1);
        changes.Count(r => r.HasError<DocumentStoreConflictError>()).ShouldBe(1);
        var committed = await firstDocuments.GetAsync(type, documentKey, DateTimeOffset.UtcNow);
        committed.IsSuccess.ShouldBeTrue();
        new byte[]
        {
            1,
            2
        }.ShouldContain(committed.Value.Content.Single());
    // Keep marked share artifacts for inspection. No recursive share cleanup.
    }

    private static async Task RunRetentionAsync(string protocol)
    {
        var profile = Select(protocol);
        var path = ProbeWorkspace.Open(profile.ScratchRoot, Guid.NewGuid().ToString("N"), profile.Id, true);
        var provider = new FileSystemBlobStoreProvider(new() { RootPath = path, AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol) });
        var key = new BlobKey("qualification", "ttl");
        var initial = await provider.UploadAsync(new() { Key = key, Content = new MemoryStream([3]), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        initial.IsSuccess.ShouldBeTrue();
        var retention = Assert.IsAssignableFrom<IBlobStoreRetentionProvider>(provider);
        var discovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        provider.Store.Fault = point =>
        {
            if (point != "retention-discovered")
            {
                return;
            }

            discovered.TrySetResult();

            if (!release.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Concurrent expiry update did not complete.");
            }
        };
        var pending = Task.Run(() => retention.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow, BatchSize = 1, MaxBatches = 1 }));

        try
        {
            await discovered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var concurrent = new FileSystemBlobStoreProvider(new() { RootPath = path, AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol) });
            var extended = await concurrent.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = initial.Value.ETag, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            extended.IsSuccess.ShouldBeTrue();
        }
        finally
        {
            release.Set();
        }

        var sweep = await pending;
        sweep.IsSuccess.ShouldBeTrue();
        sweep.Value.DeletedKeys.ShouldNotContain(key);
        (await provider.ExistsAsync(key)).Value.ShouldBeTrue();
    }

    private static async Task RunFaultAsync(string protocol, string faultKind = "disconnect")
    {
        var profile = Select(protocol);
        Assert.True(profile.ReferenceHostId != profile.PeerHostId, "Independent configured host identities required.");
        Assert.True(profile.Environment.TryGetValue("faultBarrierRoot", out var barrier) && Path.IsPathFullyQualified(barrier),
            "Fault tests require a preconfigured disposable infrastructure driver with faultBarrierRoot. No mounts or servers are changed by tests.");
        ProbeWorkspace.RejectLinks(barrier);
        var runId = Guid.NewGuid().ToString("N");
        var workspace = ProbeWorkspace.Open(profile.ScratchRoot, runId, profile.Id, true);
        var key = new BlobKey("qualification", "reconnect");
        var provider = new FileSystemBlobStoreProvider(new() { RootPath = workspace, AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol) });
        var initial = await provider.UploadAsync(new() { Key = key, Content = new MemoryStream([7]) });
        initial.IsSuccess.ShouldBeTrue();
        var open = await provider.DownloadAsync(key);
        open.IsSuccess.ShouldBeTrue();
        await using var originalReader = open.Value.Content;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        provider.Store.Fault = point =>
        {
            if (point != "before-publication")
            {
                return;
            }

            ProbeResult.Write(Path.Combine(barrier, runId + ".request.json"), new { runId, profileId = profile.Id, hostOs = profile.HostOs, protocol,
                scenario = faultKind + "-during-publication", faultKind, workspace, mutationETag = initial.Value.ETag,
                instruction = faultKind == "server-restart"
                    ? "Restart only the preconfigured disposable storage server. Acknowledge isolation, then restore independently of publication completion. Collect restart and protocol trace. Never recover owner markers."
                    : "Isolate this client from disposable storage, publish outage-ready acknowledgement, then heal without waiting for publication completion. Collect protocol trace. Never recover owner markers." });
            var ready = Path.Combine(barrier, runId + ".outage-ready.json");

            while (!File.Exists(ready))
            {
                Task.Delay(100, deadline.Token).GetAwaiter().GetResult();
            }

            using var outage = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ready));
            outage.RootElement.GetProperty("runId").GetString().ShouldBe(runId);
            outage.RootElement.GetProperty("profileId").GetString().ShouldBe(profile.Id);
            outage.RootElement.GetProperty("peerHostId").GetString().ShouldBe(profile.PeerHostId);
            outage.RootElement.GetProperty("status").GetString().ShouldBe("isolated");
        };
        var mutation = await provider.UploadAsync(new() { Key = key, Content = new MemoryStream([8]) }, deadline.Token);
        provider.Store.Fault = null;
        Assert.True(mutation.IsSuccess || mutation.HasError<BlobStoreIndeterminateOperationError>(),
            "A publication attempted across the outage must commit or report an indeterminate mutation; other outcomes fail qualification.");
        var response = Path.Combine(barrier, runId + ".response.json");

        while (!File.Exists(response))
        {
            await Task.Delay(100, deadline.Token);
        }

        using var acknowledgement = System.Text.Json.JsonDocument.Parse(File.ReadAllText(response));
        acknowledgement.RootElement.GetProperty("runId").GetString().ShouldBe(runId);
        acknowledgement.RootElement.GetProperty("profileId").GetString().ShouldBe(profile.Id);
        acknowledgement.RootElement.GetProperty("peerHostId").GetString().ShouldBe(profile.PeerHostId);
        acknowledgement.RootElement.GetProperty("status").GetString().ShouldBe("healed");
        acknowledgement.RootElement.GetProperty("faultInjected").GetBoolean().ShouldBeTrue();
        acknowledgement.RootElement.GetProperty("faultKind").GetString().ShouldBe(faultKind);
        var tracePath = acknowledgement.RootElement.GetProperty("protocolTracePath").GetString();
        Assert.True(Path.IsPathFullyQualified(tracePath) && File.Exists(tracePath), "Require independently collected protocol trace; barrier acknowledgement alone is insufficient.");
        originalReader.ReadByte().ShouldBe(7);
        var contender = new FileSystemBlobStoreProvider(new() { RootPath = workspace,
            AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol), LockAcquireTimeout = TimeSpan.Zero });
        var metadata = await contender.GetPropertiesAsync(key);
        metadata.IsSuccess.ShouldBeTrue();
        var attempted = mutation.Errors.OfType<BlobStoreIndeterminateOperationError>().FirstOrDefault();

        if (attempted is not null)
        {
            var blocked = await contender.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = metadata.Value.ETag });
            blocked.HasError<BlobStoreTimeoutError>().ShouldBeTrue("An uncertain shared publisher must retain namespace authority after reconnect.");
        }
        else
        {
            var after = await contender.DownloadAsync(key);
            after.IsSuccess.ShouldBeTrue();
            await using var content = after.Value.Content;
            content.ReadByte().ShouldBe(8);
            var stale = await contender.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = initial.Value.ETag });
            stale.HasError<BlobStoreConflictError>().ShouldBeTrue();
        }
    }

    private static async Task RunIndependentHostsAsync(string protocol)
    {
        var profile = Select(protocol);
        var runId = Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_RUN_ID");
        Assert.True(Guid.TryParseExact(runId, "N", out _), "Cross-host tests require one explicit BDK_FILESYSTEM_SHARE_RUN_ID on both independently launched hosts.");
        var role = Environment.GetEnvironmentVariable("BDK_FILESYSTEM_SHARE_ROLE");
        Assert.True(role is "controller" or "peer", "Set BDK_FILESYSTEM_SHARE_ROLE to controller or peer.");
        Assert.True(profile.Environment.TryGetValue("sharedProfileId", out var sharedProfileId) && QualificationConfiguration.SafeId(sharedProfileId),
            "Matching profiles on both hosts require the same environment.sharedProfileId.");
        var expectedHost = role == "controller" ? profile.ReferenceHostId : profile.PeerHostId;
        string.Equals(Environment.MachineName, expectedHost, StringComparison.OrdinalIgnoreCase).ShouldBeTrue("Configured role must match this machine identity.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var workspace = Path.Combine(profile.ScratchRoot, "bdk-filesystem-probes", runId, sharedProfileId);

        if (role == "controller")
        {
            ProbeWorkspace.Open(profile.ScratchRoot, runId, sharedProfileId, true);
        }
        else
        {
            while (!File.Exists(Path.Combine(workspace, "run.json")))
            {
                await Task.Delay(100, deadline.Token);
            }

            ProbeWorkspace.Open(profile.ScratchRoot, runId, sharedProfileId, false);
        }

        File.WriteAllText(Path.Combine(workspace, role + ".ready"), Environment.MachineName);
        var other = role == "controller" ? "peer" : "controller";

        while (!File.Exists(Path.Combine(workspace, other + ".ready")))
        {
            await Task.Delay(100, deadline.Token);
        }

        var provider = new FileSystemBlobStoreProvider(new() { RootPath = workspace, AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol) });
        var result = await provider.UploadAsync(new() { Key = new("qualification", "cross-host-create"), Content = new MemoryStream([1]), OverwriteMode = BlobOverwriteMode.FailIfExists }, deadline.Token);
        var documents = new FileSystemDocumentStoreProvider(new() { RootPath = workspace, AccessKind = Enum.Parse<FileSystemStorageAccessKind>(protocol) });
        var document = await documents.UpsertAsync(DocumentTypeIdentity.For<ShareDocument>(), new() { Key = new("qualification", "cross-host-document"),
            Content = [1], Options = new() { CreateOnly = true } }, deadline.Token);
        var response = new
        {
            host = Environment.MachineName,
            os = OperatingSystem.IsWindows() ? "Windows" : "Linux",
            processId = Environment.ProcessId,
            committed = result.IsSuccess,
            conflict = result.HasError<BlobStoreConflictError>(),
            documentCommitted = document.IsSuccess,
            documentConflict = document.HasError<DocumentStoreConflictError>(),
            kernelId = RemoteFileSystemProbe.KernelIdentity(),
            runId
        };
        var temporary = Path.Combine(workspace, role + ".tmp");
        ProbeResult.Write(temporary, response);
        File.Move(temporary, Path.Combine(workspace, role + ".result.json"), false);
        Assert.True(response.committed || response.conflict, "Shared provider is unavailable or failed; this is not a qualification pass.");
        Assert.True(response.documentCommitted || response.documentConflict, "Shared document publication failed; this is not a qualification pass.");

        if (role == "controller")
        {
            var peerPath = Path.Combine(workspace, "peer.result.json");

            while (!File.Exists(peerPath))
            {
                await Task.Delay(100, deadline.Token);
            }

            using var peer = System.Text.Json.JsonDocument.Parse(File.ReadAllText(peerPath));
            peer.RootElement.GetProperty("runId").GetString().ShouldBe(runId);
            string.Equals(peer.RootElement.GetProperty("host").GetString(), profile.PeerHostId, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
            string.Equals(peer.RootElement.GetProperty("host").GetString(), Environment.MachineName, StringComparison.OrdinalIgnoreCase).ShouldBeFalse();

            if (response.kernelId is not null && peer.RootElement.GetProperty("kernelId").ValueKind == System.Text.Json.JsonValueKind.String)
            {
                peer.RootElement.GetProperty("kernelId").GetString().ShouldNotBe(response.kernelId, "Containers sharing one Linux kernel are not independent client hosts.");
            }

            var committed = peer.RootElement.GetProperty("committed").GetBoolean();
            var conflict = peer.RootElement.GetProperty("conflict").GetBoolean();
            Assert.True(response.committed != committed && response.conflict != conflict && (committed || conflict), "Require one commit and one conflict across independently named hosts.");
            Assert.True(response.documentCommitted != peer.RootElement.GetProperty("documentCommitted").GetBoolean()
                && response.documentConflict != peer.RootElement.GetProperty("documentConflict").GetBoolean(), "Require one document commit and one conflict across independent hosts.");
        }

        var key = new BlobKey("qualification", "cross-host-create");
        var opened = await provider.DownloadAsync(key, deadline.Token);
        opened.IsSuccess.ShouldBeTrue();
        await using var oldReader = opened.Value.Content;
        File.WriteAllText(Path.Combine(workspace, role + ".reader-ready"), Environment.MachineName);
        var mutated = Path.Combine(workspace, "reader-mutation.done");

        if (role == "controller")
        {
            while (!File.Exists(Path.Combine(workspace, "peer.reader-ready")))
            {
                await Task.Delay(100, deadline.Token);
            }

            var replaced = await provider.UploadAsync(new() { Key = key, Content = new MemoryStream([2]) }, deadline.Token);
            replaced.IsSuccess.ShouldBeTrue();
            var updated = await provider.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = replaced.Value.ETag }, deadline.Token);
            updated.IsSuccess.ShouldBeTrue();
            (await provider.DeleteAsync(key, new() { IfMatchETag = updated.Value.ETag }, deadline.Token)).IsSuccess.ShouldBeTrue();
            File.WriteAllText(mutated, "complete");
        }
        else
        {
            while (!File.Exists(mutated))
            {
                await Task.Delay(100, deadline.Token);
            }
        }

        oldReader.ReadByte().ShouldBe(1, "A reader on the independent peer must retain its old generation through replacement, metadata commit and deletion.");
    }

    private sealed class ShareDocument;
}