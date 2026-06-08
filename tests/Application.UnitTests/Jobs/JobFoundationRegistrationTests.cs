// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public class JobFoundationRegistrationTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    public sealed class SampleJobAccessor : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    [Fact]
    public void WithJob_ValidRegistration_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);

        // Act
        var context = services.AddJobScheduler().AliveEnabled(false);
        context.WithJob<SampleJob>("cleanup", job => job
            .Description("Removes stale records.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        var definitions = GetStore(services).GetDefinitions();

        // Assert
        definitions.Count.ShouldBe(1);
        definitions[0].JobName.ShouldBe("cleanup");
        definitions[0].Description.ShouldBe("Removes stale records.");
        definitions[0].DataType.ShouldBe(typeof(Unit));
        definitions[0].Lifetime.ShouldBe(ServiceLifetime.Transient);
        definitions[0].Triggers.Count.ShouldBe(1);
        definitions[0].Triggers[0].TriggerType.ShouldBe(JobTriggerType.Manual);
    }

    [Fact]
    public void Build_ExplicitLifetime_IsStoredOnDefinition()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);

        services.AddJobScheduler()
            .AliveEnabled(false)
            .WithJob<SampleJob>("cleanup", job => job
                .Description("Removes stale records.")
                .UseLifetime(ServiceLifetime.Singleton)
                .AddTrigger("manual", trigger => trigger.Manual()));

        var definition = GetStore(services).GetDefinitions().Single();

        definition.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Build_InvalidLifetime_FailsValidation()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        var action = () => context.WithJob<SampleJob>("cleanup", job => job
            .Description("Removes stale records.")
            .UseLifetime((ServiceLifetime)999)
            .AddTrigger("manual", trigger => trigger.Manual()));

        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void GetDefinitions_ModuleIsResolvedFromAccessor_WhenNoExplicitModuleIsConfigured()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IModuleContextAccessor>(new StaticModuleContextAccessor(typeof(SampleJob), new TestModule("Reporting")));
        services.AddJobScheduler()
            .AliveEnabled(false)
            .WithJob<SampleJob>("cleanup", job => job
                .Description("Removes stale records.")
                .AddTrigger("manual", trigger => trigger.Manual()));

        using var provider = services.BuildServiceProvider();
        var definition = provider.GetRequiredService<JobRegistrationStore>().GetDefinitions().Single();

        definition.Module.ShouldBe("Reporting");
        definition.DisplayName.ShouldBe("reporting-sample-job");
    }

    [Fact]
    public void GetDefinitions_ExplicitModuleWinsOverAccessor()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IModuleContextAccessor>(new StaticModuleContextAccessor(typeof(SampleJob), new TestModule("Ignored")));
        services.AddJobScheduler()
            .AliveEnabled(false)
            .WithJob<SampleJob>("cleanup", job => job
                .Description("Removes stale records.")
                .Module("Explicit")
                .AddTrigger("manual", trigger => trigger.Manual()));

        using var provider = services.BuildServiceProvider();
        var definition = provider.GetRequiredService<JobRegistrationStore>().GetDefinitions().Single();

        definition.Module.ShouldBe("Explicit");
        definition.DisplayName.ShouldBe("explicit-sample-job");
    }

    [Fact]
    public void GetDefinitions_AppSettingsCanOverrideTargetInstances()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jobs:cleanup:TargetInstances"] = "node-a,node-b",
                ["Jobs:cleanup:Triggers:manual:TargetInstances"] = "node-c",
            })
            .Build();

        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddJobScheduler(configuration)
            .AliveEnabled(false)
            .WithJob<SampleJob>("cleanup", job => job
                .Description("Removes stale records.")
                .TargetInstances("node-z")
                .AddTrigger("manual", trigger => trigger.Manual()));

        using var provider = services.BuildServiceProvider();
        var definition = provider.GetRequiredService<JobRegistrationStore>().GetDefinitions().Single();

        definition.TargetInstances.ShouldBe(["node-a", "node-b"]);
        definition.Triggers.Single().TargetInstances.ShouldBe(["node-c"]);
    }

    [Fact]
    public void WithJob_DuplicateJobName_FailsValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);
        context.WithJob<SampleJob>("cleanup", job => job
            .Description("First job.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        // Act
        var action = () => context.WithJob<AnotherSampleJob>("cleanup", job => job
            .Description("Second job.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Build_DuplicateTriggerNameWithinJob_FailsValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        // Act
        var action = () => context.WithJob<SampleJob>("cleanup", job => job
            .Description("Removes stale records.")
            .AddTrigger("manual", trigger => trigger.Manual())
            .AddTrigger("manual", trigger => trigger.Manual()));

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Build_MissingDescription_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        // Act
        context.WithJob<SampleJob>("cleanup", job => job
            .AddTrigger("manual", trigger => trigger.Manual()));

        var definition = GetStore(services)
            .GetDefinitions()
            .Single();

        // Assert
        definition.JobName.ShouldBe("cleanup");
        definition.Description.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_EmptyExplicitDescription_SucceedsAndClearsDescription(string description)
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        // Act
        context.WithJob<SampleJob>("cleanup", job => job
            .Description(description)
            .AddTrigger("manual", trigger => trigger.Manual()));

        var definition = GetStore(services)
            .GetDefinitions()
            .Single();

        // Assert
        definition.Description.ShouldBeNull();
    }

    [Fact]
    public void Build_TypedJobBase_InfersTypedDataContract()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        // Act
        context.WithJob<TypedSampleJob>("export-customers", job => job
            .Description("Exports customers.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        var definition = GetStore(services)
            .GetDefinitions()
            .Single();

        // Assert
        definition.DataType.ShouldBe(typeof(SampleJobData));
    }

    [Fact]
    public void Build_MismatchedExplicitDataContract_FailsValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        // Act
        var action = () => context.WithJob<TypedSampleJob>("export-customers", job => job
            .Description("Exports customers.")
            .WithData<MismatchedJobData>()
            .AddTrigger("manual", trigger => trigger.Manual()));

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void GetDefinitions_AppSettingsOverrideMatchingRegistration_AppliesEnabledStateAndSchedule()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jobs:cleanup:Enabled"] = "false",
                ["Jobs:cleanup:Triggers:nightly:Schedule"] = "0 30 2 * * *",
                ["Jobs:cleanup:Triggers:nightly:Enabled"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler(configuration).AliveEnabled(false);
        context.WithJob<SampleJob>("cleanup", job => job
            .Description("Removes stale records.")
            .AddTrigger("nightly", trigger => trigger.Cron("0 0 2 * * *").Enabled()));

        // Act
        var definition = GetStore(services)
            .GetDefinitions()
            .Single();

        // Assert
        definition.Enabled.ShouldBeFalse();
        definition.Triggers.Single().Enabled.ShouldBeFalse();
        definition.Triggers.Single().Schedule.ShouldBe("0 30 2 * * *");
    }

    [Fact]
    public void GetDefinitions_UnknownConfiguredJob_FailsFast()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jobs:unknown:Enabled"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddJobScheduler(configuration).AliveEnabled(false);

        // Act
        var action = () => GetStore(services).GetDefinitions();

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void GetDefinitions_UnknownConfiguredTrigger_FailsFast()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jobs:cleanup:Triggers:unknown:Enabled"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler(configuration).AliveEnabled(false);
        context.WithJob<SampleJob>("cleanup", job => job
            .Description("Removes stale records.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        // Act
        var action = () => GetStore(services).GetDefinitions();

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void GetDefinitions_ConfigOnlyJob_DoesNotBecomeDefinitionSource()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jobs:config-only:Enabled"] = "true",
                ["Jobs:config-only:Triggers:nightly:Schedule"] = "0 0 2 * * *",
            })
            .Build();

        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddJobScheduler(configuration).AliveEnabled(false);

        // Act
        var action = () => GetStore(services).GetDefinitions();

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void WithJob_InlineDelegateRegistration_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);

        // Act
        services.AddJobScheduler()
            .AliveEnabled(false)
            .WithJob("cleanup-inline", job => job
                .WithDescription("Runs inline cleanup logic.")
                .Execute((context, cancellationToken) => Task.FromResult(Result.Success()))
                .AddTrigger("manual", trigger => trigger.Manual()));

        var definition = GetStore(services)
            .GetDefinitions()
            .Single();

        // Assert
        definition.JobName.ShouldBe("cleanup-inline");
        definition.JobType.ShouldBe(typeof(InlineJobRuntime));
        definition.DataType.ShouldBe(typeof(Unit));
        definition.Triggers.Single().TriggerType.ShouldBe(JobTriggerType.Manual);
    }

    [Fact]
    public void WithJob_InlineDelegateMissingExecute_FailsValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);

        // Act
        var action = () => services.AddJobScheduler()
            .AliveEnabled(false)
            .WithJob("cleanup-inline", job => job
                .WithDescription("Runs inline cleanup logic.")
                .AddTrigger("manual", trigger => trigger.Manual()));

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task AddJobScheduler_WhenCalledMultipleTimes_ComposesOneSchedulerAndAccumulatesRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();

        // Act
        var first = services.AddJobScheduler().AliveEnabled(false);
        first.WithBackgroundExecution(options => options.EnableBackgroundExecution = false);
        first.WithJob<SampleJob>("cleanup", job => job
            .Description("Removes stale records.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        var second = services.AddJobScheduler().AliveEnabled(false);
        second.WithJob<AnotherSampleJob>("rebuild-index", job => job
            .Description("Rebuilds the search index.")
            .AddTrigger("manual", trigger => trigger.Manual()));

        using var provider = services.BuildServiceProvider();
        var definitions = provider.GetRequiredService<JobRegistrationStore>().GetDefinitions();
        var scheduler = provider.GetRequiredService<IJobSchedulerService>();
        var schedulerService = provider.GetRequiredService<JobSchedulerService>();
        var hostedServices = provider.GetServices<IHostedService>().OfType<JobSchedulerBackgroundService>().ToList();
        var healthReport = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        first.Registrations.ShouldBeSameAs(second.Registrations);
        definitions.Select(x => x.JobName).OrderBy(x => x).ShouldBe(["cleanup", "rebuild-index"]);
        scheduler.ShouldBeSameAs(schedulerService);
        hostedServices.Count.ShouldBe(1);
        healthReport.Entries.ShouldContainKey(nameof(JobSchedulerBackgroundService));
    }

    [Fact]
    public void AddJobScheduler_WhenOtherHostedServiceAlreadyRegistered_RegistersSchedulerHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddHostedService<OtherHostedService>();

        // Act
        services.AddJobScheduler().AliveEnabled(false);

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        // Assert
        hostedServices.OfType<OtherHostedService>().Count().ShouldBe(1);
        hostedServices.OfType<JobSchedulerBackgroundService>().Count().ShouldBe(1);
    }

    [Fact]
    public void WithExceptionHandler_RegistersSingleHandlerImplementation()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);

        services.AddJobScheduler()
            .AliveEnabled(false)
            .WithExceptionHandler<RecordingSchedulerExceptionHandler>()
            .WithExceptionHandler<RecordingSchedulerExceptionHandler>();

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<IJobSchedulerExceptionHandler>().ToList();

        handlers.Count.ShouldBe(1);
        handlers[0].ShouldBeOfType<RecordingSchedulerExceptionHandler>();
    }

    [Fact]
    public void WithExceptionHandler_InvalidType_FailsValidation()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        var context = services.AddJobScheduler().AliveEnabled(false);

        var action = () => context.WithExceptionHandler(typeof(SampleJob));

        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddJobScheduler_FluentSchedulerOptions_ConfigureInstanceIdAndWorkerPool()
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment { ApplicationName = "jobs-app" });

        services.AddJobScheduler()
            .AliveEnabled(false)
            .InstanceId(context => $"{context.Environment.MachineName}-jobs")
            .StartupDelay(TimeSpan.FromSeconds(10))
            .WorkerPool(pool => pool
                .MaxConcurrency(16)
                .PollInterval(TimeSpan.FromSeconds(5))
                .BatchSize(100));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<JobSchedulerHostedOptions>();
        var scheduler = provider.GetRequiredService<JobSchedulerService>();

        options.StartupDelay.ShouldBe(TimeSpan.FromSeconds(10));
        options.MaxConcurrency.ShouldBe(16);
        options.SweepInterval.ShouldBe(TimeSpan.FromSeconds(5));
        options.BatchSize.ShouldBe(100);
        scheduler.SchedulerInstanceId.ShouldBe($"{Environment.MachineName}-jobs");
    }

    private sealed class SampleJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class AnotherSampleJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TypedSampleJob : JobBase<SampleJobData>
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<SampleJobData> context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed record SampleJobData(string CustomerId);

    private sealed record MismatchedJobData(string OrderId);

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    private sealed class OtherHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingSchedulerExceptionHandler : IJobSchedulerExceptionHandler
    {
        public Task HandleAsync(JobSchedulerExceptionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StaticModuleContextAccessor(Type jobType, IModule module) : IModuleContextAccessor
    {
        public IModule Find(Type type) => type == jobType ? module : null;
    }

    private sealed class TestModule(string name) : IModule
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; } = true;

        public string Name { get; } = name;

        public int Priority => 0;

        public IServiceCollection Register(IServiceCollection services, IConfiguration configuration = null, IWebHostEnvironment environment = null) => services;

        public IApplicationBuilder Use(IApplicationBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null) => app;
    }

    private static JobRegistrationStore GetStore(ServiceCollection services)
    {
        var descriptor = services.Single(x => x.ServiceType == typeof(JobRegistrationStore));
        if (descriptor.ImplementationInstance is JobRegistrationStore store)
        {
            return store;
        }

        return services.BuildServiceProvider().GetRequiredService<JobRegistrationStore>();
    }
}
