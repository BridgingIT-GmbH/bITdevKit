// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using JobWrapper = BridgingIT.DevKit.Application.JobScheduling.JobWrapper;
using ScopedJobWrapper = BridgingIT.DevKit.Application.JobScheduling.ScopedJobWrapper;

/// <summary>Verifies generated and supplied correlation identifiers across application entry points.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CorrelationIdGenerationTests</code></example>
public class CorrelationIdGenerationTests
{
    /// <summary>Checks probes and direct execution contexts use short identifiers.</summary>
    /// <example><code>test.Construct_DefaultCorrelation_UsesShortIdentifiers();</code></example>
    [Fact]
    public void Construct_DefaultCorrelation_UsesShortIdentifiers()
    {
        // Arrange & Act
        var message = new AliveMessage();
        var queueMessage = new AliveQueueMessage();
        using var harness = JobTestHarness.Create().WithJob<EchoJob>("correlation-test").Build();
        var instanceId = Guid.NewGuid();
        var services = Substitute.For<IServiceProvider>();
        var first = new OrchestrationContext<Data>("test", new Data(), services, instanceId);
        var second = new OrchestrationContext<Data>("test", new Data(), services, instanceId);
        var identifiers = new[]
        {
            message.CorrelationId,
            queueMessage.CorrelationId,
            new AliveJobData().CorrelationId,
            new AliveOrchestrationData().CorrelationId,
            new JobExecutionContextBuilder<Unit>().Build().CorrelationId,
            harness.Context.CorrelationId,
            first.CorrelationId,
            second.CorrelationId,
        };

        // Assert
        identifiers.ShouldAllBe(identifier => IsShortIdentifier(identifier));
        identifiers.Distinct(StringComparer.Ordinal).Count().ShouldBe(identifiers.Length);
        CorrelationId.ReadFrom(message.Properties).ShouldBe(message.CorrelationId);
        CorrelationId.ReadFrom(queueMessage.Properties).ShouldBe(queueMessage.CorrelationId);
        first.InstanceId.ShouldBe(instanceId);
        second.InstanceId.ShouldBe(instanceId);
    }

    /// <summary>Checks supplied GUID correlation identifiers remain supported.</summary>
    /// <example><code>test.Construct_SuppliedGuidCorrelation_PreservesIdentifier();</code></example>
    [Fact]
    public void Construct_SuppliedGuidCorrelation_PreservesIdentifier()
    {
        // Arrange
        var identifier = Guid.NewGuid().ToString("N");

        // Act
        var job = new JobExecutionContextBuilder<Unit>().WithCorrelationId(identifier).Build();
        var orchestration = new OrchestrationContext<Data>(
            "test",
            new Data(),
            Substitute.For<IServiceProvider>(),
            correlationId: identifier
        );

        // Assert
        job.CorrelationId.ShouldBe(identifier);
        orchestration.CorrelationId.ShouldBe(identifier);
    }

    /// <summary>Checks both Quartz wrappers generate short identifiers and preserve explicit trigger metadata.</summary>
    /// <example><code>await test.Execute_QuartzWrapper_GeneratesOrPreservesCorrelation(false, null);</code></example>
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "supplier-correlation")]
    [InlineData(true, "bb911967768142bf9b717966977da958")]
    public async Task Execute_QuartzWrapper_GeneratesOrPreservesCorrelation(
        bool scoped,
        string supplied
    )
    {
        // Arrange
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var scope = services.CreateScope();
        var job = new RecordingJob();
        var detail = JobBuilder.Create<RecordingJob>()
            .WithIdentity("correlation-test")
            .UsingJobData("JobId", "correlation-test-id")
            .Build();
        var trigger = TriggerBuilder.Create().ForJob(detail).StartNow().Build();
        if (supplied is not null)
        {
            trigger.JobDataMap["CorrelationId"] = supplied;
        }

        var context = Substitute.For<Quartz.IJobExecutionContext>();
        context.JobDetail.Returns(detail);
        context.Trigger.Returns(trigger);
        context.Scheduler.SchedulerName.Returns("test-scheduler");
        string captured = null;
        context
            .When(value => value.Put("CorrelationId", Arg.Any<object>()))
            .Do(call => captured = call.ArgAt<object>(1) as string);
        var sut = scoped ? new ScopedJobWrapper(scope, job, []) : new JobWrapper(services, job, []);

        // Act
        await sut.Execute(context);

        // Assert
        job.ExecutionCount.ShouldBe(1);
        if (supplied is null)
        {
            IsShortIdentifier(captured).ShouldBeTrue();
        }
        else
        {
            captured.ShouldBe(supplied);
        }
    }

    private static bool IsShortIdentifier(string value) =>
        value is { Length: 12 }
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private sealed class Data : IOrchestrationData { }

    private sealed class RecordingJob : Quartz.IJob
    {
        /// <summary>Gets the number of invocations that reached the wrapped job.</summary>
        /// <example><code>var count = job.ExecutionCount;</code></example>
        public int ExecutionCount { get; private set; }

        /// <inheritdoc />
        public Task Execute(Quartz.IJobExecutionContext context)
        {
            this.ExecutionCount++;
            return Task.CompletedTask;
        }
    }
}
