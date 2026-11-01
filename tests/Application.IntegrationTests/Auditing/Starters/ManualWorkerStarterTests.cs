// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Starters;

using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;

public sealed class ManualWorkerStarterTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ManualServiceRecipe_UsesBuiltInFallbackInEitherOrder(bool capture, bool fallbackFirst)
    {
        var services = new ServiceCollection(); services.AddLogging();
        if (fallbackFirst) services.AddNoOpAuditContext();
        if (capture) services.AddAuditing(options => options.Application("CustomerPortal"));
        if (!fallbackFirst) services.AddNoOpAuditContext();
        await using var host = services.BuildServiceProvider(); await using var scope = host.CreateAsyncScope(); var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var businessCalls = 0; var metadataCalls = 0; Guid id = default;
        var result = await audit.RunAsync(options => { metadataCalls++; options.Key("customers.rename").DisplayName("Rename customer"); }, (recording, _) =>
        {
            businessCalls++; id = recording.AuditId; recording.AddResource(new("Customer", "C-42")); recording.SetDetail("changed", AuditValue.From(false)); return Task.FromResult(Result<string>.Success("Unchanged"));
        });
        result.IsSuccess.ShouldBeTrue(); businessCalls.ShouldBe(1); metadataCalls.ShouldBe(capture ? 1 : 0);
        if (!capture) { host.GetService<IAuditingStorageProvider>().ShouldBeNull(); return; }

        var provider = host.GetRequiredService<IAuditingStorageProvider>(); var flush = await provider.FlushAsync(); flush.IsSuccess.ShouldBeTrue(); flush.Value.Persisted.ShouldBe(1);
        var stored = await provider.FindAsync(id); stored.IsSuccess.ShouldBeTrue(); stored.Value.EntityChanges.ShouldBeEmpty(); stored.Value.Outcome.ShouldBe(AuditOutcome.Succeeded); provider.Capabilities.IsDurable.ShouldBeFalse();
    }

    [Fact]
    public async Task WorkerRecipe_UsesFreshScopeAndIndependentServiceActorWithoutWebFrameworkWiring()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuditing(options => options.Application("CustomerPortal"));
        await using var host = services.BuildServiceProvider(); Guid id;
        await using (var scope = host.CreateAsyncScope())
        {
            using var boundary = scope.ServiceProvider.GetRequiredService<IAuditExecutionBoundary>().DetachExecution();
            await using var execution = scope.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(options => options.Key("customers.reconcile").DisplayName("Reconcile customer memberships"));
            execution.AddResource(new("Customer", "C-42")); execution.Complete(); id = execution.AuditId;
        }

        var stored = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id); stored.IsSuccess.ShouldBeTrue(); stored.Value.Actor.Kind.ShouldBe("Service"); stored.Value.InitiatingActor.ShouldBeNull();
    }
}
