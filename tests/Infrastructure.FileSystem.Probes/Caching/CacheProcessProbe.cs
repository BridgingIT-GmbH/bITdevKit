// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

/// <summary>Terminates independent processes at actual advanced-cache provider boundaries and verifies production recovery.</summary>
/// <remarks>The persistent shared-marker profile runs on local scratch storage here. These results do not qualify SMB/NFS servers, independent hosts, disconnects or power loss.</remarks>
/// <example><code>var passed = await CacheProcessProbe.RunAsync(markedWorkspace);</code></example>
public static class CacheProcessProbe
{
    private static readonly string[] Boundaries = ["credit-reserved", "payload-staged", "index-staged", "journal-flushed", "manifest-published"];
    private static readonly CacheEntryIdentity Identity = new("probe", "items", "1", "key");

    /// <summary>Runs five production crash boundaries and four fence/torn-write/corruption regressions.</summary>
    /// <param name="workspace">An existing isolated scratch workspace with the repository probe ownership marker.</param>
    /// <returns>Exactly the nine preserved protocol case identifiers after their real provider assertions pass.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(string workspace)
    {
        ValidateWorkspace(workspace);
        var passed = new List<string>();
        foreach (var boundary in Boundaries)
        {
            var scenario = "cache-production-" + boundary;
            var directory = Path.Combine(workspace, scenario);
            Directory.CreateDirectory(directory);
            var options = Options(Path.Combine(directory, "store"), "Smb");
            var other = Enumerable.Range(0, 1024).Select(index => new CacheEntryIdentity("probe", "items", "1", "other-" + index))
                .First(identity => CacheStoreRoot.Shard(identity) != CacheStoreRoot.Shard(Identity));
            await using var provider = await ReadyAsync(options);
            Require((await WriteAsync(provider, Identity, [1])).StorageOutcome == CacheStorageOutcome.Stored, "Initial publication failed.");
            await WriteAsync(provider, other, [7]);
            var opened = Require(await provider.LookupAsync(Identity, new(OpenPayload: true, ObserveAccess: false)));
            var reader = opened.Handle;
            var recovery = new FileSystemCacheRecovery(options);
            var storeId = Require(await recovery.GetStateAsync()).StoreId;
            using var child = ProductionProcessProbe.Start(workspace, scenario, WorkerRuntimeConfig());
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await WaitAsync(Path.Combine(directory, "boundary.ready"), child, deadline.Token);
                var globalCommit = boundary == "manifest-published";
                var credits = globalCommit ? [] : await CreditsAsync(options);
                var controlProof = globalCommit ? await ControlProofAsync(options) : null;
                if (!globalCommit)
                {
                    Require(credits.Count > 0, "A paused native operation must retain durable credit.");
                    await WriteAsync(provider, other, [8]);
                    Require(await ReadAsync(provider, other) == 8, "An unrelated production shard could not progress.");
                }

                if (boundary is "index-staged" or "journal-flushed" or "manifest-published")
                {
                    var contender = await provider.LookupAsync(Identity, new(ObserveAccess: false));
                    Require(contender.IsFailure, "A live shard transaction admitted a same-shard contender.");
                }

                child.Kill(true);
                await child.WaitForExitAsync(deadline.Token);
                if (globalCommit)
                {
                    Require((await provider.GetQuotaAsync()).IsFailure, "A crashed shared root publication owner was reclaimed automatically.");
                    Require(await ControlProofAsync(options) == controlProof, "Uncertain prepared root accounting changed after process termination.");
                }

                foreach (var credit in credits)
                {
                    var retained = await CreditAsync(options, credit.Id);
                    Require(retained is not null && retained.ReservedBytes >= credit.ReservedBytes, "Uncertain native credit was released after process termination.");
                }

                Require(reader.Stream.ReadByte() == 1, "An acquired production reader changed immutable generation.");
                await CloseReaderAsync(reader);
                await provider.DisposeAsync();
                var recovered = Require(await recovery.RecoverQuiescentAsync(storeId, true, deadline.Token));
                Require(recovered.Completed && !recovered.RequiresReset, "A complete checked native crash boundary could not recover.");
                await using var reopened = await ReadyAsync(options);
                Require(await ReadAsync(reopened, Identity) == (boundary == "manifest-published" ? 2 : 1), "Recovery exposed a torn or uncommitted production generation.");
                Require(await ReadAsync(reopened, other) == (globalCommit ? 7 : 8), "Recovery changed the unrelated committed shard.");
                passed.Add(boundary);
            }
            finally
            {
                if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); }

                await CloseReaderAsync(reader);
            }
        }

        var fenceOptions = Options(Path.Combine(workspace, "cache-production-fences"));
        await using (var provider = await ReadyAsync(fenceOptions))
        {
            await WriteAsync(provider, Identity, [3]);
            var fence = Require(await provider.CaptureFenceAsync(Identity));
            Require(await provider.RemoveAsync(Identity));
            var late = await WriteWithFenceAsync(provider, Identity, [4], fence);
            Require(late.IsFailure || late.Value.StorageOutcome != CacheStorageOutcome.Stored, "Invalidation permitted a late publication to resurrect its key.");
            Require((await provider.LookupAsync(Identity, new())).Value.State == CacheReadValidationState.Missing, "Invalidation did not remove the visible generation.");
            Require(await provider.ReleaseFenceAsync(Identity, fence));
            passed.Add("invalidation-fence");
            var absent = new CacheEntryIdentity("probe", "items", "1", "absent");
            var absentFence = Require(await provider.CaptureFenceAsync(absent));
            Require(await provider.RemoveAsync(absent));
            var absentLate = await WriteWithFenceAsync(provider, absent, [5], absentFence);
            Require(absentLate.IsFailure || absentLate.Value.StorageOutcome != CacheStorageOutcome.Stored, "Absent-key invalidation permitted resurrection.");
            Require(await provider.ReleaseFenceAsync(absent, absentFence));
            passed.Add("absent-key-fence");
        }

        var tailOptions = Options(Path.Combine(workspace, "cache-production-tail"));
        await using (var provider = await ReadyAsync(tailOptions))
        {
            await WriteAsync(provider, Identity, [9]);
            var root = Root(tailOptions);
            var shard = root.ShardDirectory(Identity);
            var budget = Budget();
            using var manifest = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(shard, "manifest.json"), budget, budget, default);
            using (var journal = new FileStream(Path.Combine(shard, manifest.Value.Journal + ".journal"), FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                journal.Write([100, 0, 0, 0, 123, 34]);
                FileSystemDurability.Flush(journal);
            }

            Require(await ReadAsync(provider, Identity) == 9, "An uncommitted torn journal tail changed production visibility.");
            passed.Add("torn-uncommitted-tail");
            var manifestPath = Path.Combine(shard, "manifest.json");
            var bytes = await File.ReadAllBytesAsync(manifestPath);
            bytes[^1] ^= 1;
            await File.WriteAllBytesAsync(manifestPath, bytes);
            Require((await provider.LookupAsync(Identity, new())).IsFailure, "A damaged authoritative manifest was accepted.");
            await provider.DisposeAsync();
            var recovery = new FileSystemCacheRecovery(tailOptions);
            var storeId = Require(await recovery.GetStateAsync()).StoreId;
            var damaged = Require(await recovery.RecoverQuiescentAsync(storeId, true));
            Require(!damaged.Completed && damaged.RequiresReset, "Unreadable authoritative metadata was replaced by older content.");
            var reset = Require(await recovery.ResetQuiescentAsync(storeId, true));
            Require(reset.Completed && reset.StoreId != storeId && reset.DiscardedShards > 0, "Explicit reset did not replace the damaged cache scope.");
            await using var reopened = await ReadyAsync(tailOptions);
            Require(Require(await reopened.LookupAsync(Identity, new())).State == CacheReadValidationState.Missing, "Reset retained discarded cache data.");
            passed.Add("checked-manifest-corruption");
        }

        return passed;
    }

    /// <summary>Runs one real provider write until the parent terminates this worker at its requested native boundary.</summary>
    /// <param name="workspace">The existing parent-owned marked workspace.</param>
    /// <param name="scenario">A controlled cache-production or legacy cache-prototype boundary identifier.</param>
    /// <returns>A failure code if execution unexpectedly passes its termination boundary.</returns>
    public static async Task<int> WorkerAsync(string workspace, string scenario)
    {
        ValidateWorkspace(workspace);
        var prefix = scenario.StartsWith("cache-production-", StringComparison.Ordinal) ? "cache-production-" : "cache-prototype-";
        var boundary = scenario.StartsWith(prefix, StringComparison.Ordinal) ? scenario[prefix.Length..] : null;
        Require(Boundaries.Contains(boundary, StringComparer.Ordinal), "Invalid production cache boundary.");
        var directory = Path.Combine(workspace, scenario);
        await using var provider = await ReadyAsync(Options(Path.Combine(directory, "store"), "Smb"));
        var fence = Require(await provider.CaptureFenceAsync(Identity));
        provider.Fault = point =>
        {
            if (point != boundary) { return; }

            File.WriteAllText(Path.Combine(directory, "boundary.ready"), point);
            Thread.Sleep(Timeout.Infinite); // The parent confirms this exact native hook, then kills this disposable process.
        };
        await WriteWithFenceAsync(provider, Identity, [2], fence);
        return 1;
    }

    /// <summary>Creates the same explicit native profile used by every participant in a production probe.</summary>
    /// <param name="root">The dedicated isolated cache directory.</param>
    /// <param name="access">Local, Smb, or Nfs; a shared profile on local disk provides protocol evidence only.</param>
    /// <returns>Finite code-owned native options.</returns>
    public static CacheProviderOptions Options(string root, string access = "Local") => new()
    {
        Kind = "FileSystem", RootPath = root, AccessKind = access, MaxStorageBytes = 64 * CacheDefaults.MiB,
        MaxResidentBytes = 64 * CacheDefaults.MiB, MetadataCacheBytes = 8 * CacheDefaults.MiB, LockAcquireTimeout = TimeSpan.FromMilliseconds(100)
    };

    /// <summary>Initializes an actual complete provider without recovery or request-time schema changes.</summary>
    /// <param name="options">The common native profile.</param>
    /// <returns>An owned ready provider.</returns>
    public static async Task<FileSystemCacheStoreProvider> ReadyAsync(CacheProviderOptions options)
    {
        var provider = new FileSystemCacheStoreProvider("files", options);
        Require(await provider.InitializeAsync([new("probe", "items", "1", "RAW", [], new() { Limits = new() { MaxStorageBytes = options.MaxStorageBytes } })]));
        return provider;
    }

    /// <summary>Publishes bytes using a production captured fence and same-store staging session.</summary>
    /// <param name="provider">The complete native provider.</param>
    /// <param name="identity">The canonical probe identity.</param>
    /// <param name="bytes">The complete immutable native payload.</param>
    /// <returns>The confirmed native write outcome.</returns>
    public static async Task<CacheWriteOutcome> WriteAsync(FileSystemCacheStoreProvider provider, CacheEntryIdentity identity, byte[] bytes)
    {
        var fence = Require(await provider.CaptureFenceAsync(identity));
        try { return Require(await WriteWithFenceAsync(provider, identity, bytes, fence)); }
        finally { Require(await provider.ReleaseFenceAsync(identity, fence)); }
    }

    /// <summary>Reads one complete native payload and verifies its exact stored hash.</summary>
    /// <param name="provider">The complete native provider.</param>
    /// <param name="identity">The selected canonical identity.</param>
    /// <returns>The single-byte probe value, after full snapshot verification.</returns>
    public static async Task<byte> ReadAsync(FileSystemCacheStoreProvider provider, CacheEntryIdentity identity)
    {
        var found = Require(await provider.LookupAsync(identity, new(OpenPayload: true, ObserveAccess: false)));
        Require(found.State == CacheReadValidationState.Valid && found.Handle is not null, "The production payload is unavailable.");
        await using var handle = found.Handle;
        using var destination = new MemoryStream();
        await handle.Stream.CopyToAsync(destination);
        var bytes = destination.ToArray();
        Require(bytes.Length == 1 && Convert.ToHexStringLower(SHA256.HashData(bytes)) == found.Metadata.StoredHash, "The production payload does not match its committed metadata.");
        return bytes[0];
    }

    private static async Task<Result<CacheWriteOutcome>> WriteWithFenceAsync(FileSystemCacheStoreProvider provider, CacheEntryIdentity identity, byte[] bytes, CachePublicationFence fence)
    {
        var begun = await provider.BeginWriteAsync(new(identity, fence, default, CacheDefaults.MiB));
        if (begun.IsFailure) { return Result<CacheWriteOutcome>.Failure(begun); }

        await using var session = begun.Value;
        await session.Stream.WriteAsync(bytes);
        var now = DateTimeOffset.UtcNow;
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return await session.PublishAsync(new() { Identity = identity, Version = session.Version, ContractId = "RAW", CodecId = "RAW",
            PublishedAt = now, ExpiresAt = now.AddMinutes(5), AbsoluteExpiresAt = now.AddMinutes(5), LogicalSizeBytes = bytes.Length,
            EncodedSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length, StoredHash = hash, PayloadHash = hash });
    }

    private static async Task<string> ControlProofAsync(CacheProviderOptions options)
    {
        var path = Root(options).Resolve("ledger", "manifest.json");
        var budget = Budget();
        using var manifest = await CacheCheckedMetadata.ReadAsync<CacheManifest>(path, budget, budget, default);
        // The worker is paused inside the global publication guard. This checked immutable
        // control snapshot therefore cannot change until that owner ends or recovery fences it.
        return Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path)));
    }

    private static async Task<IReadOnlyList<CacheSpaceCredit>> CreditsAsync(CacheProviderOptions options)
    {
        var budget = Budget();
        var root = Root(options);
        using var header = await root.OpenAsync(default);
        using var blocks = new CacheIndexBlockCache(4 * CacheDefaults.MiB);
        var ledger = new CacheRootQuotaLedger(root, header.Value, header.Bytes, budget, budget, blocks, TimeProvider.System);
        using var page = await ledger.GetCreditsAsync(null, 128, default);
        return page.Credits.Where(credit => credit.State is CacheSpaceCreditState.Reserved or CacheSpaceCreditState.Prepared).ToArray();
    }

    private static async Task<CacheSpaceCredit> CreditAsync(CacheProviderOptions options, string id)
    {
        var budget = Budget();
        var root = Root(options);
        using var header = await root.OpenAsync(default);
        using var blocks = new CacheIndexBlockCache(4 * CacheDefaults.MiB);
        return await new CacheRootQuotaLedger(root, header.Value, header.Bytes, budget, budget, blocks, TimeProvider.System).GetCreditAsync(id, default);
    }

    private static ResourceBudget Budget() => new("production-probe", 64 * CacheDefaults.MiB);

    private static CacheStoreRoot Root(CacheProviderOptions options) => new(new(options), Budget(), Budget());

    private static async ValueTask CloseReaderAsync(ICacheProviderReadHandle reader)
    {
        try { await reader.DisposeAsync(); }
        catch (FileSystemAuthorityTimeoutException)
        {
            // The immutable file is already closed. A killed shared shard owner can prevent pin
            // metadata cleanup, which stays charged until explicit globally quiescent recovery.
            Require(!reader.Stream.CanRead, "A failed pin cleanup left native reader I/O active.");
        }
    }

    private static string WorkerRuntimeConfig()
    {
        // Executable project references copy the probe app into test output, whose shared-framework
        // runtime assets intentionally follow the test runtimeconfig rather than the probe's own one.
        var candidate = Path.Combine(Path.GetDirectoryName(typeof(CacheProcessProbe).Assembly.Location), "BridgingIT.DevKit.Infrastructure.UnitTests.runtimeconfig.json");
        return File.Exists(candidate) ? candidate : null;
    }

    private static async Task WaitAsync(string path, System.Diagnostics.Process child, CancellationToken token)
    {
        while (!File.Exists(path))
        {
            if (child.HasExited) { throw new InvalidDataException("The production worker exited before its requested native boundary."); }

            await Task.Delay(20, token);
        }
    }

    private static void ValidateWorkspace(string workspace)
    {
        Require(Path.IsPathFullyQualified(workspace), "A marked absolute scratch workspace is required.");
        ProbeWorkspace.RejectLinks(workspace);
        Require(File.Exists(Path.Combine(workspace, "run.json")) && File.ReadAllText(Path.Combine(workspace, "run.json")).StartsWith("bdk-filesystem-probes/1/", StringComparison.Ordinal), "The probe ownership marker is missing.");
    }

    private static T Require<T>(Result<T> result)
    {
        Require((IResult)result);
        return result.Value;
    }

    private static void Require(IResult result) => Require(result.IsSuccess, "Production provider failure: " + string.Join(',', result.Errors.OfType<CacheError>().Select(error => error.Code)));

    private static void Require(bool condition, string reason)
    {
        if (!condition) { throw new InvalidDataException(reason); }
    }
}