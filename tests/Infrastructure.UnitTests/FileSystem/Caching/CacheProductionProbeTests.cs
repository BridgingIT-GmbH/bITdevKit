// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

/// <summary>Checks production cache probe contracts and explicit identity/quiescence/reset CLI boundaries.</summary>
public sealed class CacheProductionProbeTests
{
    [SkippableFact]
    public async Task Contract_ProductionLocalProvider_ExercisesActualApplicationApis()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
        using var workspace = new Scratch();

        var passed = await CacheContractProbe.RunAsync(workspace.Path);

        passed.Count.ShouldBe(5);
        passed.ShouldContain("binary-verified-transfer");
        passed.ShouldContain("node-local-single-source");
    }

    [SkippableFact]
    public async Task RecoveryCli_IdentityAndGlobalQuiescence_RejectsBeforeChangingRoot()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
        using var workspace = new Scratch();
        var options = CacheProcessProbe.Options(System.IO.Path.Combine(workspace.Path, "store"));
        await using (var provider = await CacheProcessProbe.ReadyAsync(options))
        {
            await CacheProcessProbe.WriteAsync(provider, new("probe", "items", "1", "key"), [1]);
        }

        var recovery = new FileSystemCacheRecovery(options);
        var original = (await recovery.GetStateAsync()).Value;
        var missingProof = Arguments("cache-recover", options, "--store-id", original.StoreId);
        var wrongIdentity = Arguments("cache-recover", options, "--store-id", Guid.NewGuid().ToString("N"), "--quiescent", "true");

        (await RunAsync(missingProof)).ShouldBe(1);
        (await RunAsync(wrongIdentity)).ShouldBe(1);

        var unchanged = (await recovery.GetStateAsync()).Value;
        unchanged.StoreId.ShouldBe(original.StoreId);
        unchanged.RecoveryGeneration.ShouldBe(original.RecoveryGeneration);
        unchanged.RecoveryInProgress.ShouldBeFalse();
    }

    [SkippableFact]
    public async Task ResetCli_ExplicitResetRequired_ReportsActualDiscardedScope()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
        using var workspace = new Scratch();
        var options = CacheProcessProbe.Options(System.IO.Path.Combine(workspace.Path, "store"));
        await using (var provider = await CacheProcessProbe.ReadyAsync(options))
        {
            await CacheProcessProbe.WriteAsync(provider, new("probe", "items", "1", "key"), [1]);
        }

        var recovery = new FileSystemCacheRecovery(options);
        var original = (await recovery.GetStateAsync()).Value;
        (await RunAsync(Arguments("cache-reset", options, "--store-id", original.StoreId, "--quiescent", "true"))).ShouldBe(1);
        (await recovery.GetStateAsync()).Value.StoreId.ShouldBe(original.StoreId);

        (await RunAsync(Arguments("cache-reset", options, "--store-id", original.StoreId, "--quiescent", "true", "--reset", "true"))).ShouldBe(0);

        (await recovery.GetStateAsync()).Value.StoreId.ShouldNotBe(original.StoreId);
        await using var reopened = await CacheProcessProbe.ReadyAsync(options);
        (await reopened.LookupAsync(new("probe", "items", "1", "key"), new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    [SkippableFact]
    public async Task InspectionCli_ExistingCheckedRoot_LeavesGenerationAndDataUnchanged()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
        using var workspace = new Scratch();
        var options = CacheProcessProbe.Options(System.IO.Path.Combine(workspace.Path, "store"));
        await using var provider = await CacheProcessProbe.ReadyAsync(options);
        await CacheProcessProbe.WriteAsync(provider, new("probe", "items", "1", "key"), [1]);
        var recovery = new FileSystemCacheRecovery(options);
        var before = (await recovery.GetStateAsync()).Value;

        (await RunAsync(Arguments("cache-inspect", options))).ShouldBe(0);

        (await recovery.GetStateAsync()).Value.ShouldBe(before);
        (await CacheProcessProbe.ReadAsync(provider, new("probe", "items", "1", "key"))).ShouldBe((byte)1);
    }

    [Fact]
    public async Task SharedCli_NoExplicitOptIn_ReportsUnavailableWithoutShareAccess()
    {
        var runId = Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Repository() };
        foreach (var argument in new[] { "exec", "--runtimeconfig", RuntimeConfig(), typeof(CacheProcessProbe).Assembly.Location, "cache-shared", "--run-id", runId, "--profile", "unavailable" }) { start.ArgumentList.Add(argument); }

        start.Environment["BDK_FILESYSTEM_SHARE_TESTS"] = "0";
        using var process = Process.Start(start);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        process.ExitCode.ShouldBe(2);
        var path = System.IO.Path.Combine(Repository(), ".audit", "filesystem-storage", runId, "cache-shared.json");
        using var evidence = JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        evidence.RootElement.GetProperty("status").GetString().ShouldBe("unavailable");
    }

    private static async Task<int> RunAsync(string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Repository() };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(RuntimeConfig());
        start.ArgumentList.Add(typeof(CacheProcessProbe).Assembly.Location);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }

        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        await Task.WhenAll(output, error);
        return process.ExitCode;
    }

    private static string RuntimeConfig() => System.IO.Path.ChangeExtension(typeof(CacheProductionProbeTests).Assembly.Location, ".runtimeconfig.json");

    private static string Repository()
    {
        for (var current = AppContext.BaseDirectory; current is not null; current = System.IO.Path.GetDirectoryName(System.IO.Path.TrimEndingDirectorySeparator(current)))
        {
            if (File.Exists(System.IO.Path.Combine(current, "bITdevKit.slnx"))) { return current; }
        }

        throw new DirectoryNotFoundException("The probe tests require the repository source checkout.");
    }

    private static string[] Arguments(string command, CacheProviderOptions options, params string[] controls) =>
        [command, "--run-id", Guid.NewGuid().ToString("N"), "--root", options.RootPath, "--max-storage-bytes", options.MaxStorageBytes.ToString(CultureInfo.InvariantCulture), "--max-entries", options.MaxEntries.ToString(CultureInfo.InvariantCulture), .. controls];

    private sealed class Scratch : IDisposable
    {
        private readonly string runId = Guid.NewGuid().ToString("N");

        /// <summary>Creates an isolated marked workspace under the operating system scratch root.</summary>
        public Scratch() => this.Path = ProbeWorkspace.Open(System.IO.Path.GetTempPath(), this.runId, "cache-production-test", true);

        /// <summary>Gets the exact marked workspace owned by this fixture.</summary>
        public string Path { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            ProbeWorkspace.Open(System.IO.Path.GetTempPath(), this.runId, "cache-production-test", false);
            ProbeWorkspace.RejectLinks(this.Path);
            Directory.Delete(this.Path, true);
        }
    }
}