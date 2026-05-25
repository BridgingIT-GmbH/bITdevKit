// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

public class InMemoryOrchestrationExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenStateProgressesThroughTransition_CompletesInTargetState()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<ProgressionOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<ProgressionOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Completed);
        result.CurrentState.ShouldBe("Done");
        result.Data.Trace.ShouldBe(["start", "done"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClassActivityIsConfigured_UsesDependencyInjectionAndMutatesContext()
    {
        // Arrange
        var traceCollector = new TraceCollector();
        var serviceProvider = CreateServices(services =>
        {
            services.AddSingleton(traceCollector);
            services.AddOrchestrations().WithOrchestration<ClassActivityOrchestration>();
        });
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<ClassActivityOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Completed);
        result.Data.Counter.ShouldBe(1);
        traceCollector.Entries.ShouldBe(["class-activity"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInlineActivityMutatesContext_PreservesUpdatedData()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<ContextMutationOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<ContextMutationOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Completed);
        result.Data.Counter.ShouldBe(5);
        result.Data.Flags.ShouldBe(["mutated"]);
        result.Properties["Message"].ShouldBe("updated");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryOutcomeIsReturned_ReexecutesCurrentActivityUntilItContinues()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<RetryOutcomeOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<RetryOutcomeOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Completed);
        result.Data.Counter.ShouldBe(2);
        result.CurrentState.ShouldBe("Done");
    }

    [Fact]
    public async Task ExecuteAsync_WhenWaitOutcomeIsReturned_EndsInWaitingState()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<WaitOutcomeOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<WaitOutcomeOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Waiting);
        result.CurrentState.ShouldBe("AwaitingInput");
        result.FailureReason.ShouldBe("Awaiting manual input.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelOutcomeIsReturned_EndsInCancelledState()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<CancelOutcomeOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<CancelOutcomeOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Cancelled);
        result.FailureReason.ShouldBe("Cancelled by orchestration.");
        result.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminateOutcomeIsReturned_EndsInTerminatedState()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<TerminateOutcomeOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<TerminateOutcomeOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Terminated);
        result.FailureReason.ShouldBe("Rejected by orchestration.");
        result.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoTransitionMatches_FailsExecution()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<NoTransitionOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<NoTransitionOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Failed);
        result.FailureReason.ShouldContain("no matching transition");
        result.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDefinitionContainsUnknownTransitionTarget_ReturnsFailedValidationMessage()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<InvalidTransitionTargetOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<InvalidTransitionTargetOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Failed);
        result.FailureReason.ShouldContain("targets unknown state");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDefinitionHasNoStates_ReturnsFailedValidationMessage()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<NoStatesOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationExecutor>();

        // Act
        var result = await sut.ExecuteAsync<NoStatesOrchestration, TestOrchestrationData>(new TestOrchestrationData());

        // Assert
        result.Status.ShouldBe(OrchestrationStatus.Failed);
        result.FailureReason.ShouldContain("must define at least one state");
    }

    private static ServiceProvider CreateServices(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);

        return services.BuildServiceProvider();
    }

    private sealed class TestOrchestrationData : IOrchestrationData
    {
        public int Counter { get; set; }

        public List<string> Trace { get; } = [];

        public List<string> Flags { get; } = [];
    }

    private sealed class TraceCollector
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class AppendTraceActivity(TraceCollector collector) : IOrchestrationActivity<TestOrchestrationData>
    {
        public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<TestOrchestrationData> context, CancellationToken cancellationToken = default)
        {
            collector.Entries.Add("class-activity");
            context.Data.Counter++;

            return Task.FromResult(OrchestrationOutcome.Continue());
        }
    }

    private sealed class ProgressionOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("start");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("Done"))
                .State("Done", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("done");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .Complete());
        }
    }

    private sealed class ClassActivityOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity<AppendTraceActivity>()
                    .Complete());
        }
    }

    private sealed class ContextMutationOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Counter = 5;
                        context.Data.Flags.Add("mutated");
                        context.Properties["Message"] = "updated";

                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .Complete());
        }
    }

    private sealed class RetryOutcomeOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Counter++;

                        return Task.FromResult(context.Data.Counter == 1
                            ? OrchestrationOutcome.Retry("retry once")
                            : OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class WaitOutcomeOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("AwaitingInput", state => state
                    .Activity((context, cancellationToken) =>
                        Task.FromResult(OrchestrationOutcome.Wait("Awaiting manual input."))));
        }
    }

    private sealed class CancelOutcomeOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                        Task.FromResult(OrchestrationOutcome.Cancel("Cancelled by orchestration."))));
        }
    }

    private sealed class TerminateOutcomeOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                        Task.FromResult(OrchestrationOutcome.Terminate("Rejected by orchestration."))));
        }
    }

    private sealed class NoTransitionOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()))
                    .TransitionTo("Done", context => context.Data.Counter > 0))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class InvalidTransitionTargetOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
            builder
                .State("Start", state => state.TransitionTo("Missing"));
        }
    }

    private sealed class NoStatesOrchestration : Orchestration<TestOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<TestOrchestrationData> builder)
        {
        }
    }
}