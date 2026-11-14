// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks actual share inaccessibility and restored cache fencing across two already verified hosts.</summary>
/// <remarks>Operators apply and restore the external fault. Local TEMP acknowledgments cannot substitute for observed failed share I/O and source-free cache failures. This command does not qualify power loss.</remarks>
/// <example><code>cache-shared --profile SHARE --role controller --contract disconnect-reconnect --run-id RUN_ID</code></example>
public static class CacheSharedFaultProbe
{
    /// <summary>Keeps an application-cache host alive through an explicitly acknowledged external disconnect and restoration.</summary>
    /// <param name="evidence">The host's local evidence directory.</param>
    /// <param name="runId">The fresh shared run identity already verified by the role protocol.</param>
    /// <param name="role">Controller or peer in lower case.</param>
    /// <param name="workspace">The marked workspace on the actual qualified share.</param>
    /// <param name="accessKind">The observed Smb or Nfs profile.</param>
    /// <param name="cancellationToken">The original finite overall run deadline.</param>
    /// <returns>Zero after inaccessibility, restored visibility and stale-publication fencing have actually passed.</returns>
    /// <example><code>await CacheSharedFaultProbe.RunAsync(evidence, runId, role, workspace, "Smb", token);</code></example>
    public static async Task<int> RunAsync(string evidence, string runId, string role, string workspace, string accessKind, CancellationToken cancellationToken)
    {
        if (accessKind is not ("Smb" or "Nfs") || role is not ("controller" or "peer")) { throw new InvalidDataException("An actual shared profile and fixed role are required."); }

        var control = ProbeWorkspace.Open(Path.GetTempPath(), runId, "cache-fault-" + role, true);
        var directory = Path.Combine(workspace, "cache-contract-disconnect-reconnect");
        var sentinel = Path.Combine(workspace, "fault-sentinel.json");
        if (role == "controller")
        {
            Directory.CreateDirectory(directory);
            Write(sentinel, new { runId });
        }
        else { await WaitAsync(sentinel, cancellationToken); }

        await using var services = CacheSharedProbe.CreateLocalServices(directory, CacheCoordinationScope.SharedStore, accessKind, TimeSpan.FromMinutes(10));
        var cache = services.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        if (role == "controller")
        {
            var stored = Required(await cache.SetAsync(new("fault-baseline"), runId, cancellationToken: cancellationToken));
            if (stored.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("The baseline was not retained."); }

            Write(Path.Combine(workspace, "fault-baseline.json"), new { runId });
        }
        else { await WaitAsync(Path.Combine(workspace, "fault-baseline.json"), cancellationToken); }

        var baseline = Required(await cache.GetAsync("fault-baseline", cancellationToken));
        if (!baseline.Found || baseline.Value != runId || !baseline.Version.HasValue) { throw new InvalidDataException("The retained baseline is unavailable."); }

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Result<CacheLookup<string>>> old = null;
        var calls = 0;
        if (role == "controller")
        {
            old = cache.GetOrLoadAsync(new("fault-held"), async (_, token) =>
            {
                Interlocked.Increment(ref calls);
                Write(Path.Combine(workspace, "fault-owner.json"), new { runId });
                await release.Task.WaitAsync(token);
                return Result<CacheLoadResult<string>>.Success(new("before-disconnect"));
            }, cancellationToken).AsTask();
        }

        await WaitAsync(Path.Combine(workspace, "fault-owner.json"), cancellationToken);
        Write(Path.Combine(control, "armed.json"), new { runId, role, processId = Environment.ProcessId, control,
            next = "Apply the actual mount/server fault on both clients, then create disconnected.json here with this runId." });
        Console.WriteLine($"cache-fault role={role}; control={control}");
        Task<byte[]> observedIo = null;
        try
        {
            await AcknowledgeAsync(control, "disconnected", runId, cancellationToken);
            // Exactly one bounded probe can remain in native I/O until the operator restores the mount.
            observedIo = Task.Run(() => ReadSentinel(sentinel));
            var inaccessible = false;
            try
            {
                await observedIo.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException) { inaccessible = true; }

            if (!inaccessible) { throw new InvalidDataException("The shared sentinel is still readable; an acknowledgment alone cannot qualify a disconnect."); }

            var outageCalls = 0;
            var failure = await cache.GetOrLoadAsync(new("fault-check-" + role), (_, _) =>
            {
                Interlocked.Increment(ref outageCalls);
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("unexpected source")));
            }, cancellationToken);
            if (failure.IsSuccess || outageCalls != 0 || !failure.Errors.OfType<CacheError>().Any(error =>
                error.Code is CacheErrorCode.ProviderUnavailable or CacheErrorCode.CoordinationTimeout))
            {
                throw new InvalidDataException("Shared authority inaccessibility did not fail explicitly before source invocation.");
            }

            Write(Path.Combine(control, "unavailable.checked.json"), new { runId, role, outageSourceCallbacks = outageCalls,
                failures = failure.Errors.OfType<CacheError>().Select(error => error.Code.ToString()).ToArray(),
                next = "Restore the actual mount/server on both clients, then create restored.json here with this runId." });
            await AcknowledgeAsync(control, "restored", runId, cancellationToken);
            try { await observedIo.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }

            var visible = Required(await cache.GetAsync("fault-baseline", cancellationToken));
            if (!visible.Found || visible.Value != runId || visible.Version != baseline.Version) { throw new InvalidDataException("Restore lost or replaced the independently valid baseline snapshot."); }

            Write(Path.Combine(workspace, "fault-restored-" + role + ".json"), new { runId });
            await WaitAsync(Path.Combine(workspace, "fault-restored-" + (role == "controller" ? "peer" : "controller") + ".json"), cancellationToken);
            if (role == "peer")
            {
                Required(await cache.RemoveAsync("fault-held", cancellationToken));
                var fresh = Required(await cache.GetOrLoadAsync(new("fault-held"), (_, _) =>
                {
                    Interlocked.Increment(ref calls);
                    return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("after-disconnect")));
                }, cancellationToken));
                if (!fresh.Found || fresh.Value != "after-disconnect" || !fresh.Version.HasValue || fresh.StorageOutcome != CacheStorageOutcome.Stored)
                {
                    throw new InvalidDataException("The restored peer did not publish a fresh fenced version.");
                }

                Write(Path.Combine(workspace, "fault-replacement.json"), new { runId });
                await WaitAsync(Path.Combine(workspace, "fault-old-finished.json"), cancellationToken);
            }
            else
            {
                await WaitAsync(Path.Combine(workspace, "fault-replacement.json"), cancellationToken);
                release.TrySetResult();
                var completed = await old;
                if (completed.IsSuccess && (!completed.Value.Found || completed.Value.Value != "before-disconnect" ||
                    completed.Value.StorageOutcome == CacheStorageOutcome.Stored || completed.Value.Version.HasValue) ||
                    completed.IsFailure && !completed.Errors.OfType<CacheError>().Any(error => error.Code is CacheErrorCode.ProviderUnavailable or CacheErrorCode.CoordinationTimeout or CacheErrorCode.Conflict))
                {
                    throw new InvalidDataException("The pre-disconnect source obtained a publication token after the replacement fence.");
                }

                Write(Path.Combine(workspace, "fault-old-finished.json"), new { runId });
            }

            var current = Required(await cache.GetAsync("fault-held", cancellationToken));
            if (!current.Found || current.Value != "after-disconnect" || !current.Version.HasValue || calls != 1)
            {
                throw new InvalidDataException("The final retained version or exact role source count was not proven.");
            }

            ProbeResult.Write(Path.Combine(evidence, "cache-disconnect-" + role + ".json"), new
            {
                status = "passed", runId, role, accessKind, sourceCallbacks = calls, outageSourceCallbacks = 0,
                observedShareInaccessible = true, baselineVersionPreserved = true, stalePublicationRejected = true, powerLossTested = false
            });
            return 0;
        }
        finally { release.TrySetResult(); }
    }

    private static byte[] ReadSentinel(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[129];
        var count = stream.ReadAtLeast(bytes, bytes.Length, false);
        if (count > 128) { throw new InvalidDataException("The share sentinel exceeds its bound."); }

        return bytes[..count];
    }

    private static async Task AcknowledgeAsync(string control, string phase, string runId, CancellationToken token)
    {
        var path = Path.Combine(control, phase + ".json");
        await WaitAsync(path, token);
        if (new FileInfo(path).Length > 1024) { throw new InvalidDataException("A local fault acknowledgment exceeds its bound."); }

        using var value = JsonDocument.Parse(await File.ReadAllBytesAsync(path, token), new JsonDocumentOptions { MaxDepth = 4 });
        if (value.RootElement.GetProperty("runId").GetString() != runId) { throw new InvalidDataException("A fault acknowledgment belongs to a different run."); }
    }

    private static T Required<T>(Result<T> result) => result.IsSuccess ? result.Value : throw new InvalidDataException("A cache fault-contract operation failed.");

    private static void Required(Result result)
    {
        if (result.IsFailure) { throw new InvalidDataException("A cache fault-contract mutation failed."); }
    }

    private static void Write(string path, object value)
    {
        ProbeResult.Write(path + ".tmp", value);
        FileSystemDurability.FlushFile(path + ".tmp");
        FileSystemDurability.Move(path + ".tmp", path, false, true);
    }

    private static async Task WaitAsync(string path, CancellationToken token)
    {
        while (!File.Exists(path)) { await Task.Delay(50, token); }
    }
}