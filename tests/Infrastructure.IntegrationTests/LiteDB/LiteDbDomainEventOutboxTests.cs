// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB;

using System.Diagnostics;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[IntegrationTest("Infrastructure")]
public class LiteDbDomainEventOutboxTests
{
    [Fact]
    public async Task Capture_ExplicitRollback_RetainsPendingEventsAndCommitsNeitherRecord()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox());
        var entity = new HistoryAccount { Name = "proposed" };
        entity.DomainEvents.Register(new AccountRenamed { Name = entity.Name });
        var transaction = host.Scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<HistoryAccount>>();
        await Should.ThrowAsync<InvalidOperationException>(async () => await transaction.ExecuteScopedAsync(async () =>
        {
            await host.Repository.InsertAsync(entity);
            entity.DomainEvents.GetAll().Count().ShouldBe(1);
            throw new InvalidOperationException("rollback");
        }));
        (await host.Repository.CountAsync()).ShouldBe(0);
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").Count())).ShouldBe(0);
        entity.DomainEvents.GetAll().Count().ShouldBe(1);
        await host.Repository.InsertAsync(entity);
        entity.DomainEvents.GetAll().ShouldBeEmpty();
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").Count())).ShouldBe(1);
    }

    [Fact]
    public async Task Delivery_FailureThenRetry_PersistsAttemptsAndExactEventFilter()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox(configure: o => o.RetryCount = 3));
        var first = new AccountRenamed { Name = "first" };
        var second = new AccountRenamed { Name = "second" };
        var entity = new HistoryAccount { Name = "initial" };
        entity.DomainEvents.Register([first, second]);
        await host.Repository.InsertAsync(entity);
        host.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns<Task<IResult>>(_ => throw new InvalidOperationException("delivery unavailable"));
        var sut = host.Services.GetRequiredService<IOutboxDomainEventWorker>();
        await sut.ProcessAsync(first.EventId.ToString());
        var row = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(first.EventId));
        row.Attempts.ShouldBe(1);
        row.ProcessedTicks.ShouldBeNull();
        row.LastError.ShouldContain("delivery unavailable");
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(second.EventId))).Attempts.ShouldBe(0);
        host.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResult>(Result.Success()));
        await sut.ProcessAsync(first.EventId.ToString());
        row = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(first.EventId));
        row.Attempts.ShouldBe(2);
        row.ProcessedTicks.ShouldNotBeNull();
        await sut.PurgeAsync(processedOnly: true);
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").Count())).ShouldBe(1);
    }

    [Fact]
    public async Task CompetingWorkers_UnexpiredClaim_DeliverOnceAndRenewWhileAwaiting()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox(configure: o =>
        {
            o.LeaseDuration = TimeSpan.FromSeconds(3);
            o.LeaseRenewalInterval = TimeSpan.FromMilliseconds(50);
        }));
        var domainEvent = new AccountRenamed { Name = "reserved" };
        var entity = new HistoryAccount();
        entity.DomainEvents.Register(domainEvent);
        await host.Repository.InsertAsync(entity);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            entered.TrySetResult();
            await release.Task;
            return (IResult)Result.Success();
        });
        var sut = host.Services.GetRequiredService<IOutboxDomainEventWorker>();
        var delivery = sut.ProcessAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await sut.ProcessAsync();
        var leased = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
        leased.Attempts.ShouldBe(1);
        var initialExpiry = leased.LeaseUntilTicks;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            do
            {
                await Task.Delay(20, timeout.Token);
                leased = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
            }

            while (leased.LeaseUntilTicks <= initialExpiry);
        }
        finally { release.TrySetResult(); }

        leased.LeaseUntilTicks.ShouldBeGreaterThan(initialExpiry);
        await delivery;
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId))).ProcessedTicks.ShouldNotBeNull();
    }

    [Fact]
    public async Task LostClaim_StaleDelivery_CannotFinalizeReplacementLease()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox());
        var domainEvent = new AccountRenamed { Name = "fenced" };
        var entity = new HistoryAccount();
        entity.DomainEvents.Register(domainEvent);
        await host.Repository.InsertAsync(entity);
        var replacement = Guid.NewGuid();
        host.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await host.NativeAsync(db =>
            {
                var collection = db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox");
                var row = collection.FindById(domainEvent.EventId);
                row.LeaseToken = replacement;
                row.LeaseUntilTicks = DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks;
                return collection.Update(row);
            }, true);
            return (IResult)Result.Success();
        });
        await host.Services.GetRequiredService<IOutboxDomainEventWorker>().ProcessAsync();
        var stored = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
        stored.LeaseToken.ShouldBe(replacement);
        stored.ProcessedTicks.ShouldBeNull();
    }

    [Fact]
    public async Task CaptureAndDelivery_PreserveRequestContextAcrossNativeThread()
    {
        await using var host = new LiteDbFeatureTestHost(s =>
        {
            s.AddLiteDbDomainEventOutbox();
            s.AddLiteDbChangeHistory(o => o.Track<HistoryAccount>().CaptureChanges());
        });
        using var activity = new Activity("fictional request").Start();
        activity.AddBaggage(ActivityConstants.CorrelationIdTagKey, "correlation-17");
        activity.AddBaggage(ActivityConstants.FlowIdTagKey, "flow-23");
        var entity = new HistoryAccount { Name = "context" };
        entity.DomainEvents.Register(new AccountRenamed { Name = entity.Name });
        await host.Repository.InsertAsync(entity);
        var history = host.Scope.ServiceProvider.GetRequiredService<IChangeHistoryService<HistoryAccount, LiteDbContext>>();
        var row = (await history.FindAllAsync(new() { PropertyName = "Name" })).Value.Values.Single();
        row.CorrelationId.ShouldBe("correlation-17");
        row.FlowId.ShouldBe("flow-23");
        var observed = false;
        host.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Activity.Current!.GetBaggageItem(ActivityConstants.CorrelationIdTagKey).ShouldBe("correlation-17");
            Activity.Current.GetBaggageItem("FlowId").ShouldBe("flow-23");
            observed = true;
            return Task.FromResult<IResult>(Result.Success());
        });
        await host.Services.GetRequiredService<IOutboxDomainEventWorker>().ProcessAsync();
        observed.ShouldBeTrue();
    }

    [Fact]
    public async Task ImmediateHostedDelivery_UsesCommitSignalsAlongsideExistingPollingService()
    {
        using var started = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(started.Token);
        await using var host = new LiteDbFeatureTestHost(s =>
        {
            s.AddSingleton(lifetime);
            s.AddLiteDbDomainEventOutbox(configure: o =>
            {
                o.ProcessingMode = OutboxDomainEventProcessMode.Immediate;
                o.StartupDelay = TimeSpan.Zero;
                o.ProcessingInterval = TimeSpan.FromHours(1);
            });
        });
        var hosted = host.Services.GetServices<IHostedService>().ToArray();
        hosted.ShouldContain(service => service is OutboxDomainEventService);
        foreach (var service in hosted) { await service.StartAsync(CancellationToken.None); }

        started.Cancel();
        try
        {
            var domainEvent = new AccountRenamed { Name = "immediate" };
            var entity = new HistoryAccount();
            entity.DomainEvents.Register(domainEvent);
            await host.Repository.InsertAsync(entity);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            LiteDbOutboxDomainEvent row;
            do
            {
                await Task.Delay(10, timeout.Token);
                row = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
            }

            while (!row.ProcessedTicks.HasValue);
            row.Attempts.ShouldBe(1);
        }
        finally
        {
            foreach (var service in hosted.Reverse()) { await service.StopAsync(CancellationToken.None); }
        }
    }

    [Fact]
    public async Task BulkCapture_RequiresExplicitDecorator_AndCommitsWithInsertedRows()
    {
        await using var host = new LiteDbFeatureTestHost(s =>
        {
            s.AddLiteDbDomainEventOutbox();
            s.AddLiteDbEntityBulkInserter<HistoryAccount>(configure: o => o.CollectionName = "accounts")
                .WithBehavior((inner, sp) => new LiteDbBulkOutboxBehavior<HistoryAccount>(inner, sp.GetRequiredKeyedService<LiteDbDatabaseSession>("default")));
        });
        var entity = new HistoryAccount { Name = "imported" };
        entity.DomainEvents.Register(new AccountRenamed { Name = entity.Name });
        var sut = host.Scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<HistoryAccount>>();
        (await sut.InsertAsync([entity])).IsSuccess.ShouldBeTrue();
        entity.DomainEvents.GetAll().ShouldBeEmpty();
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").Count())).ShouldBe(1);
    }

    [Fact]
    public async Task ExpiredLease_ClaimsAgainAndPreservesEventIdentity()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox());
        var domainEvent = new AccountRenamed { Name = "recover" };
        var entity = new HistoryAccount();
        entity.DomainEvents.Register(domainEvent);
        await host.Repository.InsertAsync(entity);
        await host.NativeAsync(db =>
        {
            var collection = db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox");
            var row = collection.FindById(domainEvent.EventId);
            row.LeaseToken = Guid.NewGuid();
            row.LeaseUntilTicks = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
            row.Attempts = 1;
            return collection.Update(row);
        }, true);
        await host.Services.GetRequiredService<IOutboxDomainEventWorker>().ProcessAsync();
        var row = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
        row.Attempts.ShouldBe(2);
        row.ProcessedTicks.ShouldNotBeNull();
        await host.Notifier.Received(1).PublishDynamicAsync(Arg.Is<INotification>(n => n.NotificationId == domainEvent.EventId), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_FailedResult_RemainsPendingUntilRetryLimit()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox(configure: o => o.RetryCount = 2));
        var domainEvent = new AccountRenamed { Name = "reject" };
        var entity = new HistoryAccount();
        entity.DomainEvents.Register(domainEvent);
        await host.Repository.InsertAsync(entity);
        host.Notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResult>(Result.Failure(new InvalidOperationError("rejected"))));
        var sut = host.Services.GetRequiredService<IOutboxDomainEventWorker>();
        await sut.ProcessAsync();
        var row = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
        row.ProcessedTicks.ShouldBeNull();
        await sut.ProcessAsync();
        row = await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindById(domainEvent.EventId));
        row.Attempts.ShouldBe(2);
        row.ProcessedTicks.ShouldNotBeNull();
        row.LastError.ShouldContain("rejected");
    }

    [Fact]
    public async Task Reopen_PendingEvents_RemainAvailableAndArchiveAfterDelivery()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-outbox-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "events.db");
            await using (var first = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox(), file))
            {
                var entity = new HistoryAccount();
                entity.DomainEvents.Register(new AccountRenamed { Name = "durable" });
                await first.Repository.InsertAsync(entity);
            }

            await using var reopened = new LiteDbFeatureTestHost(s => s.AddLiteDbDomainEventOutbox(configure: o => o.AutoArchiveAfter = TimeSpan.Zero), file);
            var sut = reopened.Services.GetRequiredService<IOutboxDomainEventWorker>();
            await sut.ProcessAsync();
            await sut.ArchiveAsync();
            (await reopened.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").FindAll().Single())).IsArchived.ShouldBeTrue();
        }
        finally { directory.Delete(true); }
    }
}
