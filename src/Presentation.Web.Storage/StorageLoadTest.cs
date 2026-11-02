// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Diagnostics;
using System.Security.Cryptography;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

internal sealed record StorageLoadRequest
{
    public string Id { get; init; }

    public string Target { get; init; }

    public string Container { get; init; }

    public int PayloadKiB { get; init; } = 64;

    public int Workers { get; init; } = 1;

    public int Seconds { get; init; } = 10;

    public int ReadPercent { get; init; } = 50;

    internal void Validate()
    {
        if ((this.Id is not null && !Guid.TryParse(this.Id, out _)) || string.IsNullOrWhiteSpace(this.Target)
            || this.PayloadKiB is < 4 or > 4096 || this.Workers is < 1 or > 16 || this.Seconds is < 1 or > 60 || this.ReadPercent is < 0 or > 100)
        {
            throw new StorageLoadValidationException("Choose a target, 4–4096 KiB, 1–16 workers, 1–60 seconds, and 0–100% reads.");
        }
    }
}

internal sealed class StorageLoadTest(string feature, IServiceScopeFactory scopeFactory) : IDisposable
{
    private readonly SemaphoreSlim admission = new(1, 1);
    private readonly Lock gate = new();
    private readonly MetricsService fallbackMetrics = new();
    private Run current;

    internal object Snapshot()
    {
        lock (this.gate)
        {
            if (this.current is not { } run)
            {
                return new
                {
                    phase = "Idle"
                };
            }

            var seconds = run.Started == 0 ? 0 : run.ElapsedSeconds ?? Stopwatch.GetElapsedTime(run.Started).TotalSeconds;

            return new
            {
                run.Id,
                run.Phase,
                run.Request.Target,
                run.Request.Container,
                readBytes = Interlocked.Read(ref run.ReadBytes),
                writeBytes = Interlocked.Read(ref run.WriteBytes),
                readItems = Interlocked.Read(ref run.ReadItems),
                writeItems = Interlocked.Read(ref run.WriteItems),
                readMiBPerSecond = seconds > 0 ? Interlocked.Read(ref run.ReadBytes) / seconds / 1048576 : 0,
                writeMiBPerSecond = seconds > 0 ? Interlocked.Read(ref run.WriteBytes) / seconds / 1048576 : 0,
                elapsedSeconds = seconds,
                errors = Interlocked.Read(ref run.Errors),
                run.Error,
                cleanupPending = run.Pending.Count,
                remainingKeys = run.Pending.Select(index => feature switch
                {
                    "documents" => $"bdk.storage.diagnostics.v1 / bdk-diag-{run.Id} / {index}",
                    "blobs" => $"{run.Request.Container}/bdk-diag-{run.Id}-{index}.bin",
                    _ => $"bdk-diag-{run.Id}-{index}.bin"
                }).ToArray()
            };
        }
    }

    internal void Stop(string id = null)
    {
        lock (this.gate)
        {
            if (this.current is { Phase: "Preparing" or "Running" } run && (id is null || run.Id == id || run.StopToken == id))
            {
                run.StopRequested = true;
                run.Cancellation.Cancel();
            }
        }
    }

    internal async Task<object> ExecuteAsync(StorageLoadRequest request, CancellationToken cancellationToken)
    {
        request.Validate();

        if (!await this.admission.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A load test or cleanup is already active on this node.");
        }

        var workers = new List<Worker>();
        Run run = null;

        try
        {
            lock (this.gate)
            {
                if (this.current?.Pending.Count > 0)
                {
                    throw new InvalidOperationException("Retry cleanup of the previous run before starting another load test.");
                }

                run = new(request, cancellationToken);
                this.current = run;
            }

            for (var index = 0; index < request.Workers; index++)
            {
                var scope = scopeFactory.CreateAsyncScope();

                try
                {
                    workers.Add(new(index, scope, new(scope.ServiceProvider, feature, request, run.Id, index), scope.ServiceProvider.GetService<IMetricsService>() ?? this.fallbackMetrics));
                }
                catch
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }

            using var preparation = CancellationTokenSource.CreateLinkedTokenSource(run.Cancellation.Token);
            preparation.CancelAfter(TimeSpan.FromSeconds(30));
            await Task.WhenAll(workers.Select(worker => worker.Target.SeedAsync(run.Payload, preparation.Token))).ConfigureAwait(false);
            lock (this.gate)
            {
                run.Phase = "Running";
                run.Started = Stopwatch.GetTimestamp();
            }

            await Task.WhenAll(workers.Select(worker => this.LoadAsync(run, worker, run.Cancellation.Token))).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (run?.Cancellation.IsCancellationRequested == true)
        {
            run.StopRequested = true;
        }
        catch (InvalidOperationException) when (run is null)
        {
            throw;
        }
        catch (StorageLoadValidationException exception)
        {
            if (run is not null)
            {
                this.Fail(run, exception);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (run is not null)
            {
                this.Fail(run, exception);
            }
        }
        finally
        {
            if (run is not null)
            {
                lock (this.gate)
                {
                    run.ElapsedSeconds = run.Started == 0 ? 0 : Stopwatch.GetElapsedTime(run.Started).TotalSeconds;
                    run.Phase = "Cleaning";
                }

                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await Task.WhenAll(workers.Select(worker => this.CleanAsync(run, worker.Target, cleanup.Token))).ConfigureAwait(false);

                foreach (var worker in workers)
                {
                    try
                    {
                        await worker.Scope.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        this.Fail(run, exception);
                    }
                }

                lock (this.gate)
                {
                    run.Pending = workers.Where(worker => worker.Target.PendingCleanup).Select(worker => worker.Index).ToList();
                    run.StopRequested |= cancellationToken.IsCancellationRequested;
                    run.Phase = run.Pending.Count > 0 ? "Cleanup required" : run.Errors > 0 ? "Failed" : run.StopRequested ? "Stopped" : "Completed";
                    run.Cancellation.Dispose();
                }
            }

            this.admission.Release();
        }

        return this.Snapshot();
    }

    internal async Task<object> RetryCleanupAsync()
    {
        if (!await this.admission.WaitAsync(0).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Wait for the active test to finish cleaning up.");
        }

        try
        {
            Run run;
            int[] pending;
            lock (this.gate)
            {
                run = this.current;
                pending = run?.Pending.ToArray() ?? [];

                if (run is not null)
                {
                    run.Phase = "Cleaning";
                }
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            foreach (var index in pending)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var target = new StorageLoadTarget(scope.ServiceProvider, feature, run.Request, run.Id, index, cleanupOnly: true);
                    await this.CleanAsync(run, target, timeout.Token).ConfigureAwait(false);

                    if (!target.PendingCleanup)
                    {
                        lock (this.gate)
                        {
                            run.Pending.Remove(index);
                        }
                    }
                }
                catch (Exception exception)
                {
                    this.Fail(run, exception);
                }
            }

            lock (this.gate)
            {
                if (run is not null)
                {
                    run.Phase = run.Pending.Count == 0 ? "Cleaned" : "Cleanup required";

                    if (run.Pending.Count == 0)
                    {
                        run.Error = null;
                    }
                }
            }

            return this.Snapshot();
        }
        finally
        {
            this.admission.Release();
        }
    }

    private async Task LoadAsync(Run run, Worker worker, CancellationToken cancellationToken)
    {
        var iteration = worker.Index * 17;
        MetricTag[] readTags = [new("feature", feature), new("operation", "read")];
        MetricTag[] writeTags = [new("feature", feature), new("operation", "write")];

        try
        {
            while (!cancellationToken.IsCancellationRequested && Stopwatch.GetElapsedTime(run.Started) < TimeSpan.FromSeconds(run.Request.Seconds))
            {
                var read = (iteration++ * 37 % 100) < run.Request.ReadPercent;
                var started = Stopwatch.GetTimestamp();
                var bytes = read ? await worker.Target.ReadAsync(cancellationToken).ConfigureAwait(false) : await worker.Target.WriteAsync(run.Payload, createOnly: false, cancellationToken).ConfigureAwait(false);

                if (bytes != run.Payload.Length)
                {
                    throw new StorageLoadException("An incomplete payload was returned; its bytes were not counted.");
                }

                var tags = read ? readTags : writeTags;
                worker.Metrics.AddCounter("storage_load_bytes", bytes, tags);
                worker.Metrics.AddCounter("storage_load_items", 1, tags);
                var elapsed = Stopwatch.GetElapsedTime(started);
                worker.Metrics.RecordHistogram("storage_load_duration", elapsed.TotalMilliseconds, "ms", tags);
                worker.Metrics.RecordHistogram("storage_load_bytes_per_second", bytes / Math.Max(elapsed.TotalSeconds, 0.000000001), "bytes/s", tags);

                if (read)
                {
                    Interlocked.Add(ref run.ReadBytes, bytes);
                    Interlocked.Increment(ref run.ReadItems);
                }
                else
                {
                    Interlocked.Add(ref run.WriteBytes, bytes);
                    Interlocked.Increment(ref run.WriteItems);
                }

                // In-memory/custom providers may complete synchronously; let stop requests and other workers run.
                if ((iteration & 63) == 0)
                {
                    await Task.Yield();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            worker.Metrics.AddCounter("storage_load_errors", 1, [new("feature", feature)]);
            this.Fail(run, exception);
        }
    }

    private async Task CleanAsync(Run run, StorageLoadTarget target, CancellationToken cancellationToken)
    {
        try
        {
            await target.CleanupAsync(run.Payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            this.fallbackMetrics.AddCounter("storage_load_cleanup_failures", 1, [new("feature", feature)]);
            this.Fail(run, exception);
        }
    }

    private void Fail(Run run, Exception exception)
    {
        lock (this.gate)
        {
            run.Errors++;
            run.Error = exception is StorageLoadException or StorageLoadValidationException ? exception.Message : "Storage test failed: " + exception.GetType().Name;

            if (run.Phase is "Preparing" or "Running")
            {
                run.Cancellation.Cancel();
            }
        }
    }

    public void Dispose()
    {
        this.Stop();
        this.fallbackMetrics.Dispose();
    }

    private sealed class Run(StorageLoadRequest request, CancellationToken cancellationToken)
    {
        internal readonly string Id = Guid.NewGuid().ToString("N");
        internal readonly string StopToken = request.Id is null ? null : Guid.Parse(request.Id).ToString("N");
        internal readonly byte[] Payload = RandomNumberGenerator.GetBytes(request.PayloadKiB * 1024);
        internal readonly StorageLoadRequest Request = request;
        internal readonly CancellationTokenSource Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        internal string Phase = "Preparing";
        internal string Error;
        internal long Started;
        internal double? ElapsedSeconds;
        internal long ReadBytes;
        internal long WriteBytes;
        internal long ReadItems;
        internal long WriteItems;
        internal long Errors;
        internal bool StopRequested;
        internal List<int> Pending = [];
    }

    private sealed record Worker(int Index, AsyncServiceScope Scope, StorageLoadTarget Target, IMetricsService Metrics);
}

internal sealed class StorageLoadException(string message) : Exception(message);

internal sealed class StorageLoadValidationException(string message) : ArgumentException(message);
