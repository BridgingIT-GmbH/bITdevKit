// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs one configurable, bounded workload that produces CPU, allocation, heap, and GC pressure.
/// </summary>
/// <param name="logger">The optional structured logger.</param>
/// <param name="measurements">The optional service that records the complete workload as a segment.</param>
/// <example><code>var stress = new ProfilingStressService(); stress.TryStart(ProfilingStressRequest.Default);</code></example>
public sealed class ProfilingStressService(
    ILogger<ProfilingStressService> logger = null,
    IProfilingMeasurementService measurements = null
)
    : IProfilingStressService
{
    private const string SegmentName = "Profiling stress test";
    private const int AllocationBlockBytes = 64 * 1024;
    private const int LargeObjectBlockBytes = 256 * 1024;
    private const int CpuRoundsPerAllocation = 8;
    private const int RetainedBlockBytes = 1024 * 1024;
    private int running;

    /// <inheritdoc />
    public bool IsRunning => Volatile.Read(ref this.running) != 0;

    /// <inheritdoc />
    public ProfilingStressResult TryStart(
        ProfilingStressRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRequest(request);
        var result = new ProfilingStressResult(
            false,
            request.DurationSeconds,
            request.WorkerCount,
            request.RetainedMemoryBytes
        );
        if (Interlocked.CompareExchange(ref this.running, 1, 0) != 0)
        {
            return result;
        }

        _ = Task.Run(
            () => this.RunGuardedAsync(result with { Started = true }, cancellationToken),
            CancellationToken.None
        );
        return result with { Started = true };
    }

    private async Task RunGuardedAsync(
        ProfilingStressResult workload,
        CancellationToken cancellationToken
    )
    {
        try
        {
            logger?.LogInformation(
                "[ProfilingStress] workload started (durationSeconds={DurationSeconds}, workers={WorkerCount}, retainedMiB={RetainedMiB})",
                workload.DurationSeconds,
                workload.WorkerCount,
                workload.RetainedMemoryBytes / 1_048_576
            );
            var checksum = 0UL;
            Result measurementResult;
            if (measurements is null)
            {
                checksum = await RunWorkloadAsync(workload, cancellationToken)
                    .ConfigureAwait(false);
                measurementResult = Result.Success();
            }
            else
            {
                measurementResult = await measurements
                    .MeasureAsync(
                        SegmentName,
                        async token =>
                        {
                            checksum = await RunWorkloadAsync(workload, token)
                                .ConfigureAwait(false);
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            if (measurementResult.IsFailure)
            {
                logger?.LogWarning(
                    "[ProfilingStress] workload could not be measured (errors={Errors})",
                    string.Join("; ", measurementResult.Errors.Select(error => error.Message))
                );
                return;
            }

            logger?.LogInformation(
                "[ProfilingStress] workload completed (checksum={Checksum})",
                checksum
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger?.LogInformation("[ProfilingStress] workload cancelled");
        }
        catch (Exception exception)
        {
            logger?.LogError(
                exception,
                "[ProfilingStress] workload failed"
            );
        }
        finally
        {
            Volatile.Write(ref this.running, 0);
        }
    }

    private static async Task<ulong> RunWorkloadAsync(
        ProfilingStressResult workload,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var retained = AllocateRetainedMemory(
            workload.RetainedMemoryBytes,
            cancellationToken
        );
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            blocking: true,
            compacting: false
        );

        var duration = TimeSpan.FromSeconds(workload.DurationSeconds);
        var workers = Enumerable
            .Range(0, workload.WorkerCount)
            .Select(worker =>
                Task.Factory.StartNew(
                    () => BurnCpuAndAllocate(worker, duration, cancellationToken),
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                )
            )
            .ToArray();
        var checksums = await Task.WhenAll(workers).ConfigureAwait(false);
        GC.KeepAlive(retained);
        return checksums.Aggregate(0UL, (current, value) => current ^ value);
    }

    private static IReadOnlyList<byte[]> AllocateRetainedMemory(
        long retainedMemoryBytes,
        CancellationToken cancellationToken
    )
    {
        var retained = new List<byte[]>((int)Math.Ceiling(
            (double)retainedMemoryBytes / RetainedBlockBytes
        ));
        long allocated = 0;
        var blockIndex = 0;
        while (allocated < retainedMemoryBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blockSize = (int)Math.Min(RetainedBlockBytes, retainedMemoryBytes - allocated);
            var buffer = GC.AllocateUninitializedArray<byte>(blockSize);
            TouchPages(buffer, blockIndex++);
            retained.Add(buffer);
            allocated += buffer.Length;
        }

        return retained;
    }

    private static ulong BurnCpuAndAllocate(
        int worker,
        TimeSpan duration,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var value = 0x9e3779b97f4a7c15UL ^ (uint)worker;
        var iteration = 0;
        while (stopwatch.Elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buffer = GC.AllocateUninitializedArray<byte>(AllocationBlockBytes);
            for (var round = 0; round < CpuRoundsPerAllocation; round++)
            {
                for (var offset = 0; offset < buffer.Length; offset += 64)
                {
                    value = unchecked((value ^ (value >> 27)) * 0x3c79ac492ba7b653UL);
                    value = unchecked((value ^ (value >> 33)) * 0x1c69b3f74ac4ae35UL);
                    if (round == 0)
                    {
                        buffer[offset] = (byte)value;
                    }
                    else
                    {
                        buffer[offset] ^= (byte)value;
                    }
                }
            }

            var hash = SHA256.HashData(buffer);
            value ^= BitConverter.ToUInt64(hash, 0);
            if ((iteration++ & 7) == 0)
            {
                var largeObject = GC.AllocateUninitializedArray<byte>(LargeObjectBlockBytes);
                TouchPages(largeObject, iteration);
                value ^= largeObject[^1];
            }
        }

        return value;
    }

    private static void TouchPages(byte[] buffer, int seed)
    {
        for (var offset = 0; offset < buffer.Length; offset += 4096)
        {
            buffer[offset] = unchecked((byte)(seed + offset));
        }

        buffer[^1] = unchecked((byte)seed);
    }

    private static void ValidateRequest(ProfilingStressRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            request.DurationSeconds,
            0,
            nameof(ProfilingStressRequest.DurationSeconds)
        );
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            request.WorkerCount,
            0,
            nameof(ProfilingStressRequest.WorkerCount)
        );
        ArgumentOutOfRangeException.ThrowIfNegative(
            request.RetainedMemoryBytes,
            nameof(ProfilingStressRequest.RetainedMemoryBytes)
        );
    }
}
