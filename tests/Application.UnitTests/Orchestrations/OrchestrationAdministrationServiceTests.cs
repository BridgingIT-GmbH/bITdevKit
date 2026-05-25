// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

public class OrchestrationAdministrationServiceTests
{
    [Fact]
    public async Task ArchiveAsync_WhenInstanceIsTerminal_ArchivesInstance()
    {
        using var provider = CreateServices();
        var instances = provider.GetRequiredService<IOrchestrationInstanceStore>();
        var admin = provider.GetRequiredService<IOrchestrationAdministrationService>();
        var queries = provider.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = Guid.NewGuid();

        await instances.CreateAsync(CreateContext(instanceId, OrchestrationStatus.Completed));

        var result = await admin.ArchiveAsync(instanceId);
        var snapshot = await queries.GetInstanceAsync(instanceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("was archived");
        snapshot.ShouldNotBeNull();
        snapshot.IsArchived.ShouldBeTrue();
        snapshot.ArchivedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReleaseLeaseAsync_WhenLeaseExists_ReleasesLease()
    {
        using var provider = CreateServices();
        var instances = provider.GetRequiredService<IOrchestrationInstanceStore>();
        var leases = provider.GetRequiredService<IOrchestrationLeaseStore>();
        var admin = provider.GetRequiredService<IOrchestrationAdministrationService>();
        var instanceId = Guid.NewGuid();

        await instances.CreateAsync(CreateContext(instanceId, OrchestrationStatus.Waiting));
        var lease = await leases.AcquireAsync(instanceId, "worker-1", TimeSpan.FromMinutes(5));

        var result = await admin.ReleaseLeaseAsync(instanceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("was released");
        (await leases.VerifyAsync(instanceId, lease.LeaseId, "worker-1")).ShouldBeFalse();
    }

    [Fact]
    public async Task RequeueTimersAsync_WhenTimersWereProcessed_RequeuesTimers()
    {
        using var provider = CreateServices();
        var instances = provider.GetRequiredService<IOrchestrationInstanceStore>();
        var timers = provider.GetRequiredService<IOrchestrationTimerStore>();
        var admin = provider.GetRequiredService<IOrchestrationAdministrationService>();
        var queries = provider.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 05, 07, 10, 00, 00, TimeSpan.Zero);

        await instances.CreateAsync(CreateContext(instanceId, OrchestrationStatus.Waiting));
        var timer = await timers.ScheduleAsync(instanceId, "WaitDelay", now.AddMinutes(5), continuation: "resume");
        await timers.UpdateStatusAsync(timer.TimerId, OrchestrationTimerStatus.Consumed, "handled");

        var result = await admin.RequeueTimersAsync(instanceId);
        var stored = await queries.GetTimersAsync(instanceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("1 timer(s)");
        stored.Single().Status.ShouldBe(OrchestrationTimerStatus.Pending);
        stored.Single().ProcessedUtc.ShouldBeNull();
    }

    [Fact]
    public async Task PurgeAsync_WhenFiltersMatch_RemovesRetainedData()
    {
        using var provider = CreateServices();
        var instances = provider.GetRequiredService<IOrchestrationInstanceStore>();
        var history = provider.GetRequiredService<IOrchestrationHistoryStore>();
        var signals = provider.GetRequiredService<IOrchestrationSignalStore>();
        var timers = provider.GetRequiredService<IOrchestrationTimerStore>();
        var admin = provider.GetRequiredService<IOrchestrationAdministrationService>();
        var queries = provider.GetRequiredService<IOrchestrationQueryStore>();
        var instanceId = Guid.NewGuid();

        var context = CreateContext(instanceId, OrchestrationStatus.Completed);
        await instances.CreateAsync(context);
        await history.AppendAsync(new OrchestrationHistoryEntry { InstanceId = instanceId, EventType = "Completed" });
        await signals.PersistAsync(instanceId, "Approved", new TestSignalPayload("ok"));
        await timers.ScheduleAsync(instanceId, "WaitDelay", DateTimeOffset.UtcNow.AddMinutes(-5));
        await admin.ArchiveAsync(instanceId);

        var result = await admin.PurgeAsync(new OrchestrationPurgeRequest
        {
            OlderThan = DateTimeOffset.UtcNow.AddMinutes(1),
            Statuses = [nameof(OrchestrationStatus.Completed)],
            IsArchived = true,
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.PurgedInstanceCount.ShouldBe(1);
        (await queries.GetInstanceAsync(instanceId)).ShouldBeNull();
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOrchestrations();

        return services.BuildServiceProvider();
    }

    private static OrchestrationContext<TestOrchestrationData> CreateContext(Guid instanceId, OrchestrationStatus status)
    {
        var services = new ServiceCollection().BuildServiceProvider();

        return new OrchestrationContext<TestOrchestrationData>(
            "OrderApproval",
            new TestOrchestrationData { OrderId = "ORD-42" },
            services,
            instanceId: instanceId,
            correlationId: "corr-42",
            startedUtc: DateTimeOffset.UtcNow.AddMinutes(-30))
        {
            Status = status,
            CurrentState = status == OrchestrationStatus.Completed ? "Completed" : "AwaitingApproval",
            CurrentActivity = "SendApprovalEmail",
            CompletedUtc = status == OrchestrationStatus.Completed ? DateTimeOffset.UtcNow.AddMinutes(-20) : null,
        };
    }

    private sealed class TestOrchestrationData : IOrchestrationData
    {
        public string OrderId { get; set; }
    }

    private sealed record TestSignalPayload(string Value);
}