// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using System.Diagnostics;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;

/// <summary>
/// Runs a bounded local workload that produces observable CPU, allocation, heap, and GC pressure.
/// </summary>
/// <param name="logger">The structured logger.</param>
/// <param name="measurements">The Profiling measurement service.</param>
/// <param name="profile">An optional workload profile; the bounded local defaults are used otherwise.</param>
/// <example>
/// Register this job with a development-only manual trigger, start Profiling, and dispatch the job
/// from the Jobs dashboard.
/// </example>
public sealed class WeatherProfilingStressJob(
    ILogger<WeatherProfilingStressJob> logger,
    IProfilingMeasurementService measurements,
    WeatherProfilingStressProfile profile = null
) : JobBase
{
    private readonly WeatherProfilingStressProfile profile =
        profile ?? new WeatherProfilingStressProfile();

    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<Unit> context,
        CancellationToken cancellationToken = default
    )
    {
        Validate(this.profile);
        StressSummary summary = null;

        logger.LogInformation(
            "[ProfilingStress] workload started (workers={WorkerCount}, cpuDurationMs={CpuDurationMs}, allocationMiB={AllocationMiB}, retainedMiB={RetainedMiB})",
            this.profile.WorkerCount,
            this.profile.CpuDuration.TotalMilliseconds,
            this.profile.AllocationBytes / 1_048_576,
            this.profile.RetainedBytes / 1_048_576
        );

        var measurementResult = await measurements
            .MeasureAsync(
                "WeatherFiesta profiling stress",
                async token =>
                {
                    summary = await RunWorkloadAsync(this.profile, logger, token)
                        .ConfigureAwait(false);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (measurementResult.IsFailure)
        {
            logger.LogWarning(
                "[ProfilingStress] workload could not be measured (errors={Errors})",
                string.Join("; ", measurementResult.Errors.Select(error => error.Message))
            );

            return Result.Failure(measurementResult.Messages, measurementResult.Errors);
        }

        var message =
            $"WeatherFiesta profiling stress completed. Workers={this.profile.WorkerCount}, "
            + $"CpuDuration={this.profile.CpuDuration.TotalSeconds:N1}s, "
            + $"Allocated={summary.AllocatedBytes / 1_048_576:N0}MiB, "
            + $"Retained={summary.RetainedBytes / 1_048_576:N0}MiB, "
            + $"Checksum={summary.Checksum:x16}.";
        context.Messages.Add(message);
        logger.LogInformation("[ProfilingStress] workload completed ({Summary})", message);

        return Result.Success(message);
    }

    private static async Task<StressSummary> RunWorkloadAsync(
        WeatherProfilingStressProfile profile,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[ProfilingStress] CPU saturation phase started");
        var workers = Enumerable
            .Range(0, profile.WorkerCount)
            .Select(worker =>
                Task.Factory.StartNew(
                    () => BurnCpu(worker, profile.CpuDuration, cancellationToken),
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                )
            )
            .ToArray();
        var checksums = await Task.WhenAll(workers).ConfigureAwait(false);
        var checksum = checksums.Aggregate(0UL, (current, value) => current ^ value);

        logger.LogInformation("[ProfilingStress] managed allocation phase started");
        var retained = await AllocateAsync(profile, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "[ProfilingStress] forced full GC and post-GC retention phase started"
        );
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            blocking: true,
            compacting: false
        );
        await Task.Delay(profile.PostGcHoldDuration, cancellationToken).ConfigureAwait(false);
        GC.KeepAlive(retained);

        return new(checksum, profile.AllocationBytes, retained.Sum(buffer => (long)buffer.Length));
    }

    private static ulong BurnCpu(
        int worker,
        TimeSpan duration,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var value = 0x9e3779b97f4a7c15UL ^ (uint)worker;
        while (stopwatch.Elapsed < duration)
        {
            for (var iteration = 0; iteration < 16_384; iteration++)
            {
                value = unchecked((value ^ (value >> 27)) * 0x3c79ac492ba7b653UL);
                value = unchecked((value ^ (value >> 33)) * 0x1c69b3f74ac4ae35UL);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return value;
    }

    private static async Task<IReadOnlyList<byte[]>> AllocateAsync(
        WeatherProfilingStressProfile profile,
        CancellationToken cancellationToken
    )
    {
        var retained = new List<byte[]>((int)Math.Ceiling(
            (double)profile.RetainedBytes / profile.AllocationBlockBytes
        ));
        long allocatedBytes = 0;
        long retainedBytes = 0;
        var batchBytes = 0;
        var blockIndex = 0;
        while (allocatedBytes < profile.AllocationBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blockSize = (int)Math.Min(
                profile.AllocationBlockBytes,
                profile.AllocationBytes - allocatedBytes
            );
            var buffer = GC.AllocateUninitializedArray<byte>(blockSize);
            for (var offset = 0; offset < buffer.Length; offset += 4096)
            {
                buffer[offset] = unchecked((byte)(blockIndex + offset));
            }

            buffer[^1] = unchecked((byte)blockIndex);
            allocatedBytes += buffer.Length;
            batchBytes += buffer.Length;
            blockIndex++;

            if (retainedBytes < profile.RetainedBytes)
            {
                retained.Add(buffer);
                retainedBytes += buffer.Length;
            }

            if (batchBytes >= profile.AllocationBatchBytes)
            {
                batchBytes = 0;
                await Task.Delay(profile.AllocationBatchDelay, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return retained;
    }

    private static void Validate(WeatherProfilingStressProfile profile)
    {
        if (
            profile.WorkerCount <= 0
            || profile.CpuDuration <= TimeSpan.Zero
            || profile.AllocationBytes <= 0
            || profile.RetainedBytes <= 0
            || profile.RetainedBytes > profile.AllocationBytes
            || profile.AllocationBlockBytes <= 0
            || profile.AllocationBatchBytes <= 0
            || profile.AllocationBatchDelay < TimeSpan.Zero
            || profile.PostGcHoldDuration < TimeSpan.Zero
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile),
                "The Profiling stress profile must contain positive, bounded workload values."
            );
        }
    }

    private sealed record StressSummary(
        ulong Checksum,
        long AllocatedBytes,
        long RetainedBytes
    );
}

/// <summary>Defines the bounded workload used by <see cref="WeatherProfilingStressJob"/>.</summary>
/// <example><code>var profile = new WeatherProfilingStressProfile();</code></example>
public sealed record WeatherProfilingStressProfile
{
    /// <summary>Gets the number of dedicated CPU workers.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { WorkerCount = 2 };</code></example>
    public int WorkerCount { get; init; } = Math.Max(1, Environment.ProcessorCount - 1);

    /// <summary>Gets how long the CPU saturation phase runs.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { CpuDuration = TimeSpan.FromSeconds(8) };</code></example>
    public TimeSpan CpuDuration { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>Gets the total managed bytes allocated during the allocation phase.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { AllocationBytes = 192L * 1024 * 1024 };</code></example>
    public long AllocationBytes { get; init; } = 192L * 1024 * 1024;

    /// <summary>Gets the allocated bytes kept reachable through the post-GC observation phase.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { RetainedBytes = 64L * 1024 * 1024 };</code></example>
    public long RetainedBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>Gets the size of each large managed allocation.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { AllocationBlockBytes = 256 * 1024 };</code></example>
    public int AllocationBlockBytes { get; init; } = 256 * 1024;

    /// <summary>Gets the allocated bytes produced between pacing delays.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { AllocationBatchBytes = 1024 * 1024 };</code></example>
    public int AllocationBatchBytes { get; init; } = 1024 * 1024;

    /// <summary>Gets the delay between allocation batches.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { AllocationBatchDelay = TimeSpan.FromMilliseconds(20) };</code></example>
    public TimeSpan AllocationBatchDelay { get; init; } = TimeSpan.FromMilliseconds(20);

    /// <summary>Gets how long retained objects remain reachable after the forced full GC.</summary>
    /// <example><code>var profile = new WeatherProfilingStressProfile { PostGcHoldDuration = TimeSpan.FromSeconds(5) };</code></example>
    public TimeSpan PostGcHoldDuration { get; init; } = TimeSpan.FromSeconds(5);
}
