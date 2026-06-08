// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

public class JobSchedulerServiceDispatchTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task DispatchAsync_ByJobType_Succeeds()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAsync<SuccessfulJob>();

        result.IsSuccess.ShouldBeTrue();
        result.Value.JobName.ShouldBe("success");
    }

    [Fact]
    public async Task DispatchAsync_ByJobName_Succeeds()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAsync("success");

        result.IsSuccess.ShouldBeTrue();
        result.Value.TriggerName.ShouldBe("manual");
    }

    [Fact]
    public async Task DispatchAsync_ByJobName_PersistsOccurrenceWithoutExecuting()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAsync("success");

        result.IsSuccess.ShouldBeTrue();
        var occurrence = await store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var executions = await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId);
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Pending);
        executions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_TargetedToAnotherInstance_AcceptsOccurrenceWithoutLocalExecution()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithBackgroundExecution(options => options.SchedulerInstanceId = "node-a")
                .WithJob<SuccessfulJob>("targeted", job => job
                    .Description("Targets a different scheduler instance.")
                    .AddTrigger("manual", trigger => trigger
                        .Manual()
                        .TargetInstances("node-b")));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAsync("targeted");

        result.IsSuccess.ShouldBeTrue();
        var executions = await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId);
        executions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_UnknownJob_Fails()
    {
        var provider = CreateProvider(services => services.AddJobScheduler());
        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAsync("missing-job");

        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DispatchAsync_DisabledJobOrTrigger_Fails(bool disableJob, bool disableTrigger)
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job =>
                {
                    job.Description("Succeeds.");
                    if (disableJob)
                    {
                        job.Enabled(false);
                    }

                    job.AddTrigger("manual", trigger =>
                    {
                        trigger.Manual();
                        if (disableTrigger)
                        {
                            trigger.Enabled(false);
                        }
                    });
                });
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAsync("success");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAsync_MissingManualTrigger_Fails()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("scheduled", trigger => trigger.At(DateTimeOffset.UtcNow.AddHours(1))));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAsync("success");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAsync_AmbiguousManualTrigger_Fails()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("manual-a", trigger => trigger.Manual())
                    .AddTrigger("manual-b", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAsync("success");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAndWaitAsync_TypedData_IsAvailableInTypedContext()
    {
        TypedDataJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<TypedDataJob>("typed", job => job
                    .Description("Uses typed data.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAndWaitAsync<TypedDataJob>(new TypedPayload("customer-42"));

        result.IsSuccess.ShouldBeTrue();
        TypedDataJob.LastCustomerId.ShouldBe("customer-42");
    }

    [Fact]
    public async Task DispatchAndWaitAsync_InlineDelegateJob_UsesTypedContextAndNormalPipeline()
    {
        InlineDelegateRecorder.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddScoped<InlineDependency>();
            services.AddJobScheduler()
                .WithJob("inline-export", (Action<InlineJobDefinitionBuilder>)(job => job
                    .WithDescription("Uses an inline delegate job.")
                    .Execute<TypedPayload>((Func<IJobExecutionContext<TypedPayload>, IServiceProvider, CancellationToken, Task<Result>>)((context, serviceProvider, cancellationToken) =>
                    {
                        InlineDelegateRecorder.LastCustomerId = context.Data.CustomerId;
                        InlineDelegateRecorder.LastTenant = context.Properties.Get<string>("tenant", string.Empty);
                        InlineDelegateRecorder.LastDependencyValue = serviceProvider.GetRequiredService<InlineDependency>().Value;
                        context.Messages.Add("Inline delegate completed.");
                        context.Properties["processed"] = true;
                        InlineDelegateRecorder.LastProcessed = (bool)context.Properties["processed"];
                        return Task.FromResult(Result.Success());
                    }))
                    .AddTrigger("manual", trigger => trigger.Manual())));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync(
            "inline-export",
            new TypedPayload("customer-007"),
            new JobDispatchOptions
            {
                Properties = new PropertyBag { ["tenant"] = "alpha" },
            });

        var occurrence = await store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var history = await store.ExecutionHistory.ListAsync(result.Value.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.ShouldContain("Inline delegate completed.");
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Completed);
        occurrence.Properties["tenant"].ShouldBe("alpha");
        history.Count.ShouldBeGreaterThanOrEqualTo(4);
        InlineDelegateRecorder.LastCustomerId.ShouldBe("customer-007");
        InlineDelegateRecorder.LastTenant.ShouldBe("alpha");
        InlineDelegateRecorder.LastDependencyValue.ShouldBe("scoped-dependency");
        InlineDelegateRecorder.LastProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAndWaitAsync_InlineDelegateFailureResult_RecordsFailedAttempt()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob("inline-failed", job => job
                    .WithDescription("Fails through an inline delegate result.")
                    .Execute((context, cancellationToken) =>
                    {
                        context.Messages.Add("Inline delegate returned failure.");
                        return Task.FromResult(Result.Failure().WithError(new ValidationError("Inline failure.")));
                    })
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync("inline-failed");
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        result.Value.Messages.ShouldContain("Inline delegate returned failure.");
        execution.Status.ShouldBe(JobExecutionStatus.Failed);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_InlineDelegateThrownException_RecordsFailedAttempt()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob("inline-throwing", job => job
                    .WithDescription("Throws through an inline delegate.")
                    .Execute((context, cancellationToken) => throw new InvalidOperationException("Inline boom"))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync("inline-throwing");
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Status.ShouldBe(JobExecutionStatus.Failed);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_InvalidData_FailsWithResultFailure()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<TypedDataJob>("typed", job => job
                    .Description("Uses typed data.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAndWaitAsync<TypedDataJob>("wrong-shape");

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAndWaitAsync_JobSuccess_RecordsExecutionAndHistory()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync<SuccessfulJob>();
        var occurrence = await store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var executions = await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId);
        var history = await store.ExecutionHistory.ListAsync(result.Value.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Completed);
        executions.Count.ShouldBe(1);
        history.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_FailedResult_RecordsFailedAttempt()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<FailedResultJob>("failed", job => job
                    .Description("Fails with result.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync<FailedResultJob>();
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Status.ShouldBe(JobExecutionStatus.Failed);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_ThrownException_RecordsFailedAttempt()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<ThrowingJob>("throwing", job => job
                    .Description("Throws.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();

        var result = await sut.DispatchAndWaitAsync<ThrowingJob>();
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Status.ShouldBe(JobExecutionStatus.Failed);
    }

    [Fact]
    public async Task DispatchAsync_ThrownExceptionWithRetry_SchedulesRetry()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<ThrowingJob>("throwing", job => job
                    .Description("Throws.")
                    .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(5)))
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        var runtime = provider.GetRequiredService<JobSchedulerService>();
        var store = provider.GetRequiredService<IJobStoreProvider>();
        var fakeTime = (FakeTimeProvider)provider.GetRequiredService<TimeProvider>();

        var result = await sut.DispatchAsync<ThrowingJob>();
        await runtime.ExecuteStoredOccurrenceAsync(result.Value.OccurrenceId);
        var occurrence = await store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        occurrence.Status.ShouldBe(JobOccurrenceStatus.RetryScheduled);
        occurrence.DueUtc.ShouldBe(fakeTime.GetUtcNow().AddMinutes(5));
        execution.Status.ShouldBe(JobExecutionStatus.Retried);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_ThrownException_InvokesRegisteredExceptionHandler()
    {
        var handler = new RecordingSchedulerExceptionHandler();
        var provider = CreateProvider(services =>
        {
            services.AddSingleton(handler);
            services.AddSingleton<IJobSchedulerExceptionHandler>(sp => sp.GetRequiredService<RecordingSchedulerExceptionHandler>());
            services.AddJobScheduler()
                .WithJob<ThrowingJob>("throwing", job => job
                    .Description("Throws.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAndWaitAsync<ThrowingJob>();
        var context = await handler.Invocation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        context.Source.ShouldBe(JobSchedulerExceptionSource.Execution);
        context.Definition.ShouldNotBeNull();
        context.Definition.JobName.ShouldBe("throwing");
        context.Trigger.ShouldNotBeNull();
        context.Trigger.TriggerName.ShouldBe("manual");
        context.OccurrenceId.ShouldNotBeNull();
        context.ExecutionId.ShouldNotBeNull();
        context.Exception.ShouldBeOfType<InvalidOperationException>();
        context.Exception.Message.ShouldBe("Boom");
    }

    [Fact]
    public async Task DispatchAndWaitAsync_SingletonLifetime_ReusesSameJobInstance()
    {
        LifetimeTrackingJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<LifetimeTrackingJob>("singleton-lifetime", job => job
                    .Description("Tracks singleton lifetime.")
                    .UseLifetime(ServiceLifetime.Singleton)
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        await sut.DispatchAndWaitAsync<LifetimeTrackingJob>();
        await sut.DispatchAndWaitAsync<LifetimeTrackingJob>();

        LifetimeTrackingJob.ConstructionCount.ShouldBe(1);
        LifetimeTrackingJob.ExecutionInstanceIds.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_ScopedLifetime_CreatesOneJobInstancePerExecution()
    {
        LifetimeTrackingJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<LifetimeTrackingJob>("scoped-lifetime", job => job
                    .Description("Tracks scoped lifetime.")
                    .UseLifetime(ServiceLifetime.Scoped)
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        await sut.DispatchAndWaitAsync<LifetimeTrackingJob>();
        await sut.DispatchAndWaitAsync<LifetimeTrackingJob>();

        LifetimeTrackingJob.ConstructionCount.ShouldBe(2);
        LifetimeTrackingJob.ExecutionInstanceIds.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_IneligibleTargetInstance_FailsClearly()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .InstanceId("node-a")
                .WithJob<SuccessfulJob>("targeted", job => job
                    .Description("Runs only on another scheduler instance.")
                    .AddTrigger("manual", trigger => trigger
                        .Manual()
                        .TargetInstances("node-b")));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAndWaitAsync("targeted");

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("targets scheduler instance(s) 'node-b'");
        result.Errors.Single().Message.ShouldContain("scheduler instance 'node-a'");
    }

    [Fact]
    public async Task DispatchAndWaitAsync_ReturnsCompletedExecutionResult()
    {
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<SuccessfulJob>("success", job => job
                    .Description("Succeeds.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();

        var result = await sut.DispatchAndWaitAsync<SuccessfulJob>();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ExecutionId.ShouldNotBe(Guid.Empty);
        result.Value.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task DispatchAndWaitAsync_CancellationToken_ReachesJob()
    {
        CancellationAwareJob.Reset();
        var provider = CreateProvider(services =>
        {
            services.AddJobScheduler()
                .WithJob<CancellationAwareJob>("cancel-aware", job => job
                    .Description("Observes cancellation.")
                    .AddTrigger("manual", trigger => trigger.Manual()));
        });

        var sut = provider.GetRequiredService<IJobSchedulerService>();
        using var cts = new CancellationTokenSource();

        var task = sut.DispatchAndWaitAsync<CancellationAwareJob>(cancellationToken: cts.Token);
        await CancellationAwareJob.Started.Task;
        cts.Cancel();
        var result = await task;

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Cancelled);
        CancellationAwareJob.WasCancellationObserved.ShouldBeTrue();
    }

    private ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class SuccessfulJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            context.Messages.Add("Completed successfully.");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FailedResultJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            context.Messages.Add("Returning failure.");
            return Task.FromResult(Result.Failure().WithError(new ValidationError("The job failed.")));
        }
    }

    private sealed class ThrowingJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Boom");
        }
    }

    private sealed class LifetimeTrackingJob : JobBase
    {
        private readonly Guid instanceId = Guid.NewGuid();

        public static int ConstructionCount;

        public static List<Guid> ExecutionInstanceIds { get; } = [];

        public LifetimeTrackingJob()
        {
            Interlocked.Increment(ref ConstructionCount);
        }

        public static void Reset()
        {
            ConstructionCount = 0;
            ExecutionInstanceIds.Clear();
        }

        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            lock (ExecutionInstanceIds)
            {
                ExecutionInstanceIds.Add(this.instanceId);
            }

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TypedDataJob : JobBase<TypedPayload>
    {
        public static string LastCustomerId { get; private set; }

        public static void Reset()
        {
            LastCustomerId = null;
        }

        public override Task<Result> ExecuteAsync(IJobExecutionContext<TypedPayload> context, CancellationToken cancellationToken = default)
        {
            LastCustomerId = context.Data.CustomerId;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed record TypedPayload(string CustomerId);

    private sealed class RecordingSchedulerExceptionHandler : IJobSchedulerExceptionHandler
    {
        public TaskCompletionSource<JobSchedulerExceptionContext> Invocation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(JobSchedulerExceptionContext context, CancellationToken cancellationToken = default)
        {
            this.Invocation.TrySetResult(context);
            return Task.CompletedTask;
        }
    }

    private static class InlineDelegateRecorder
    {
        public static string LastCustomerId { get; set; }

        public static string LastTenant { get; set; }

        public static string LastDependencyValue { get; set; }

        public static bool LastProcessed { get; set; }

        public static void Reset()
        {
            LastCustomerId = null;
            LastTenant = null;
            LastDependencyValue = null;
            LastProcessed = false;
        }
    }

    private sealed class InlineDependency
    {
        public string Value { get; } = "scoped-dependency";
    }

    private sealed class CancellationAwareJob : JobBase
    {
        public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static bool WasCancellationObserved { get; private set; }

        public static void Reset()
        {
            Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WasCancellationObserved = false;
        }

        public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                WasCancellationObserved = context.CancellationToken.IsCancellationRequested;
                throw;
            }
        }
    }
}
