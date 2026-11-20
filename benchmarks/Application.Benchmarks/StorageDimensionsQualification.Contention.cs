// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Collections.Concurrent;
using System.Diagnostics;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task<int> RunDimensionContentionProfileAsync(IServiceScopeFactory scopes, string engine,
        IReadOnlyList<DimensionQualificationJob> jobs, string output, bool smoke, QualificationCommands commands,
        MixedCommandProfile profile, IContainer container)
    {
        var cohorts = new[]
        {
            (Name: "writers-only", Jobs: jobs.Where(job => job.Writer).ToArray(), ReadRate: 0d),
            (Name: "selective-and-writes", Jobs: jobs.Where(job => job.Writer || job.SelectiveTarget).ToArray(), ReadRate: 200d),
            (Name: "broad-count-and-writes", Jobs: jobs.Where(job => job.Writer || !job.SelectiveTarget).ToArray(), ReadRate: 200d)
        };
        await WriteAsync(output, "contention-definition.json", new
        {
            DiagnosticOnly = true, QualificationComplete = false, Engine = engine,
            MatchedWriteRequestsPerSecond = 100, MatchedReadRequestsPerSecond = 200,
            QueueDeadlineMilliseconds = 250, WarmupSeconds = smoke ? 1 : 3,
            MeasurementSeconds = smoke ? 3 : 15, Repetitions = smoke ? 1 : 5,
            Cohorts = cohorts.Select(cohort => new { cohort.Name, cohort.ReadRate, Jobs = cohort.Jobs.Select(job => job.Name) }),
            Note = "Matched offered rates across engines. Cohort order rotates each repetition. Separate saturated diagnostic samples are instrumented and cannot qualify latency."
        });
        var results = new List<object>();
        var operationChecksPassed = true;
        var deliveryComplete = true;
        for (var run = 0; run < (smoke ? 1 : 5); run++)
        {
            for (var offset = 0; offset < cohorts.Length; offset++)
            {
                var cohort = cohorts[(run + offset) % cohorts.Length];
                var offered = new OrdinaryOfferedLoad(cohort.ReadRate, 100);
                var warmupErrors = new ConcurrentQueue<string>();
                await LoadOfferedAsync(cohort.Jobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 3), warmupErrors, offered, false, allowWritersOnly: true);
                var errors = new ConcurrentQueue<string>();
                var missed = new ConcurrentQueue<object>();
                var cpuBefore = await CaptureContainerCpuAsync(container);
                var workBefore = await CaptureDatabaseWorkAsync(scopes, engine);
                var sqlBefore = engine == "sqlserver" ? await CaptureSqlProfileAsync(scopes) : null;
                var commandBefore = commands.Capture();
                var timer = Stopwatch.StartNew();
                var cases = await LoadOfferedAsync(cohort.Jobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 15), errors, offered, true, missed, allowWritersOnly: true);
                timer.Stop();
                var cpuAfter = await CaptureContainerCpuAsync(container);
                var failed = cases.Any(item => item.Failures != 0) || !warmupErrors.IsEmpty;
                operationChecksPassed &= !failed;
                deliveryComplete &= cases.All(item => item.MissedRequests == 0) && errors.IsEmpty;
                var record = new
                {
                    Run = run + 1, Cohort = cohort.Name, Mode = "matched-offered-load", Offered = offered,
                    DurationIncludingDrainSeconds = timer.Elapsed.TotalSeconds,
                    DatabaseCpuSeconds = (cpuAfter - cpuBefore) / 1_000_000d,
                    ApproximateDatabaseCpuCores = (cpuAfter - cpuBefore) / 1_000_000d / timer.Elapsed.TotalSeconds,
                    CpuNote = "Owned cgroup CPU deltas include small before/after inspection overhead. They are not CPU attribution to individual queries.",
                    Cases = cases, Errors = errors.ToArray(), WarmupErrors = warmupErrors.ToArray(), MissedRequestDetails = missed.ToArray(),
                    CommandWork = commands.Capture().Subtract(commandBefore), DatabaseWorkBefore = workBefore,
                    DatabaseWorkAfter = await CaptureDatabaseWorkAsync(scopes, engine), SqlBefore = sqlBefore,
                    SqlAfter = engine == "sqlserver" ? await CaptureSqlProfileAsync(scopes) : null
                };
                results.Add(record);
                await WriteAsync(output, $"contention-matched-{run + 1}-{cohort.Name}.json", record);
                Console.WriteLine($"Contention {engine} {cohort.Name} repetition {run + 1}: failures={cases.Sum(item => item.Failures)}, misses={cases.Sum(item => item.MissedRequests)}, database CPU={record.ApproximateDatabaseCpuCores:F2} cores.");
                if (failed) return 1;
            }
        }

        // Profile separate saturated intervals to identify stage and scheduling costs, never to replace matched measurements.
        foreach (var cohort in cohorts)
        {
            var observed = cohort.Jobs.Select(job => !job.Writer ? job : job with
            {
                Execute = async worker =>
                {
                    using var operation = profile.Begin(job.Name, worker);
                    return await job.Execute(worker);
                }
            }).ToArray();
            var errors = new ConcurrentQueue<string>();
            var readers = cohort.ReadRate == 0 ? 0 : 16;
            await LoadAsync(observed, engine, TimeSpan.FromSeconds(smoke ? 1 : 3), errors, false, readerCount: readers);
            profile.Samples.Clear();
            var cpuBefore = await CaptureContainerCpuAsync(container);
            var sqlBefore = engine == "sqlserver" ? await CaptureSqlProfileAsync(scopes) : null;
            var samples = new List<object>();
            using var sampling = new CancellationTokenSource();
            var sampler = engine == "sqlserver" ? SampleSqlRequestsAsync(scopes, samples, sampling.Token) : Task.CompletedTask;
            var timer = Stopwatch.StartNew();
            var cases = await LoadAsync(observed, engine, TimeSpan.FromSeconds(smoke ? 3 : 15), errors, true, readerCount: readers);
            timer.Stop();
            sampling.Cancel();
            await sampler;
            var cpuAfter = await CaptureContainerCpuAsync(container);
            operationChecksPassed &= errors.IsEmpty && cases.All(item => item.Failures == 0);
            await WriteAsync(output, $"contention-saturated-{cohort.Name}.json", new
            {
                DiagnosticOnly = true, Cohort = cohort.Name, Mode = "saturated-instrumented", Cases = cases,
                DurationSeconds = timer.Elapsed.TotalSeconds, ApproximateDatabaseCpuCores = (cpuAfter - cpuBefore) / 1_000_000d / timer.Elapsed.TotalSeconds,
                WriterSamples = profile.Samples.ToArray(), WriterSamplesCapped = profile.Samples.Count == 20000,
                SqlBefore = sqlBefore, SqlAfter = engine == "sqlserver" ? await CaptureSqlProfileAsync(scopes) : null,
                SqlSamples = samples, Errors = errors.ToArray(),
                Note = "Instrumentation and closed-loop engine-specific throughput perturb the workload. Use only to identify contributors, not as a matched engine latency comparison."
            });
            Console.WriteLine($"Saturated writer-stage diagnostic {engine} {cohort.Name} complete.");
        }

        await WriteAsync(output, "contention-results.json", new
        {
            Engine = engine, DiagnosticOnly = true, QualificationComplete = false, Smoke = smoke,
            OperationChecksPassed = operationChecksPassed, MatchedDeliveryComplete = deliveryComplete, Results = results,
            Note = "Missed offered requests are retained and invalidate matched latency comparisons; they do not become fast successes."
        });
        return operationChecksPassed ? 0 : 1;
    }

    private static async Task<long> CaptureContainerCpuAsync(IContainer container)
    {
        var result = await container.ExecAsync(["sh", "-c",
            "if [ -r /sys/fs/cgroup/cpu.stat ]; then cat /sys/fs/cgroup/cpu.stat; elif [ -r /sys/fs/cgroup/cpuacct/cpuacct.usage ]; then printf 'usage_nsec '; cat /sys/fs/cgroup/cpuacct/cpuacct.usage; else exit 1; fi"]);
        if (result.ExitCode != 0) throw new InvalidOperationException("Cannot capture owned database CPU usage.");
        var line = result.Stdout.Split('\n').Single(item => item.StartsWith("usage_usec ", StringComparison.Ordinal) || item.StartsWith("usage_nsec ", StringComparison.Ordinal));
        var value = long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], System.Globalization.CultureInfo.InvariantCulture);
        return line.StartsWith("usage_nsec ", StringComparison.Ordinal) ? value / 1000 : value;
    }
}
