// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public sealed class AuditDiagnosticsTests
{
    [Fact]
    public async Task Probes_ArePureAndDistinguishStaticSelectionFromObservedCapture()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuditing();
        var catalog = AuditBoundaryRegistrationCatalog.GetOrAdd(services, "Requester", "Dispatch", "Handler"); catalog.Select("Dispatch"); catalog.MarkUnknown("Handler");
        var provider = Substitute.For<IAuditingStorageProvider>(); provider.Capabilities.Returns(new AuditProviderCapabilities()); services.AddSingleton(provider);
        await using var host = services.BuildServiceProvider(); var diagnostics = host.GetRequiredService<AuditDiagnosticsService>();
        (await diagnostics.ExplainAsync(new() { Feature = "Manual" })).Value.Decision.ShouldBe("Eligible");
        (await diagnostics.ExplainAsync(new() { Feature = "Requester", Boundary = "Dispatch" })).Value.Decision.ShouldBe("Eligible");
        (await diagnostics.ExplainAsync(new() { Feature = "Requester", Boundary = "Handler" })).Value.Decision.ShouldBe("Unknown");
        (await diagnostics.ExplainAsync(new() { Feature = "NotConfigured" })).Value.Decision.ShouldBe("Unknown");
        (await diagnostics.ExplainAsync(new() { IsRead = true })).Value.ReasonCode.ShouldBe("ReadNotSelected");
        (await diagnostics.ExplainAsync(new() { Suppressed = true })).Value.ReasonCode.ShouldBe("Suppressed");
        (await diagnostics.ExplainAsync(new() { Feature = new string('x', 129) })).IsFailure.ShouldBeTrue();
        var snapshot = await diagnostics.GetSnapshotAsync(); snapshot.Observations.ShouldBeEmpty(); snapshot.Failures.ShouldBeEmpty(); snapshot.Registrations.Length.ShouldBe(2);
        await provider.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
        await provider.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
        await provider.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualScopesAndPredictionShareRootCapacityPolicyAndRetainBoundedObservationFacts()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuditing(options => options.Limits(limits => limits with { MaxActiveRoots = 1 }));
        await using var host = services.BuildServiceProvider(); await using var scope = host.CreateAsyncScope(); var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var diagnostics = host.GetRequiredService<AuditDiagnosticsService>();
        await using (var root = audit.BeginScope(options => options.Key("customers.rename")))
        {
            var prediction = await diagnostics.ExplainAsync(new() { HasRecordingParent = true, Mode = AuditScopeMode.RequiresNew }); prediction.Value.ReasonCode.ShouldBe("AuditRootLimit");
            await using var rejected = audit.BeginScope(new AuditScopeOptionsBuilder().Key("customers.independent").Mode(AuditScopeMode.RequiresNew).Build()); rejected.IsRecording.ShouldBeFalse();
            var source = (IAuditSourceScope)root; source.SetSource(new() { Feature = "Requester" }); source.SetSource(new() { Feature = "Requester" });
            root.Complete();
        }

        var snapshot = await diagnostics.GetSnapshotAsync(); snapshot.Observations["RootAdmitted"].ShouldBe(1); snapshot.Observations["BoundaryAdmitted.Requester"].ShouldBe(1); snapshot.Observations["AppendPresenceConfirmed"].ShouldBe(1);
        for (var index = 0; index < 1000; index++) host.GetRequiredService<AuditRuntime>().ObserveBoundary("custom-resource-" + index);
        (await diagnostics.GetSnapshotAsync()).Observations.Count.ShouldBeLessThan(10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestrictedOrFailedScope_DoesNotDiscloseProcessCounters(bool fails)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddAuditing().WithReadScope((_, _) => Task.FromResult(fails ? Result<AuditReadScope>.Failure(new AuditError("AuditReadScopeUnavailable")) : Result<AuditReadScope>.Success(new(new() { Filters = [new("ModuleName", FilterOperator.Equal, "Customers")] }, "customers-v1"))));
        await using var root = services.BuildServiceProvider(); await using var scope = root.CreateAsyncScope();
        var runtime = root.GetRequiredService<AuditRuntime>(); runtime.ObserveBoundary("Email");
        var diagnostics = scope.ServiceProvider.GetRequiredService<AuditDiagnosticsService>(); var snapshot = await diagnostics.GetSnapshotAsync();
        snapshot.OperationalVisibility.ShouldBe(fails ? "Unavailable" : "Restricted"); snapshot.Observations.ShouldBeNull(); snapshot.Failures.ShouldBeNull(); snapshot.Delivery.ShouldBeNull(); snapshot.MemoryEvictedRecords.ShouldBeNull(); snapshot.Registrations.ShouldBeEmpty();
        (await diagnostics.ExplainAsync(new())).Value.Decision.ShouldBe("Unknown");
    }

    [Fact]
    public async Task Inventory_IsBoundedWithoutInvokingFactoriesOrBusinessMetadata()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuditing();
        var catalog = AuditBoundaryRegistrationCatalog.GetOrAdd(services, "Storage", "Client");
        for (var index = 0; index < 100; index++) catalog.Declare("Client", "Client" + index, "selected");
        await using var host = services.BuildServiceProvider(); var snapshot = await host.GetRequiredService<AuditDiagnosticsService>().GetSnapshotAsync();
        snapshot.Registrations.Length.ShouldBe(32); snapshot.AdditionalDeclarationsOmitted.ShouldBeTrue(); snapshot.Observations.ShouldBeEmpty();
    }
}
