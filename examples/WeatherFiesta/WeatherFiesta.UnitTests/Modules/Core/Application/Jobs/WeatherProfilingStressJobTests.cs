// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Unit tests for <see cref="WeatherProfilingStressJob"/>.
/// </summary>
public class WeatherProfilingStressJobTests
{
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
                services.AddSingleton<IProfilingMeasurementService>(measurements);
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

    private sealed class RecordingProfilingMeasurementService : IProfilingMeasurementService
    {
        public List<string> Names { get; } = [];

        public Task<Result<IProfilingMeasurementScope>> BeginAsync(
            string name,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public async Task<Result> MeasureAsync(
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
