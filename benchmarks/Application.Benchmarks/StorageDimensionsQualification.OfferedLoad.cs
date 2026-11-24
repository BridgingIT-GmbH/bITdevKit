// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

public static partial class StorageDimensionsQualification
{
    /// <summary>Checks offered-load accounting and work-conserving dispatch without a database.</summary>
    /// <param name="args">The engine budget arguments.</param>
    /// <returns>Zero when the scheduler proof succeeds.</returns>
    /// <example><code>await StorageDimensionsQualification.VerifyOfferedSchedulerAsync(["--engine", "sqlserver"]);</code></example>
    public static async Task<int> VerifyOfferedSchedulerAsync(string[] args)
    {
        var budgeted = StorageDimensionsQualificationBudget.StartBudgetedChild(args);
        if (budgeted.HasValue) return budgeted.Value;
        using var timer = new QualificationTimerResolution();
        try
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await CaptureTimerPrecisionAsync(timer.Requested)));
            await VerifyOfferedLoadAsync();
            Console.WriteLine("Offered scheduler accounting, overload and shared-capacity proofs passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private sealed record OrdinaryOfferedLoad(double ReadsPerSecond, double WritesPerSecond, int QueueDeadlineMilliseconds = 250);

    private static async Task<OrdinaryOfferedLoad> CalibrateOrdinaryLoadAsync(BlobDimensionsQualification blobs, DocumentDimensionsQualification documents,
        string engine, string output, bool smoke, ConcurrentQueue<string> errors)
    {
        await VerifyOfferedLoadAsync();
        var parentRates = new List<(double Reads, double Writes)>();
        var seconds = smoke ? 3 : 120;
        for (var run = 0; run < (smoke ? 1 : 3); run++)
        {
            foreach (var parentOnly in run % 2 == 0 ? new[] { true, false } : new[] { false, true })
            {
                var jobs = new List<DimensionQualificationJob>();
                if (blobs is not null) jobs.AddRange(blobs.BaselineJobs(parentOnly));
                if (documents is not null) jobs.AddRange(documents.BaselineJobs(parentOnly));
                await LoadAsync(jobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 15), errors, false);
                var timerBefore = await CaptureTimerPrecisionAsync(OperatingSystem.IsWindows());
                var timer = Stopwatch.StartNew();
                var cases = await LoadAsync(jobs, engine, TimeSpan.FromSeconds(seconds), errors, true);
                timer.Stop();
                var timerAfter = await CaptureTimerPrecisionAsync(OperatingSystem.IsWindows());
                var reads = cases.Where(item => !jobs.Single(job => job.Name == item.Name).Writer).Sum(item => item.Operations) / timer.Elapsed.TotalSeconds;
                var writes = cases.Where(item => jobs.Single(job => job.Name == item.Name).Writer).Sum(item => item.Operations) / timer.Elapsed.TotalSeconds;
                if (parentOnly) parentRates.Add((reads, writes));
                await WriteAsync(output, $"ordinary-saturation-{run + 1}-{(parentOnly ? "parent" : "dimensions")}.json",
                    new { TimerPrecisionBefore = timerBefore, TimerPrecisionAfter = timerAfter, Run = run + 1, ParentOnly = parentOnly, DurationSeconds = timer.Elapsed.TotalSeconds, ReadsPerSecond = reads, WritesPerSecond = writes, Cases = cases });
            }
        }

        // Freeze headroom from the parent baseline only; Dimensions measurements cannot tune the offered rate.
        var offered = new OrdinaryOfferedLoad(Math.Max(16, Math.Floor(parentRates.Select(rate => rate.Reads).Order().ElementAt(parentRates.Count / 2) * 0.7)),
            Math.Max(engine == "sqlite" ? 1 : 4, Math.Floor(parentRates.Select(rate => rate.Writes).Order().ElementAt(parentRates.Count / 2) * 0.7)));
        await WriteAsync(output, "ordinary-offered-load.json", new { FractionOfParentCapacity = 0.7, ParentCalibration = parentRates.Select(rate => new { rate.Reads, rate.Writes }), OfferedLoad = offered });
        return offered;
    }

    private readonly record struct OfferedRequest(int CaseIndex, long Scheduled);

    private static async Task<IReadOnlyList<DimensionQualificationCase>> LoadOfferedAsync(IReadOnlyList<DimensionQualificationJob> jobs, string engine,
        TimeSpan duration, ConcurrentQueue<string> errors, OrdinaryOfferedLoad offered, bool record, ConcurrentQueue<object> missedRequests = null, bool allowWritersOnly = false)
    {
        if (!jobs.Any(job => job.Writer) || (!jobs.Any(job => !job.Writer) && !(allowWritersOnly && offered.ReadsPerSecond == 0)))
            throw new ArgumentException("Offered-load comparisons require reader and writer jobs.");
        var cases = jobs.Select(job => new DimensionQualificationCase(job.Name, job.ExpectedMatches, job.SelectiveTarget)).ToArray();
        var writers = engine == "sqlite" ? 1 : 4;
        Channel<OfferedRequest> CreateQueue(double rate, int workers) => Channel.CreateBounded<OfferedRequest>(new BoundedChannelOptions(
            Math.Max(workers * 2, (int)Math.Ceiling(rate * offered.QueueDeadlineMilliseconds / 1000) + workers))
        {
            SingleWriter = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false
        });
        var reads = CreateQueue(offered.ReadsPerSecond, 16);
        var writes = CreateQueue(offered.WritesPerSecond, writers);
        var ready = 0;
        long epoch = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ThreadPool.GetMinThreads(out var minimum, out var io);
        ThreadPool.SetMinThreads(Math.Max(minimum, 16 + writers + 4), io);
        async Task StartTogether()
        {
            if (Interlocked.Increment(ref ready) == 16 + writers + 2)
            {
                epoch = Stopwatch.GetTimestamp();
                start.SetResult();
            }

            await start.Task;
        }

        async Task Produce(bool writer, Channel<OfferedRequest> queue)
        {
            var selected = jobs.Select((job, index) => (job, index)).Where(pair => pair.job.Writer == writer).ToArray();
            var rate = writer ? offered.WritesPerSecond : offered.ReadsPerSecond;
            await StartTogether();
            try
            {
                for (long arrival = 0; arrival / rate < duration.TotalSeconds; arrival++)
                {
                    var (_, index) = selected[(int)(arrival % selected.Length)];
                    var scheduled = epoch + (long)(arrival / rate * Stopwatch.Frequency);
                    var remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), scheduled).TotalMilliseconds;
                    while (remaining > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remaining)));
                        remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), scheduled).TotalMilliseconds;
                    }

                    if (!queue.Writer.TryWrite(new(index, scheduled)) && record)
                    {
                        cases[index].RecordMissed();
                        if (missedRequests is not null && missedRequests.Count < 128) missedRequests.Enqueue(new
                        {
                            Name = jobs[index].Name, Worker = -1, Writer = writer, AtUtc = DateTimeOffset.UtcNow,
                            Stage = "arrival-queue-capacity", ScheduledOffsetSeconds = Stopwatch.GetElapsedTime(epoch, scheduled).TotalSeconds
                        });
                    }
                }
            }
            finally
            {
                queue.Writer.TryComplete();
            }
        }

        async Task Consume(int worker, bool writer, Channel<OfferedRequest> queue)
        {
            await StartTogether();
            var previousCompletion = epoch;
            var previousOperationMilliseconds = 0d;
            await foreach (var request in queue.Reader.ReadAllAsync())
            {
                var index = request.CaseIndex;
                var job = jobs[index];
                var began = Stopwatch.GetTimestamp();
                var queued = Stopwatch.GetElapsedTime(request.Scheduled, began).TotalMilliseconds;
                if (queued > offered.QueueDeadlineMilliseconds)
                {
                    if (record)
                    {
                        cases[index].RecordMissed();
                        if (missedRequests is not null && missedRequests.Count < 128) missedRequests.Enqueue(new
                        {
                            Name = job.Name, Worker = worker, Writer = writer, AtUtc = DateTimeOffset.UtcNow, Stage = "shared-queue-deadline",
                            ScheduledOffsetSeconds = Stopwatch.GetElapsedTime(epoch, request.Scheduled).TotalSeconds,
                            BeganOffsetSeconds = Stopwatch.GetElapsedTime(epoch, began).TotalSeconds, QueueMilliseconds = queued,
                            PreviousCompletionOffsetSeconds = Stopwatch.GetElapsedTime(epoch, previousCompletion).TotalSeconds,
                            PreviousOperationMilliseconds = previousOperationMilliseconds
                        });
                    }

                    continue;
                }

                var rows = 0;
                var failed = false;
                try { rows = await job.Execute(worker); }
                catch (Exception exception)
                {
                    failed = true;
                    if (errors.Count < 32) errors.Enqueue($"{job.Name}: {exception.GetType().Name}: {exception.Message}");
                }

                previousCompletion = Stopwatch.GetTimestamp();
                previousOperationMilliseconds = Stopwatch.GetElapsedTime(began, previousCompletion).TotalMilliseconds;
                if (record)
                {
                    cases[index].RecordQueue(queued);
                    cases[index].Record(Stopwatch.GetElapsedTime(request.Scheduled, previousCompletion).TotalMilliseconds, rows, failed);
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 16).Select(worker => Task.Run(() => Consume(worker, false, reads)))
            .Concat(Enumerable.Range(0, writers).Select(worker => Task.Run(() => Consume(worker, true, writes))))
            .Concat([Task.Run(() => Produce(false, reads)), Task.Run(() => Produce(true, writes))]));
        var expected = (long)Math.Ceiling(duration.TotalSeconds * offered.ReadsPerSecond) + (long)Math.Ceiling(duration.TotalSeconds * offered.WritesPerSecond);
        if (record && (cases.Any(item => item.Operations == 0 || item.MissedRequests != 0) || cases.Sum(item => item.OfferedRequests) != expected))
            errors.Enqueue("Fixed offered load was not delivered completely; missing/missed requests invalidate comparison.");
        return cases;
    }

    private static async Task VerifyOfferedLoadAsync()
    {
        DimensionQualificationJob[] jobs = [new("scheduler/read", false, 1, false, _ => Task.FromResult(1)),
            new("scheduler/write", true, 1, false, async _ => { await Task.Delay(50); return 1; })];
        var errors = new ConcurrentQueue<string>();
        var cases = await LoadOfferedAsync(jobs, "sqlite", TimeSpan.FromSeconds(1), errors, new(16, 100, 10), true);
        if (cases.Sum(item => item.OfferedRequests) != 116 || cases.Sum(item => item.MissedRequests) == 0 || errors.IsEmpty
            || cases.Single(item => item.Name == "scheduler/write").P95Milliseconds < 50)
            throw new InvalidOperationException("Offered-load scheduler failed overload/accounting verification.");
        errors.Clear();
        var delivered = await LoadOfferedAsync(jobs, "sqlite", TimeSpan.FromSeconds(1), errors, new(16, 4), true);
        if (!errors.IsEmpty || delivered.Sum(item => item.Operations) != 20 || delivered.Any(item => item.MissedRequests != 0))
            throw new InvalidOperationException("Offered-load scheduler failed complete-delivery verification.");
        errors.Clear();
        DimensionQualificationJob[] mixed = [new("scheduler/read", false, 1, false, _ => Task.FromResult(1)),
            new("scheduler/slow-write", true, 1, false, async _ => { await Task.Delay(50); return 1; }),
            new("scheduler/fast-write", true, 1, false, _ => Task.FromResult(1))];
        var shared = await LoadOfferedAsync(mixed, "sqlserver", TimeSpan.FromSeconds(2), errors, new(16, 100, 5), true);
        if (!errors.IsEmpty || shared.Sum(item => item.Operations) != 232 || shared.Any(item => item.MissedRequests != 0))
            throw new InvalidOperationException("Offered scheduler stranded requests behind busy workers despite available shared capacity.");

    }
}
