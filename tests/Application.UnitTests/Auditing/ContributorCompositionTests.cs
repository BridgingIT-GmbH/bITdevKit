// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Application.DataPorter;
using Application.Notifications;
using Application.Storage;
using Application.Utilities;
using Microsoft.Extensions.DependencyInjection;

public sealed class ContributorCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Registration_BothOrders_IsIdempotentAndDoesNotResolveSourceFactories(bool coreFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (coreFirst) services.AddAuditing();
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithFakeSmtpClient().WithAuditing().WithAuditing());
        services.AddDataPorter().WithAuditing().WithAuditing();
        services.AddStartupTasks().WithBehavior<AuditingStartupTaskBehavior>().WithBehavior<AuditingStartupTaskBehavior>();
        services.AddFileMonitoring(builder => builder.WithAuditing().WithAuditing().UseInMemory("inbox", location =>
        {
            location.UseOnDemandOnly = true;
            location.UseProcessor<UnresolvedProcessor>();
        }));
        if (!coreFirst) services.AddAuditing();
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var registrations = host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations).ToArray();
        registrations.ShouldContain(item => item.Feature == "Email" && item.Boundary == "Worker" && item.Selection == "selected");
        registrations.ShouldContain(item => item.Feature == "DataPorter" && item.Selection == "selected");
        registrations.ShouldContain(item => item.Feature == "StartupTasks" && item.Selection == "selected");
        registrations.ShouldContain(item => item.Feature == "FileMonitoring" && item.Boundary == "Scan" && item.Selection == "omitted");
        registrations.Select(item => item.Identity).Distinct().Count().ShouldBe(registrations.Length);
        host.GetServices<INotificationOperationBehavior>().OfType<AuditingNotificationOperationBehavior>().ShouldHaveSingleItem();
        host.GetServices<IDataPorterOperationBehavior>().OfType<AuditingDataPorterOperationBehavior>().ShouldHaveSingleItem();
        host.GetServices<IStartupTaskBehavior>().OfType<AuditingStartupTaskBehavior>().ShouldHaveSingleItem();
        host.GetServices<IFileProcessingExecutionBehavior>().OfType<AuditingFileProcessingBehavior>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("disabled")]
    [InlineData("suppressed")]
    public async Task Contributors_AbsentDisabledOrSuppressedCore_SkipSelectorsAndPreserveBusinessResults(string mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (mode != "absent") services.AddAuditing(options => options.Enabled(mode != "disabled"));
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithFakeSmtpClient().WithAuditing());
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = host.CreateAsyncScope();
        using var suppression = mode == "suppressed" ? scope.ServiceProvider.GetRequiredService<IAuditContext>().Suppress() : null;
        (await scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>().QueueAsync(new() { Id = Guid.NewGuid(), Subject = "private-subject" }, default)).IsSuccess.ShouldBeTrue();
        (await scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsync<Customer>((Stream)null)).IsFailure.ShouldBeTrue();
        var calls = 0;
        var startup = new AuditingStartupTaskBehavior(host.GetRequiredService<IServiceScopeFactory>(), new() { SelectTask = _ => throw new InvalidOperationException("Selector must not run.") });
        await startup.Execute(new TaskFixture(), default, () => { calls++; return Task.CompletedTask; });
        var file = new AuditingFileProcessingBehavior(host.GetRequiredService<IServiceScopeFactory>(), new() { SelectLocation = _ => throw new InvalidOperationException("Selector must not run.") });
        var expected = Result.Success();
        (await file.ExecuteAsync(new() { Operation = "Event", LocationName = "inbox" }, () => { calls++; return Task.FromResult(expected); })).ShouldBe(expected);
        calls.ShouldBe(2);
        if (mode != "absent") (await AuditFeatureAdapterTests.Records(host)).ShouldBeEmpty();
        else host.GetService<IAuditContext>().ShouldBeNull();
    }

    [Fact]
    public void Contributors_DefaultRegistrations_InstallNoAuditImplementationOrAdapter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithFakeSmtpClient());
        services.AddDataPorter();
        services.AddStartupTasks();
        services.AddFileMonitoring(builder => builder.UseInMemory("inbox", location => location.UseOnDemandOnly = true));
        using var host = services.BuildServiceProvider();
        host.GetService<IAuditContext>().ShouldBeNull();
        host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations).ShouldAllBe(item => item.Selection == "omitted");
        host.GetServices<INotificationOperationBehavior>().ShouldBeEmpty();
        host.GetServices<IDataPorterOperationBehavior>().ShouldBeEmpty();
        host.GetServices<IStartupTaskBehavior>().ShouldBeEmpty();
        host.GetServices<IFileProcessingExecutionBehavior>().ShouldBeEmpty();
    }

    [Fact]
    public void KnownAuditDeduplication_PreservesEveryCustomRegistration()
    {
        var first = new object();
        var audit = new CaptureMarker();
        AuditSourceExecutor.DistinctAuditing<object, CaptureMarker>([first, first, audit, new CaptureMarker(), first]).ShouldBe(new[] { first, first, audit, first });
    }

    public sealed class CaptureMarker { }
    public sealed class Customer { public string Name { get; set; } }
    public sealed class TaskFixture : IStartupTask { public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    public sealed class UnresolvedProcessor : IFileEventProcessor
    {
        public UnresolvedProcessor() => throw new InvalidOperationException("Diagnostics constructed a processor.");
        public string ProcessorName => "CustomerImport";
        public bool IsEnabled { get; set; } = true;
        public IEnumerable<IProcessorBehavior> Behaviors => [];
        public Task ProcessAsync(FileProcessingContext context, CancellationToken token) => Task.CompletedTask;
    }
}
