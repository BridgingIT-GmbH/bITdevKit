// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

/// <summary>Dispatches application benchmarks with an optional explicit finite in-process whole-case budget.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheProviderBenchmarks*'</code></example>
public static class Program
{
    /// <summary>Runs the selected workloads; BDK_CACHE_BENCH_TIMEOUT_SECONDS selects a bounded in-process toolchain when setup requires a longer budget.</summary>
    /// <param name="args">The ordinary BenchmarkDotNet command-line options.</param>
    /// <exception cref="ArgumentOutOfRangeException">The explicit case budget is outside 30 seconds through 24 hours.</exception>
    /// <example><code>$env:BDK_CACHE_BENCH_TIMEOUT_SECONDS = '900'; dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter '*CacheProviderBenchmarks.GetHitAsync*'</code></example>
    public static void Main(string[] args)
    {
        var timeoutText = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_TIMEOUT_SECONDS");
        if (string.IsNullOrWhiteSpace(timeoutText))
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return;
        }

        if (!int.TryParse(timeoutText, out var seconds) || seconds is < 30 or > 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "BDK_CACHE_BENCH_TIMEOUT_SECONDS must be between 30 and 86400.");
        }

        var job = Job.Default.WithToolchain(new InProcessEmitToolchain(TimeSpan.FromSeconds(seconds), logOutput: true))
            .WithId("BoundedInProcess");
        var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(job);
        // The selected toolchain already runs in process; the CLI flag would replace its explicit budget.
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Where(arg => arg != "--inProcess").ToArray(), configuration);
    }
}