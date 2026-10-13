// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Messages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies optional queue-handler segments preserve execution and cancellation.</summary>
/// <example><code>await new WeatherHelloWorldQueueMessageHandlerTests().Handle_OptionalProfiling_PreservesExecutionAndRecordsSegments(2);</code></example>
public sealed class WeatherHelloWorldQueueMessageHandlerTests
{
    /// <summary>Missing and disabled profiling execute normally; enabled profiling adds segments to the existing queue operation.</summary>
    /// <example><code>await tests.Handle_OptionalProfiling_PreservesExecutionAndRecordsSegments(2);</code></example>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Handle_OptionalProfiling_PreservesExecutionAndRecordsSegments(int mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (mode > 0) { services.AddProfiling(options => options.Enabled(mode == 2)).WithOperationProfiling(); }

        using var provider = services.BuildServiceProvider();
        var profiler = provider.GetService<IOperationProfiler>();
        var writer = provider.GetService<OperationProfilingWriterService>();
        if (writer is not null) { await writer.TickAsync(); }

        var sut = new WeatherHelloWorldQueueMessageHandler(NullLogger<WeatherHelloWorldQueueMessageHandler>.Instance, profiler);
        using (var operation = profiler?.BeginOperation(new OperationProfilingStartRequest { Key = "queue:hello", Kind = "QueueHandler" }))
        {
            await sut.Handle(new("Hello", "manual", DateTimeOffset.UtcNow, ["Prepared"]), CancellationToken.None);
            operation?.Complete();
        }

        if (writer is null) { return; }

        await writer.TickAsync();
        var page = await provider.GetRequiredService<IOperationProfilingQueryService>().QueryAsync(new() { FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1), ToUtc = DateTimeOffset.UtcNow.AddMinutes(1) });
        page.IsSuccess.ShouldBeTrue();
        if (mode == 1) { page.Value.Records.ShouldBeEmpty(); return; }

        var record = page.Value.Records.Single();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Segments.Select(segment => segment.Key).Order().ShouldBe(["LogCompletion", "ProcessGreeting"]);
        record.Segments.ShouldAllBe(segment => segment.Statistics.Count == 1 && segment.Outcomes.Single().Outcome == ProfilingSegmentOutcome.Completed);
    }

    /// <summary>Cancellation of the application delay remains visible as a canceled segment and does not log completion.</summary>
    /// <example><code>await tests.Handle_Cancellation_RecordsCanceledWorkAndPreservesException();</code></example>
    [Fact]
    public async Task Handle_Cancellation_RecordsCanceledWorkAndPreservesException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var profiler = provider.GetRequiredService<IOperationProfiler>();
        var sut = new WeatherHelloWorldQueueMessageHandler(NullLogger<WeatherHelloWorldQueueMessageHandler>.Instance, profiler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using (var operation = profiler.BeginOperation(new OperationProfilingStartRequest { Key = "queue:hello", Kind = "QueueHandler" }))
        {
            await Should.ThrowAsync<OperationCanceledException>(() => sut.Handle(new(), cancellation.Token));
            operation.Cancel();
        }

        await writer.TickAsync();
        var page = await provider.GetRequiredService<IOperationProfilingQueryService>().QueryAsync(new() { FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1), ToUtc = DateTimeOffset.UtcNow.AddMinutes(1), Outcomes = [] });
        page.IsSuccess.ShouldBeTrue();
        var record = page.Value.Records.Single();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Canceled);
        record.Segments.Single().Key.ShouldBe("ProcessGreeting");
        record.Segments.Single().Outcomes.Single().Outcome.ShouldBe(ProfilingSegmentOutcome.Canceled);
    }
}
