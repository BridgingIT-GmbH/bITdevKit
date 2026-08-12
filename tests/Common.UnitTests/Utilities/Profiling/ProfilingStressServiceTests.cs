// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Diagnostics;

public sealed class ProfilingStressServiceTests
{
    public static TheoryData<ProfilingStressRequest, string> InvalidRequests =>
        new()
        {
            {
                ProfilingStressRequest.Default with { DurationSeconds = 0 },
                nameof(ProfilingStressRequest.DurationSeconds)
            },
            {
                ProfilingStressRequest.Default with { WorkerCount = 0 },
                nameof(ProfilingStressRequest.WorkerCount)
            },
            {
                ProfilingStressRequest.Default with { RetainedMemoryBytes = -1 },
                nameof(ProfilingStressRequest.RetainedMemoryBytes)
            },
        };

    [Fact]
    public async Task TryStart_WhenRunIsActive_AcceptsOnlyOneBoundedWorkload()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var sut = new ProfilingStressService();
        var request = ProfilingStressRequest.Default with
        {
            DurationSeconds = 5,
            WorkerCount = 2,
            RetainedMemoryBytes = 1024 * 1024,
        };

        // Act
        var first = sut.TryStart(request, cancellation.Token);
        var second = sut.TryStart(request, cancellation.Token);

        // Assert
        first.Started.ShouldBeTrue();
        first.DurationSeconds.ShouldBe(request.DurationSeconds);
        first.WorkerCount.ShouldBe(request.WorkerCount);
        first.RetainedMemoryBytes.ShouldBe(request.RetainedMemoryBytes);
        second.Started.ShouldBeFalse();
        sut.IsRunning.ShouldBeTrue();

        cancellation.Cancel();
        var timeout = Stopwatch.StartNew();
        while (sut.IsRunning && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }

        sut.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task TryStart_WithMeasurementService_MeasuresCompleteWorkloadAsProfilingStressTest()
    {
        // Arrange
        var measurements = new RecordingProfilingMeasurementService();
        var sut = new ProfilingStressService(measurements: measurements);
        var request = ProfilingStressRequest.Default with
        {
            DurationSeconds = 1,
            WorkerCount = 1,
            RetainedMemoryBytes = 0,
        };

        // Act
        var result = sut.TryStart(request);
        var timeout = Stopwatch.StartNew();
        while (sut.IsRunning && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }

        // Assert
        result.Started.ShouldBeTrue();
        sut.IsRunning.ShouldBeFalse();
        measurements.Name.ShouldBe("Profiling stress test");
        measurements.WorkloadStarted.ShouldBeTrue();
        measurements.WorkloadCompleted.ShouldBeTrue();
        measurements.MeasurementCompleted.ShouldBeTrue();
    }

    [Fact]
    public void Default_WhenCreated_UsesDashboardWorkloadShape()
    {
        // Act
        var request = ProfilingStressRequest.Default;

        // Assert
        request.DurationSeconds.ShouldBe(30);
        request.WorkerCount.ShouldBeGreaterThanOrEqualTo(1);
        request.RetainedMemoryBytes.ShouldBeInRange(
            32L * 1024 * 1024,
            128L * 1024 * 1024
        );
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void TryStart_WithInvalidRequest_ThrowsArgumentOutOfRangeException(
        ProfilingStressRequest request,
        string parameterName
    )
    {
        // Arrange
        var sut = new ProfilingStressService();

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => sut.TryStart(request));

        // Assert
        exception.ParamName.ShouldBe(parameterName);
        sut.IsRunning.ShouldBeFalse();
    }

    private sealed class RecordingProfilingMeasurementService : IProfilingMeasurementService
    {
        public string Name { get; private set; }

        public bool WorkloadStarted { get; private set; }

        public bool WorkloadCompleted { get; private set; }

        public bool MeasurementCompleted { get; private set; }

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
            this.Name = name;
            this.WorkloadStarted = true;
            await action(cancellationToken);
            this.WorkloadCompleted = true;
            this.MeasurementCompleted = true;
            return Result.Success();
        }
    }
}
