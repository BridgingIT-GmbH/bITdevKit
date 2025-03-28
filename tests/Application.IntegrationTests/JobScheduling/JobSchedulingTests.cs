// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.JobScheduling;

using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Shouldly;
using Xunit;

public class JobSchedulingTests
{
    private readonly ILoggerFactory loggerFactory;

    public JobSchedulingTests()
    {
        this.loggerFactory = Substitute.For<ILoggerFactory>();
        this.loggerFactory.CreateLogger(Arg.Any<string>()).Returns(NullLogger.Instance);
    }

    private IHost CreateHost(Action<WebApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        configure(builder);
        var app = builder.Build();
        return app as IHost;
    }

    [Fact]
    public async Task BasicScheduler_Runs()
    {
        var host = this.CreateHost(builder => builder.Services.AddJobScheduling());
        await host.StartAsync();
        var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();
        Assert.True(scheduler.IsStarted);
        await host.StopAsync();
    }

    [Fact]
    public async Task ScopedJob_ExecutesWithData()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Named("TestJob")
                    .WithData("key1", "value1")
                    .RegisterScoped();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.Count.ShouldBeGreaterThanOrEqualTo(2);
        executed.All(e => e.Name == "TestJob").ShouldBeTrue();
        executed.All(e => e.Data["key1"] == "value1").ShouldBeTrue();
    }

    [Fact]
    public async Task SingletonJob_ExecutesWithMultipleData()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .WithData("key1", "value1")
                    .WithData("key2", "value2")
                    .RegisterSingleton();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.Count.ShouldBeGreaterThanOrEqualTo(2);
        executed.All(e => e.Data["key1"] == "value1").ShouldBeTrue();
        executed.All(e => e.Data["key2"] == "value2").ShouldBeTrue();
    }

    [Fact]
    public async Task DisabledJob_DoesNotExecute()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Enabled(false)
                    .RegisterScoped();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultipleJobs_ExecuteCorrectly()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Named("ScopedJob")
                    .WithData("type", "scoped")
                    .RegisterScoped()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Named("SingletonJob")
                    .WithData("type", "singleton")
                    .RegisterSingleton();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.Count(e => e.Name == "ScopedJob").ShouldBeGreaterThanOrEqualTo(2);
        executed.Count(e => e.Name == "SingletonJob").ShouldBeGreaterThanOrEqualTo(2);
        executed.Where(e => e.Name == "ScopedJob").All(e => e.Data["type"] == "scoped").ShouldBeTrue();
        executed.Where(e => e.Name == "SingletonJob").All(e => e.Data["type"] == "singleton").ShouldBeTrue();
    }

    [Fact]
    public async Task OldSyntax_WorksCorrectly()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>(
                    CronExpressions.EverySecond,
                    "OldSyntaxJob",
                    new Dictionary<string, string> { { "key", "value" } },
                    true);
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.All(e => e.Name == "OldSyntaxJob").ShouldBeTrue();
        executed.All(e => e.Data["key"] == "value").ShouldBeTrue();
    }

    [Fact]
    public async Task JobData_PersistsAcrossExecutions()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .WithData("persistent", "data")
                    .RegisterScoped();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.All(e => e.Data["persistent"] == "data").ShouldBeTrue();
    }

    [Fact]
    public async Task CronExpression_TriggersCorrectly()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron("0/2 * * * * ?")
                    .Named("CronTest")
                    .RegisterScoped();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(5000);
        await host.StopAsync();

        executed.Count.ShouldBeInRange(2, 3);
        executed.All(e => e.Name == "CronTest").ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidCronExpression_DoesNotCrash()
    {
        var executed = new List<(string Name, Dictionary<string, string> Data)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<TestJob>()
                    .Cron("ASD")
                    .Named("InvalidCronJob")
                    .WithData("key", "value")
                    .RegisterScoped()
                .WithJob<TestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Named("ValidJob")
                    .WithData("key", "value")
                    .RegisterScoped();
        });

        TestJob.OnExecute = (name, data) => executed.Add((name, data));
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.All(e => e.Name == "ValidJob").ShouldBeTrue();
        executed.All(e => e.Name != "InvalidCronJob").ShouldBeTrue();
        executed.All(e => e.Data["key"] == "value").ShouldBeTrue();
    }

    //[Fact]
    //public async Task Job_RespectsCancellation()
    //{
    //    var executed = new List<(string Name, Dictionary<string, string> Data, bool Cancelled)>();
    //    var host = this.CreateHost(builder =>
    //    {
    //        builder.Services
    //            .AddJobScheduling()
    //            .WithJob<CancellableTestJob>()
    //                .Cron(CronExpressions.EverySecond)
    //                .Named("CancellableJob")
    //                .RegisterScoped();
    //    });

    //    CancellableTestJob.OnExecute = (name, data, cancelled) => executed.Add((name, data, cancelled));
    //    await host.StartAsync();
    //    await Task.Delay(1500); // Allow a couple executions
    //    await host.StopAsync(); // Trigger cancellation during a run

    //    executed.ShouldNotBeEmpty();
    //    executed.Count.ShouldBeGreaterThanOrEqualTo(1);
    //    executed.Any(e => e.Cancelled).ShouldBeTrue(); // At least one should be cancelled
    //    executed.All(e => e.Name == "CancellableJob").ShouldBeTrue();
    //}

    [Fact]
    public async Task NonConcurrentJob_DoesNotOverlap()
    {
        var executed = new List<(string Name, DateTime StartTime, DateTime EndTime)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<NonConcurrentTestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Named("NonConcurrentJob")
                    .RegisterScoped();
        });

        NonConcurrentTestJob.OnExecute = (name, start, end) => executed.Add((name, start, end));
        await host.StartAsync();
        await Task.Delay(3500); // Allow multiple triggers
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Check no overlap: each job's end time should be before the next start time
        for (var i = 0; i < executed.Count - 1; i++)
        {
            executed[i].EndTime.ShouldBeLessThanOrEqualTo(executed[i + 1].StartTime);
        }
        executed.All(e => e.Name == "NonConcurrentJob").ShouldBeTrue();
    }

    [Fact]
    public async Task FailingJob_DoesNotCrashScheduler()
    {
        var executed = new List<(string Name, int Attempt)>();
        var host = this.CreateHost(builder =>
        {
            builder.Services
                .AddJobScheduling()
                .WithJob<FailingTestJob>()
                    .Cron(CronExpressions.EverySecond)
                    .Named("FailingJob")
                    .RegisterScoped();
        });

        FailingTestJob.OnExecute = (name, attempt) => executed.Add((name, attempt));
        await host.StartAsync();
        await Task.Delay(2500); // Allow a couple attempts
        await host.StopAsync();

        executed.ShouldNotBeEmpty();
        executed.Count.ShouldBeGreaterThanOrEqualTo(2); // Should retry
        executed.All(e => e.Name == "FailingJob").ShouldBeTrue();
        executed.Select(e => e.Attempt).ShouldContain(a => a > 1); // At least one retry
    }
}

public static class CronExpressions
{
    public const string EverySecond = "0/1 * * * * ?";
}

public class TestJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    public static Action<string, Dictionary<string, string>> OnExecute;

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
        var data = context.JobDetail.JobDataMap.Keys
            .ToDictionary(k => k, k => context.JobDetail.JobDataMap[k]?.ToString() ?? string.Empty);

        OnExecute?.Invoke(name, data);
        await Task.Delay(100, cancellationToken);
    }
}

public class CancellableTestJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    public static Action<string, Dictionary<string, string>, bool> OnExecute;

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
        var data = context.JobDetail.JobDataMap.Keys
            .ToDictionary(k => k, k => context.JobDetail.JobDataMap[k]?.ToString() ?? string.Empty);

        await Task.Delay(1000, cancellationToken); // Long enough to be cancelled during shutdown
        OnExecute?.Invoke(name, data, cancellationToken.IsCancellationRequested);
    }
}

[DisallowConcurrentExecution]
public class NonConcurrentTestJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    public static Action<string, DateTime, DateTime> OnExecute;

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
        var startTime = DateTime.UtcNow;
        await Task.Delay(1500, cancellationToken); // Longer than trigger interval
        var endTime = DateTime.UtcNow;

        OnExecute?.Invoke(name, startTime, endTime);
    }
}

public class FailingTestJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    public static Action<string, int> OnExecute;
    private static int attemptCount = 0;

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
        attemptCount++;
        OnExecute?.Invoke(name, attemptCount);

        await Task.Delay(100, cancellationToken);
        if (attemptCount <= 2) // Fail on first two attempts
        {
            throw new Exception("Simulated job failure");
        }
    }
}