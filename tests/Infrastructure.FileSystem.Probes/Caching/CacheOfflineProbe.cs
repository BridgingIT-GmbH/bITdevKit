// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Globalization;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

/// <summary>Routes explicit operator-owned cache inspection, quiescent recovery and opt-in reset to the real offline APIs.</summary>
/// <example><code>cache-recover --root ROOT --store-id INSPECTED_ID --quiescent true</code></example>
public static class CacheOfflineProbe
{
    /// <summary>Inspects checked identity or performs an explicitly authorized offline recovery/reset on the selected dedicated root.</summary>
    /// <param name="command">cache-inspect, cache-recover, or cache-reset.</param>
    /// <param name="evidence">The existing local evidence output directory.</param>
    /// <param name="arguments">Explicit native profile settings and independently inspected identity/quiescence assertions.</param>
    /// <returns>Zero for completion, two when explicit reset is required, or one for a rejected or failed operation.</returns>
    public static async Task<int> RunAsync(string command, string evidence, IReadOnlyDictionary<string, string> arguments)
    {
        if (command is not ("cache-inspect" or "cache-recover" or "cache-reset") || !arguments.TryGetValue("--root", out var root))
        {
            throw new InvalidDataException("A cache offline command and explicit dedicated --root are required.");
        }

        var options = new CacheProviderOptions { Kind = "FileSystem", RootPath = root,
            AccessKind = arguments.GetValueOrDefault("--access") ?? "Local", Durability = arguments.GetValueOrDefault("--durability") ?? "Durable",
            MaxStorageBytes = Number(arguments, "--max-storage-bytes", new CacheProviderOptions().MaxStorageBytes), MaxEntries = Number(arguments, "--max-entries", new CacheProviderOptions().MaxEntries) };
        var recovery = new FileSystemCacheRecovery(options);
        if (command == "cache-inspect")
        {
            var inspected = await recovery.GetStateAsync();
            ProbeResult.Write(Path.Combine(evidence, "cache-inspection.json"), new { inspected.IsSuccess, state = inspected.Value,
                errors = inspected.Errors.OfType<CacheError>().Select(error => error.Code.ToString()).ToArray() });
            if (inspected.IsSuccess) { Console.WriteLine($"store-id={inspected.Value.StoreId}; generation={inspected.Value.RecoveryGeneration}; fenced={inspected.Value.RecoveryInProgress}"); }

            return inspected.IsSuccess ? 0 : 1;
        }

        if (arguments.GetValueOrDefault("--quiescent") != "true" || !arguments.TryGetValue("--store-id", out var storeId) || !Guid.TryParseExact(storeId, "N", out _))
        {
            throw new InvalidDataException("Stop or independently fence every participant and drain native work on every host, then pass the inspected --store-id and --quiescent true.");
        }

        var reset = arguments.GetValueOrDefault("--reset") == "true";
        if (command == "cache-reset" && !reset) { throw new InvalidDataException("Discarding the cache requires explicit --reset true in addition to identity and global quiescence."); }

        var result = reset ? await recovery.ResetQuiescentAsync(storeId, true) : await recovery.RecoverQuiescentAsync(storeId, true);
        ProbeResult.Write(Path.Combine(evidence, "cache-recovery.json"), new { operation = reset ? "reset" : "recover", result.IsSuccess, outcome = result.Value,
            errors = result.Errors.OfType<CacheError>().Select(error => error.Code.ToString()).ToArray(),
            quiescence = "Operator asserted every reader, writer, sweeper and recovery participant on every host is stopped or independently fenced, including native tails and server replays." });
        return result.IsFailure ? 1 : result.Value.Completed ? 0 : result.Value.RequiresReset ? 2 : 1;
    }

    private static long Number(IReadOnlyDictionary<string, string> arguments, string name, long fallback) => arguments.TryGetValue(name, out var value) ?
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result > 0 ? result : throw new InvalidDataException("Invalid finite cache quota option.") : fallback;
}