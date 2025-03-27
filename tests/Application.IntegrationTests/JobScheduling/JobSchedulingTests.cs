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

    [Fact]
    public async Task BasicScheduler_Runs()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services.AddJobScheduling();
        var app = builder.Build();
        var host = app as IHost;

        await host.StartAsync();
        var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();

        Assert.True(scheduler.IsStarted); // Verify scheduler is running
        await host.StopAsync();
    }

    [Fact]
    public async Task ScopedJob_ExecutesWithData()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddJobScheduling()
            .WithJob<TestJob>()
                .Cron(CronExpressions.EverySecond)
                .Named("TestJob")
                .WithData("key1", "value1")
                .RegisterScoped();

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500); // Wait for a couple of executions
        await host.StopAsync();

        // Assert
        executed.ShouldNotBeEmpty();
        executed.Count.ShouldBeGreaterThanOrEqualTo(2); // Should run at least twice in 2.5s
        executed.All(e => e.Name == "TestJob").ShouldBeTrue();
        executed.All(e => e.Data["key1"] == "value1").ShouldBeTrue();
    }

    [Fact]
    public async Task SingletonJob_ExecutesWithMultipleData()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddLogging()
            .AddJobScheduling()
            .WithJob<TestJob>()
                .Cron(CronExpressions.EverySecond)
                .WithData("key1", "value1")
                .WithData("key2", "value2")
                .RegisterSingleton();

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        // Assert
        executed.ShouldNotBeEmpty();
        executed.Count.ShouldBeGreaterThanOrEqualTo(2);
        executed.All(e => e.Data["key1"] == "value1").ShouldBeTrue();
        executed.All(e => e.Data["key2"] == "value2").ShouldBeTrue();
    }

    [Fact]
    public async Task DisabledJob_DoesNotExecute()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddJobScheduling()
            .WithJob<TestJob>()
                .Cron(CronExpressions.EverySecond)
                .Enabled(false)
                .RegisterScoped();

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        // Assert
        executed.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultipleJobs_ExecuteCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
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

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        // Assert
        executed.ShouldNotBeEmpty();
        executed.Count(e => e.Name == "ScopedJob").ShouldBeGreaterThanOrEqualTo(2);
        executed.Count(e => e.Name == "SingletonJob").ShouldBeGreaterThanOrEqualTo(2);
        executed.Where(e => e.Name == "ScopedJob").All(e => e.Data["type"] == "scoped").ShouldBeTrue();
        executed.Where(e => e.Name == "SingletonJob").All(e => e.Data["type"] == "singleton").ShouldBeTrue();
    }

    [Fact]
    public async Task OldSyntax_WorksCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddJobScheduling()
            .WithJob<TestJob>(
                CronExpressions.EverySecond,
                "OldSyntaxJob",
                new Dictionary<string, string> { { "key", "value" } },
                true);

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        // Assert
        executed.ShouldNotBeEmpty();
        executed.All(e => e.Name == "OldSyntaxJob").ShouldBeTrue();
        executed.All(e => e.Data["key"] == "value").ShouldBeTrue();
    }

    [Fact]
    public async Task JobData_PersistsAcrossExecutions()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddJobScheduling()
            .WithJob<TestJob>()
                .Cron(CronExpressions.EverySecond)
                .WithData("persistent", "data")
                .RegisterScoped();

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        // Assert
        executed.ShouldNotBeEmpty();
        executed.All(e => e.Data["persistent"] == "data").ShouldBeTrue();
    }

    [Fact]
    public async Task CronExpression_TriggersCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddJobScheduling()
            .WithJob<TestJob>()
                .Cron("0/2 * * * * ?") // Every 2 seconds
                .Named("CronTest")
                .RegisterScoped();

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(5000); // Wait 5 seconds
        await host.StopAsync();

        // Assert
        executed.Count.ShouldBeInRange(2, 3); // Expect 2-3 executions (5s / 2s = 2.5)
        executed.All(e => e.Name == "CronTest").ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidCronExpression_DoesNotCrash()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services
            .AddJobScheduling()
            .WithJob<TestJob>()
                .Cron("ASD") // Invalid cron expression
                .Named("InvalidCronJob")
                .WithData("key", "value")
                .RegisterScoped()
            .WithJob<TestJob>()
                .Cron(CronExpressions.EverySecond)
                .Named("ValidJob")
                .WithData("key", "value")
                .RegisterScoped();

        var app = builder.Build();
        var host = app as IHost;
        var executed = new List<(string Name, Dictionary<string, string> Data)>();

        TestJob.OnExecute = (name, data) => executed.Add((name, data));

        // Act
        await host.StartAsync();
        await Task.Delay(2500);
        await host.StopAsync();

        // Assert
        executed.ShouldNotBeEmpty(); // Valid job should still run
        executed.All(e => e.Name == "ValidJob").ShouldBeTrue(); // Only valid job executes
        executed.All(e => e.Name != "InvalidCronJob").ShouldBeTrue(); // Invalid job skipped
        executed.All(e => e.Data["key"] == "value").ShouldBeTrue();
    }
}

public class TestJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    public static Action<string, Dictionary<string, string>> OnExecute;

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var name = context.JobDetail.Description ?? context.JobDetail.Key.Name;
        // Safely convert values to strings
        var data = context.JobDetail.JobDataMap.Keys
            .ToDictionary(
                k => k,
                k => context.JobDetail.JobDataMap[k]?.ToString() ?? string.Empty);

        OnExecute?.Invoke(name, data);
        await Task.Delay(100, cancellationToken); // Simulate some work
    }
}