// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;
using Domain.Outbox;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Infrastructure")]
public class OutboxDomainEventWorkerTests(StubDbContextFixture fixture) : IClassFixture<StubDbContextFixture>
{
    private readonly StubDbContextFixture fixture = fixture;

    [Fact]
    public async Task ProcessAsync_WithCompetingWorkers_OnlyOneLeaseIsAcquired()
    {
        await this.ResetContext();

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueProcessing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishCount = 0;
        var notifier = Substitute.For<INotifier>();
        notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => this.PublishAsync(started, continueProcessing, () => Interlocked.Increment(ref publishCount), callInfo.ArgAt<CancellationToken>(2)));

        await this.SeedOutboxEventAsync(new PersonDomainEventStub(DateTime.UtcNow.Ticks));

        await using var provider = this.CreateProvider(notifier);
        var options = this.CreateOptions();
        var worker1 = new OutboxDomainEventWorker<StubDbContext>(NullLoggerFactory.Instance, provider, options: options);
        var worker2 = new OutboxDomainEventWorker<StubDbContext>(NullLoggerFactory.Instance, provider, options: options);

        var task1 = worker1.ProcessAsync(cancellationToken: CancellationToken.None);
        var task2 = worker2.ProcessAsync(cancellationToken: CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        continueProcessing.TrySetResult(true);
        await Task.WhenAll(task1, task2);

        publishCount.ShouldBe(1);

        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.OutboxDomainEvents.Single();
        stored.ProcessedDate.ShouldNotBeNull();
        stored.LockedBy.ShouldBeNull();
        stored.LockedUntil.ShouldBeNull();
        stored.LastError.ShouldBeNull();
        stored.Properties[OutboxDomainEventPropertyConstants.ProcessStatusKey].ToString().ShouldBe("Success");
    }

    [Fact]
    public async Task ProcessAsync_WithLongRunningHandler_RenewsLease()
    {
        await this.ResetContext();

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueProcessing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifier = Substitute.For<INotifier>();
        notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => this.PublishAsync(started, continueProcessing, null, callInfo.ArgAt<CancellationToken>(2)));

        await this.SeedOutboxEventAsync(new PersonDomainEventStub(DateTime.UtcNow.Ticks));

        await using var provider = this.CreateProvider(notifier);
        var options = this.CreateOptions();
        options.LeaseDuration = TimeSpan.FromMilliseconds(80);
        options.LeaseRenewalInterval = TimeSpan.FromMilliseconds(20);
        var worker = new OutboxDomainEventWorker<StubDbContext>(NullLoggerFactory.Instance, provider, options: options);

        var processingTask = worker.ProcessAsync(cancellationToken: CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        DateTimeOffset? initialLockedUntil;
        using (var inspectContext = this.fixture.CreateContext())
        {
            var stored = inspectContext.OutboxDomainEvents.Single();
            stored.LockedBy.ShouldNotBeNullOrWhiteSpace();
            stored.LockedUntil.ShouldNotBeNull();
            stored.ProcessingStartedDate.ShouldNotBeNull();
            initialLockedUntil = stored.LockedUntil;
        }

        await Task.Delay(200);

        using (var inspectContext = this.fixture.CreateContext())
        {
            var stored = inspectContext.OutboxDomainEvents.Single();
            stored.LockedUntil.ShouldNotBeNull();
            stored.LockedUntil.Value.ShouldBeGreaterThan(initialLockedUntil.Value);
        }

        continueProcessing.TrySetResult(true);
        await processingTask;

        using var assertContext = this.fixture.CreateContext();
        var finalStored = assertContext.OutboxDomainEvents.Single();
        finalStored.ProcessedDate.ShouldNotBeNull();
        finalStored.LockedBy.ShouldBeNull();
        finalStored.LockedUntil.ShouldBeNull();
        finalStored.LastError.ShouldBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_WhenAutoArchiveAfterIsReached_ArchivesProcessedEvent()
    {
        await this.ResetContext();

        await this.SeedOutboxEventAsync(
            new PersonDomainEventStub(DateTime.UtcNow.Ticks),
            outboxEvent =>
            {
                outboxEvent.ProcessedDate = DateTimeOffset.UtcNow.AddMinutes(-5);
                outboxEvent.Properties[OutboxDomainEventPropertyConstants.ProcessStatusKey] = "Success";
            });

        await using var provider = this.CreateProvider(Substitute.For<INotifier>());
        var options = this.CreateOptions();
        options.AutoArchiveAfter = TimeSpan.FromMinutes(1);
        var worker = new OutboxDomainEventWorker<StubDbContext>(NullLoggerFactory.Instance, provider, options: options);

        await worker.ArchiveAsync(CancellationToken.None);

        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.OutboxDomainEvents.Single();
        stored.IsArchived.ShouldBeTrue();
        stored.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_WhenProcessedEventIsImmediatelyArchivable_ArchivesAfterProcessing()
    {
        await this.ResetContext();

        var notifier = Substitute.For<INotifier>();
        notifier.PublishDynamicAsync(Arg.Any<INotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IResult>(Result.Success()));

        await this.SeedOutboxEventAsync(new PersonDomainEventStub(DateTime.UtcNow.Ticks));

        await using var provider = this.CreateProvider(notifier);
        var options = this.CreateOptions();
        options.AutoArchiveAfter = TimeSpan.Zero;
        var worker = new OutboxDomainEventWorker<StubDbContext>(NullLoggerFactory.Instance, provider, options: options);

        await worker.ProcessAsync(cancellationToken: CancellationToken.None);
        await worker.ArchiveAsync(CancellationToken.None);

        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.OutboxDomainEvents.Single();
        stored.ProcessedDate.ShouldNotBeNull();
        stored.IsArchived.ShouldBeTrue();
        stored.ArchivedDate.ShouldNotBeNull();
    }

    private OutboxDomainEventOptions CreateOptions()
    {
        return new OutboxDomainEventOptions
        {
            Serializer = new SystemTextJsonSerializer(),
            RetryCount = 3,
            ProcessingCount = 10,
            LeaseDuration = TimeSpan.FromSeconds(1),
            LeaseRenewalInterval = TimeSpan.FromMilliseconds(100)
        };
    }

    private async Task SeedOutboxEventAsync(PersonDomainEventStub domainEvent, Action<OutboxDomainEvent> configure = null)
    {
        await using var context = this.fixture.CreateContext();
        var outboxEvent = new OutboxDomainEvent
        {
            Id = Guid.NewGuid(),
            EventId = domainEvent.EventId.ToString(),
            Type = domainEvent.GetType().AssemblyQualifiedNameShort(),
            Content = new SystemTextJsonSerializer().SerializeToString(domainEvent),
            ContentHash = HashHelper.Compute(domainEvent),
            CreatedDate = domainEvent.Timestamp
        };
        configure?.Invoke(outboxEvent);
        context.OutboxDomainEvents.Add(outboxEvent);
        await context.SaveChangesAsync();
    }

    private ServiceProvider CreateProvider(INotifier notifier)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(notifier);
        services.AddScoped<StubDbContext>(_ => this.fixture.CreateContext());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private async Task ResetContext()
    {
        this.fixture.Context.ChangeTracker.Clear();

        await using var context = this.fixture.CreateContext();
        context.OutboxDomainEvents.RemoveRange(context.OutboxDomainEvents.ToList());
        await context.SaveChangesAsync();
    }

    private async Task<IResult> PublishAsync(
        TaskCompletionSource<bool> started,
        TaskCompletionSource<bool> continueProcessing,
        Action onPublish,
        CancellationToken cancellationToken)
    {
        onPublish?.Invoke();
        started.TrySetResult(true);
        await continueProcessing.Task.WaitAsync(cancellationToken);

        return Result.Success();
    }
}
