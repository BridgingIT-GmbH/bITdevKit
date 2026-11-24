// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises ordinary application-cache contracts through the complete production filesystem provider.</summary>
/// <example><code>var passed = await CacheContractProbe.RunAsync(markedWorkspace);</code></example>
public static class CacheContractProbe
{
    /// <summary>Runs typed/negative/binary/single-flight/clear checks on an isolated local production store.</summary>
    /// <param name="workspace">The marked repository probe scratch directory.</param>
    /// <returns>The actual passed ordinary application API scenarios; no network/durability qualification is implied.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(string workspace)
    {
        ProbeWorkspace.RejectLinks(workspace);
        if (!File.Exists(Path.Combine(workspace, "run.json"))) { throw new InvalidDataException("A marked cache probe workspace is required."); }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithFileSystemProvider()
            .WithProvider("files", provider => provider.With(x => x.Kind, "FileSystem").With(x => x.RootPath, Path.Combine(workspace, "contract-store")))
            .WithValueContract<string>("text").WithBinaryContract("raw")
            .Defaults(collection => collection.With(x => x.Provider, "files").With(x => x.NegativeCaching.Enabled, true))
            .WithCollection<string>("items").WithBinaryCollection("binary");
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IApplicationCache>();
        var items = cache.GetCollection<string>("items");
        Require((await items.SetAsync(new("card"), "ready")).IsSuccess, "Typed production write failed.");
        Require((await items.GetAsync("CARD")).Value.Value == "ready", "Typed production read lost canonical key semantics.");
        var negativeCalls = 0;
        for (var index = 0; index < 2; index++)
        {
            var result = await items.GetOrLoadAsync(new("missing"), (_, _) =>
            {
                negativeCalls++;
                return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound()));
            });
            Require(result.IsSuccess && result.Value.IsNotFound, "Confirmed negative production read failed.");
        }

        Require(negativeCalls == 1, "A retained production negative marker reloaded its source.");
        var binary = cache.GetBinaryCollection("binary");
        using var source = new MemoryStream([1, 2, 3]);
        Require((await binary.SetAsync(new("payload"), source)).IsSuccess, "Binary production write failed.");
        var acquired = await binary.OpenReadAsync("payload");
        Require(acquired.IsSuccess && acquired.Value.Found, "Binary production acquisition failed.");
        await using (var handle = acquired.Value.Value)
        {
            await handle.Stream.CopyToAsync(Stream.Null);
            Require((await handle.WaitForCompletionAsync()).Value == CacheReadCompletion.Validated, "Binary production integrity did not complete.");
        }

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var owner = items.GetOrLoadAsync(new("expensive"), async (_, token) =>
        {
            calls++;
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("loaded"));
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var followers = Enumerable.Range(0, 3).Select(_ => items.GetOrLoadAsync(new("expensive"), (_, _) => throw new InvalidOperationException("A joined callback executed.")).AsTask()).ToArray();
        release.SetResult();
        var completed = await Task.WhenAll(followers.Prepend(owner));
        Require(calls == 1 && completed.All(result => result.IsSuccess && result.Value.Value == "loaded"), "Production node-local single-flight did not retain one owned source outcome.");
        var operations = provider.GetRequiredService<CacheOperationService>();
        var submitted = await operations.SubmitAsync(new(["items"], CacheSelection.None, Clear: true));
        Require(submitted.IsSuccess && submitted.Value.Single().IsSuccess, "Tracked production clear was not admitted.");
        var status = submitted.Value.Single().Value.Status;
        var binding = provider.GetRequiredService<CacheCatalog>().Collections["items"];
        var runner = provider.GetRequiredService<CacheOperationRunner>();
        for (var unit = 0; unit < 512 && status.State != CacheOperationState.Completed; unit++)
        {
            var continued = await runner.RunUnitAsync(binding, status.Id);
            Require(continued.IsSuccess, "A tracked production clear continuation failed.");
            status = continued.Value;
        }

        Require(status.State == CacheOperationState.Completed, "Tracked production clear did not complete within its finite fixture allowance.");
        var removed = await items.GetAsync("card");
        Require(removed.IsSuccess && !removed.Value.Found, "Production clear did not invalidate its selected collection: " + string.Join(',', removed.Errors.OfType<CacheError>().Select(error => error.Code)));
        return ["typed-canonical-roundtrip", "confirmed-negative", "binary-verified-transfer", "node-local-single-source", "collection-clear"];
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) { throw new InvalidDataException(reason); }
    }
}