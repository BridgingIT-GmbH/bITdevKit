// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

public abstract class EntityFrameworkJobSchedulerExamplesTestsBase
{
    protected abstract EntityFrameworkJobSchedulerTestSupport Support { get; }

    [Fact]
    public async Task DispatchAndWaitAsync_ClassJobWithConstructorInjection_PersistsExpectedState()
    {
        var scheduler = this.Support.GetRequiredService<IJobSchedulerService>();
        var recorder = this.Support.GetRequiredService<ExampleUsageRecorder>();

        var result = await scheduler.DispatchAndWaitAsync(
            "reference-di-job",
            new ExamplePayload("customer-007"),
            new JobDispatchOptions
            {
                Properties = new PropertyBag { ["tenant"] = "alpha" },
            });

        var occurrence = await this.Support.Store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var history = await this.Support.Store.ExecutionHistory.ListAsync(result.Value.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.ShouldContain("Constructor injected job completed.");
        recorder.ClassJobExecutions.Count.ShouldBe(1);
        recorder.ClassJobExecutions[0].CustomerId.ShouldBe("customer-007");
        recorder.ClassJobExecutions[0].Tenant.ShouldBe("alpha");
        recorder.ClassJobExecutions[0].DependencyValue.ShouldBe("scoped-dependency");
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Completed);
        occurrence.Properties["tenant"].ShouldBe("alpha");
        history.ShouldContain(x => x.EventName == "ExecutionCompleted");
        history.ShouldContain(x => x.EventName == "OccurrenceCompleted");
    }

    [Fact]
    public async Task DispatchAndWaitAsync_InlineJobWithServiceProviderInjection_PersistsExpectedState()
    {
        var scheduler = this.Support.GetRequiredService<IJobSchedulerService>();
        var recorder = this.Support.GetRequiredService<ExampleUsageRecorder>();

        var result = await scheduler.DispatchAndWaitAsync(
            "reference-inline-job",
            new ExamplePayload("customer-042"),
            new JobDispatchOptions
            {
                Properties = new PropertyBag { ["tenant"] = "beta" },
            });

        var occurrence = await this.Support.Store.Occurrences.GetAsync(result.Value.OccurrenceId);
        var history = await this.Support.Store.ExecutionHistory.ListAsync(result.Value.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.ShouldContain("Inline delegate completed.");
        recorder.InlineJobExecutions.Count.ShouldBe(1);
        recorder.InlineJobExecutions[0].CustomerId.ShouldBe("customer-042");
        recorder.InlineJobExecutions[0].Tenant.ShouldBe("beta");
        recorder.InlineJobExecutions[0].DependencyValue.ShouldBe("scoped-dependency");
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Completed);
        occurrence.Properties["tenant"].ShouldBe("beta");
        history.ShouldContain(x => x.EventName == "ExecutionCompleted");
        history.ShouldContain(x => x.EventName == "OccurrenceCompleted");
    }

    [Fact]
    public async Task SweepOnceAsync_ChainedJobs_CreateAndExecuteSuccessorOccurrence()
    {
        var scheduler = this.Support.GetRequiredService<IJobSchedulerService>();
        var background = this.Support.GetRequiredService<JobSchedulerBackgroundService>();
        var recorder = this.Support.GetRequiredService<ExampleUsageRecorder>();

        var dispatch = await scheduler.DispatchAsync("reference-chain-source");
        dispatch.IsSuccess.ShouldBeTrue();

        for (var index = 0; index < 4; index++)
        {
            await background.SweepOnceAsync();
        }

        var occurrences = await this.Support.Store.Occurrences.ListAsync();
        var predecessor = occurrences.Single(x => x.OccurrenceId == dispatch.Value.OccurrenceId);
        var successor = occurrences.Single(x => x.JobName == "reference-chain-target");
        var dependencies = await this.Support.Store.Dependencies.ListByDependentAsync(successor.OccurrenceId);
        var history = await this.Support.Store.ExecutionHistory.ListAsync(successor.OccurrenceId);

        predecessor.Status.ShouldBe(JobOccurrenceStatus.Completed);
        successor.Status.ShouldBe(JobOccurrenceStatus.Completed);
        successor.Properties["chain:predecessorJob"].ShouldBe("reference-chain-source");
        successor.Properties["chain:predecessorTrigger"].ShouldBe("manual");
        successor.Properties["chain"] .ShouldBe("example");
        dependencies.Count.ShouldBe(1);
        dependencies[0].Status.ShouldBe(JobDependencyStatus.Satisfied);
        recorder.ChainSteps.ShouldBe(["source", "target"]);
        history.ShouldContain(x => x.EventName == "DependencySatisfied");
        history.ShouldContain(x => x.EventName == "OccurrenceCompleted");
    }

    protected static void ConfigureExampleServices(IServiceCollection services)
    {
        services.AddSingleton<ExampleUsageRecorder>();
        services.AddScoped<ExampleScopedDependency>();
    }

    protected static void ConfigureExampleJobs(JobBuilderContext jobs)
    {
        jobs.WithJob<ConstructorInjectionExampleJob>("reference-di-job", job => job
            .Description("Demonstrates a normal typed job with constructor injection.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        jobs.WithJob("reference-inline-job", (Action<InlineJobDefinitionBuilder>)(job => job
            .WithDescription("Demonstrates a normal inline delegate job.")
            .Execute<ExamplePayload>((Func<IJobExecutionContext<ExamplePayload>, IServiceProvider, CancellationToken, Task<Result>>)((context, serviceProvider, cancellationToken) =>
            {
                var recorder = serviceProvider.GetRequiredService<ExampleUsageRecorder>();
                var dependency = serviceProvider.GetRequiredService<ExampleScopedDependency>();
                recorder.InlineJobExecutions.Add(new ExampleExecutionRecord(
                    context.Data.CustomerId,
                    context.Properties.Get<string>("tenant", string.Empty),
                    dependency.Value));
                context.Messages.Add("Inline delegate completed.");
                return Task.FromResult(Result.Success());
            }))
            .AddTrigger("manual", trigger => trigger.Manual())));

        jobs.WithJob<ChainSourceExampleJob>("reference-chain-source", job => job
            .Description("Demonstrates chained job execution.")
            .AddTrigger("manual", trigger => trigger.Manual())
            .Then("reference-chain-target", chain => chain
                .WithTrigger("manual")
                .WithProperty("chain", "example")));

        jobs.WithJob<ChainTargetExampleJob>("reference-chain-target", job => job
            .Description("Runs after the chained predecessor completes.")
            .AddTrigger("manual", trigger => trigger.Manual()));
    }
}

[IntegrationTest("Infrastructure")]
public class EntityFrameworkSqliteJobSchedulerExamplesTests : EntityFrameworkJobSchedulerExamplesTestsBase, IDisposable
{
    private readonly string databasePath;
    private readonly EntityFrameworkJobSchedulerTestSupport support;

    public EntityFrameworkSqliteJobSchedulerExamplesTests(ITestOutputHelper output)
    {
        this.databasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-jobs-examples-{Guid.NewGuid():N}.db");
        this.support = new EntityFrameworkJobSchedulerTestSupport(
            output,
            options => options.UseSqlite($"Data Source={this.databasePath}"),
            ConfigureExampleServices,
            ConfigureExampleJobs);
    }

    protected override EntityFrameworkJobSchedulerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();

        try
        {
            if (File.Exists(this.databasePath))
            {
                File.Delete(this.databasePath);
            }
        }
        catch (IOException)
        {
        }
    }
}

[IntegrationTest("Infrastructure")]
[Collection(nameof(JobsTestEnvironmentCollection))]
public class EntityFrameworkSqlServerJobSchedulerExamplesTests(ITestOutputHelper output, JobsTestEnvironmentFixture fixture) : EntityFrameworkJobSchedulerExamplesTestsBase, IDisposable
{
    private readonly EntityFrameworkJobSchedulerTestSupport support = new(
        fixture.WithOutput(output).Output,
        options => options.UseSqlServer(fixture.SqlConnectionString),
        ConfigureExampleServices,
        ConfigureExampleJobs);

    protected override EntityFrameworkJobSchedulerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();
    }
}

[IntegrationTest("Infrastructure")]
[Collection(nameof(JobsTestEnvironmentCollection))]
public class EntityFrameworkPostgresJobSchedulerExamplesTests(ITestOutputHelper output, JobsTestEnvironmentFixture fixture) : EntityFrameworkJobSchedulerExamplesTestsBase, IDisposable
{
    private readonly EntityFrameworkJobSchedulerTestSupport support = new(
        fixture.WithOutput(output).Output,
        options => options.UseNpgsql(fixture.PostgresConnectionString),
        ConfigureExampleServices,
        ConfigureExampleJobs);

    protected override EntityFrameworkJobSchedulerTestSupport Support => this.support;

    public void Dispose()
    {
        this.support.Dispose();
    }
}

public sealed record ExamplePayload(string CustomerId);

public sealed record ExampleExecutionRecord(string CustomerId, string Tenant, string DependencyValue);

public sealed class ExampleUsageRecorder
{
    public List<ExampleExecutionRecord> ClassJobExecutions { get; } = [];

    public List<ExampleExecutionRecord> InlineJobExecutions { get; } = [];

    public List<string> ChainSteps { get; } = [];
}

public sealed class ExampleScopedDependency
{
    public string Value { get; } = "scoped-dependency";
}

public sealed class ConstructorInjectionExampleJob(ExampleScopedDependency dependency, ExampleUsageRecorder recorder) : JobBase<ExamplePayload>
{
    public override Task<Result> ExecuteAsync(IJobExecutionContext<ExamplePayload> context, CancellationToken cancellationToken = default)
    {
        recorder.ClassJobExecutions.Add(new ExampleExecutionRecord(
            context.Data.CustomerId,
            context.Properties.Get("tenant", string.Empty),
            dependency.Value));
        context.Messages.Add("Constructor injected job completed.");

        return Task.FromResult(Result.Success());
    }
}

public sealed class ChainSourceExampleJob(ExampleUsageRecorder recorder) : JobBase
{
    public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
    {
        recorder.ChainSteps.Add("source");
        context.Messages.Add("Chain source completed.");

        return Task.FromResult(Result.Success());
    }
}

public sealed class ChainTargetExampleJob(ExampleUsageRecorder recorder) : JobBase
{
    public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
    {
        recorder.ChainSteps.Add("target");
        context.Messages.Add("Chain target completed.");

        return Task.FromResult(Result.Success());
    }
}