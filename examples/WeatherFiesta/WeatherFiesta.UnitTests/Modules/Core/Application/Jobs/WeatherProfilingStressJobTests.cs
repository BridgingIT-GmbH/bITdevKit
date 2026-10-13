// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Unit tests for <see cref="WeatherProfilingStressJob"/>.
/// </summary>
public class WeatherProfilingStressJobTests
{
    /// <summary>Omitted Operation Profiling keeps explicit Runtime measurement and the original workload result.</summary>
    [Fact]
    public async Task DispatchAndWaitAsync_WithSmallProfile_CompletesMeasuredWorkload()
    {
        // Arrange
        var measurements = new RecordingProfilingMeasurementService();
        var profile = new WeatherProfilingStressProfile
        {
            WorkerCount = 1,
            CpuDuration = TimeSpan.FromMilliseconds(10),
            AllocationBytes = 1024 * 1024,
            RetainedBytes = 256 * 1024,
            AllocationBlockBytes = 64 * 1024,
            AllocationBatchBytes = 128 * 1024,
            AllocationBatchDelay = TimeSpan.Zero,
            PostGcHoldDuration = TimeSpan.Zero,
        };
        using var harness = JobSchedulerTestHarness.Create()
            .WithJob<WeatherProfilingStressJob>("core_profiling_stress", job => job
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IRuntimeProfilingMeasurementService>(measurements);
                services.AddSingleton(profile);
            })
            .Build();

        // Act
        var result = await harness.DispatchAndWaitAsync<WeatherProfilingStressJob>();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        measurements.Names.ShouldBe(["WeatherFiesta profiling stress"]);
        result.Value.Messages.ShouldContain(message =>
            message.Contains("profiling stress completed")
            && message.Contains("Workers=1")
            && message.Contains("Allocated=1MiB"));
    }

    /// <summary>Optional operation injection supports omitted, disabled, and enabled registrations without changing execution.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExecuteAsync_OptionalOperations_PreservesResultAndCapturesThreeSegments(int mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (mode > 0) { services.AddProfiling(options => options.Enabled(mode == 2)).WithOperationProfiling(); }

        using var provider = services.BuildServiceProvider();
        var profiler = provider.GetService<IOperationProfiler>();
        var writer = provider.GetService<OperationProfilingWriterService>();
        if (writer is not null) { await writer.TickAsync(); }

        var measurements = new RecordingProfilingMeasurementService();
        var sut = new WeatherProfilingStressJob(NullLogger<WeatherProfilingStressJob>.Instance, measurements, SmallProfile(), profiler);
        var context = Substitute.For<IJobExecutionContext<Unit>>();
        context.Messages.Returns(new List<string>());

        var result = await sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        measurements.Names.Count.ShouldBe(1);
        context.Messages.Count.ShouldBe(1);
        if (writer is not null)
        {
            await writer.TickAsync();
            var records = (await provider.GetRequiredService<IOperationProfilingQueryService>().QueryAsync(new() { ToUtc = DateTimeOffset.UtcNow.AddMinutes(1), FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1) })).Value.Records;
            var record = records.Single();
            record.Key.ShouldBe("weather:profiling-stress");
            record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
            record.Segments.Select(segment => segment.Path.Components.Single()).Order().ShouldBe(["Allocate", "Cpu", "Retain"]);
            record.Segments.ShouldAllBe(segment => segment.Statistics.Count == 1);
        }
    }

    /// <summary>An existing owner receives nested workload segments and keeps its own root key.</summary>
    [Fact]
    public async Task ExecuteAsync_WithOwner_JoinsWithoutCreatingAnotherRoot()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var profiler = provider.GetRequiredService<IOperationProfiler>();
        var sut = new WeatherProfilingStressJob(NullLogger<WeatherProfilingStressJob>.Instance, new RecordingProfilingMeasurementService(), SmallProfile(), profiler);
        var context = Substitute.For<IJobExecutionContext<Unit>>();
        context.Messages.Returns(new List<string>());
        using (var owner = profiler.BeginOperation("interaction:refresh", OperationProfilingKind.Service))
        {
            (await sut.ExecuteAsync(context)).IsSuccess.ShouldBeTrue();
            owner.Complete();
        }

        await writer.TickAsync();
        var record = (await provider.GetRequiredService<IOperationProfilingQueryService>().QueryAsync(new() { ToUtc = DateTimeOffset.UtcNow.AddMinutes(1), FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1) })).Value.Records.Single();
        record.Key.ShouldBe("interaction:refresh");
        record.Segments.ShouldContain(segment => segment.Path.Components.SequenceEqual(new[] { "weather:profiling-stress", "Cpu" }));
    }

    /// <summary>Application cancellation remains cancellation and closes the owned recording cleanly.</summary>
    [Fact]
    public async Task ExecuteAsync_Cancelled_PreservesCancellationAndRecordsOutcome()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var sut = new WeatherProfilingStressJob(NullLogger<WeatherProfilingStressJob>.Instance, new RecordingProfilingMeasurementService(), SmallProfile(), provider.GetRequiredService<IOperationProfiler>());
        var context = Substitute.For<IJobExecutionContext<Unit>>();
        context.Messages.Returns(new List<string>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.ExecuteAsync(context, cancellation.Token));
        await writer.TickAsync();
        var record = (await provider.GetRequiredService<IOperationProfilingQueryService>().QueryAsync(new() { Outcomes = [], ToUtc = DateTimeOffset.UtcNow.AddMinutes(1), FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1) })).Value.Records.Single();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Canceled);
        record.Segments.Single().Outcomes.Single().Outcome.ShouldBe(ProfilingSegmentOutcome.Canceled);
        context.Messages.ShouldBeEmpty();
    }

    private static WeatherProfilingStressProfile SmallProfile() => new()
    {
        WorkerCount = 1, CpuDuration = TimeSpan.FromMilliseconds(10), AllocationBytes = 1024 * 1024,
        RetainedBytes = 256 * 1024, AllocationBlockBytes = 64 * 1024, AllocationBatchBytes = 128 * 1024,
        AllocationBatchDelay = TimeSpan.Zero, PostGcHoldDuration = TimeSpan.Zero,
    };

    private sealed class RecordingProfilingMeasurementService : IRuntimeProfilingMeasurementService
    {
        /// <summary>Gets the exactly-once Runtime measurement observations.</summary>
        public List<string> Names { get; } = [];

        /// <summary>Rejects unused scope creation in this workload fixture.</summary>
        public Task<IResult<IRuntimeProfilingMeasurementScope>> BeginAsync(
            string name,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        /// <summary>Executes the workload once without changing its exception or cancellation.</summary>
        public async Task<IResult> MeasureAsync(
            string name,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default
        )
        {
            this.Names.Add(name);
            await action(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
    }
}
