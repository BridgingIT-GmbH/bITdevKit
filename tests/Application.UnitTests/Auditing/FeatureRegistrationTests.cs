// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Application.Messaging;
using Application.Orchestrations;
using Application.Queueing;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

public sealed class FeatureRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inventory_IsSafeBoundedAndIdempotentInBothCoreRegistrationOrders(bool coreFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (coreFirst) services.AddAuditing();
        services.AddMessaging().WithAuditing().WithAuditing();
        services.AddMessaging();
        services.AddQueueing().WithAuditing().WithAuditing();
        services.AddQueueing();
        services.AddOrchestrations().AliveEnabled(false).WithAuditingBehavior().WithAuditingBehavior();
        services.AddBlobStorage().WithAuditing().WithAuditing().WithClient("exports", _ => throw new InvalidOperationException("A client factory must not run during diagnostics."));
        services.AddDocumentStorage().WithAuditing<Customer>().WithAuditing<Customer>()
            .RegisterProvider<Customer>(_ => throw new InvalidOperationException("A provider factory must not run during diagnostics."));
        services.AddPipelines().WithPipeline<Context>("Configured", definition => definition.AddBehavior<PipelineAuditingBehavior>().AddStep(() => { }))
            .WithPipeline<OpaqueDefinition>();
        if (!coreFirst) services.AddAuditing();
        using var host = services.BuildServiceProvider();
        var metadata = host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations).ToArray();
        metadata.Select(item => item.Identity).Distinct().Count().ShouldBe(metadata.Length);
        metadata.Where(item => item.Feature is "Messaging" or "Queueing" or "Orchestrations").ShouldAllBe(item => item.Selection == "selected");
        metadata.ShouldContain(item => item.Feature == "Pipelines" && item.TypeAlias == "Configured" && item.Selection == "selected");
        metadata.ShouldContain(item => item.Feature == "Pipelines" && item.TypeAlias == nameof(OpaqueDefinition) && item.Selection == "unknown");
        host.GetServices<IOrchestrationBehavior>().OfType<AuditingOrchestrationBehavior>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Inventory_OmittedSources_DoNotInstallCapture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMessaging();
        services.AddQueueing();
        services.AddOrchestrations().AliveEnabled(false);
        services.AddBlobStorage();
        services.AddDocumentStorage();
        services.AddPipelines().WithPipeline<Context>("Unselected", definition => definition.AddStep(() => { }));
        using var host = services.BuildServiceProvider();
        host.GetService<IAuditContext>().ShouldBeNull();
        var metadata = host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations).ToArray();
        metadata.ShouldNotBeEmpty();
        metadata.ShouldAllBe(item => item.Selection == "omitted");
        host.GetServices<IOrchestrationBehavior>().ShouldBeEmpty();
    }

    [Fact]
    public void Inventory_OpaqueFactoriesAreUnknownWithoutRunningThem()
    {
        var services = new ServiceCollection();
        services.AddMessaging().WithBehavior((Func<IServiceProvider, IMessagePublisherBehavior>)(_ => throw new InvalidOperationException("Factory")));
        services.AddQueueing().WithBehavior((Func<IServiceProvider, IQueueHandlerBehavior>)(_ => throw new InvalidOperationException("Factory")));
        using var host = services.BuildServiceProvider();
        var metadata = host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations).ToArray();
        metadata.ShouldContain(item => item.Feature == "Messaging" && item.Boundary == "Producer" && item.Selection == "unknown");
        metadata.ShouldContain(item => item.Feature == "Queueing" && item.Boundary == "Consumer" && item.Selection == "unknown");
    }

    [Fact]
    public void Catalog_RepeatedAndOverflowDeclarations_RemainBounded()
    {
        var catalog = new AuditBoundaryRegistrationCatalog("Feature", "Operation");
        for (var index = 0; index < 500; index++) catalog.Declare("Operation", "Type" + index, "selected");
        catalog.Registrations.Count.ShouldBe(100);
        catalog.Registrations.ShouldContain(item => item.Boundary == "AdditionalDeclarations" && item.Selection == "unknown");
        catalog.Registrations.Select(item => item.Identity).Distinct().Count().ShouldBe(100);
    }

    public sealed class Customer { }
    public sealed class Context : PipelineContextBase { }
    public sealed class OpaqueDefinition : PipelineDefinition<Context>
    {
        public OpaqueDefinition() => throw new InvalidOperationException("A definition factory must not run during diagnostics.");
        protected override void Configure(IPipelineDefinitionBuilder<Context> builder) => builder.AddStep(() => { });
    }
}
