// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Repositories.BulkInserter;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;

[UnitTest("Domain")]
public class EntityBulkInserterBehaviorTests
{
    [Theory]
    [InlineData(AuditStateByType.ByUserName, "ada", "ada@example.test", "42", "ada")]
    [InlineData(AuditStateByType.ByEmail, "ada", "ada@example.test", "42", "ada@example.test")]
    [InlineData(AuditStateByType.ByUserId, "ada", "ada@example.test", "42", "42")]
    public async Task AuditState_UsesConfiguredCurrentUserValue(
        AuditStateByType byType,
        string userName,
        string email,
        string userId,
        string expected)
    {
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.UserName.Returns(userName);
        accessor.Email.Returns(email);
        accessor.UserId.Returns(userId);
        var terminal = new CapturingBulkInserter();
        var sut = new EntityBulkInserterAuditStateBehavior<AggregateEntity>(terminal,
            new EntityBulkInserterAuditStateBehaviorOptions { ByType = byType }, accessor);
        var entity = new AggregateEntity();

        await sut.InsertAsync([entity]);

        entity.AuditState.CreatedBy.ShouldBe(expected);
        terminal.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Cancellation_DoesNotMaterializeOrForwardCancelledBatch()
    {
        var terminal = new CapturingBulkInserter();
        var sut = new EntityBulkInserterCancellationBehavior<AggregateEntity>(terminal);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var enumerated = false;

        IEnumerable<AggregateEntity> Entities()
        {
            enumerated = true;
            yield return new AggregateEntity();
        }

        await Should.ThrowAsync<OperationCanceledException>(() => sut.InsertAsync(Entities(), source.Token));

        enumerated.ShouldBeFalse();
        terminal.InvocationCount.ShouldBe(0);
    }

    [Fact]
    public async Task Concurrency_AssignsOneFreshValueAndForwardsOnce()
    {
        var terminal = new CapturingBulkInserter();
        var sut = new EntityBulkInserterConcurrencyBehavior<AggregateEntity>(terminal);
        var entity = new AggregateEntity { ConcurrencyVersion = Guid.NewGuid() };

        await sut.InsertAsync([entity]);

        entity.ConcurrencyVersion.ShouldNotBe(Guid.Empty);
        terminal.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task DomainEvent_CreatesEventsBeforeEventMetricsAndFiltersNulls()
    {
        var terminal = new CapturingBulkInserter();
        var metrics = new EntityBulkInserterDomainEventMetricsBehavior<AggregateEntity>(terminal);
        var sut = new EntityBulkInserterDomainEventBehavior<AggregateEntity>(metrics);
        var entity = new AggregateEntity();

        await sut.InsertAsync([entity, null]);

        entity.DomainEvents.GetAll().Count().ShouldBe(1);
        terminal.Entities.ShouldBe([entity]);
        terminal.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publisher_ClearsEventsOnlyAfterSuccessfulPublication()
    {
        var terminal = new CapturingBulkInserter();
        var publisher = Substitute.For<IDomainEventPublisher>();
        publisher.Send(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResult>(Result.Success()));
        var sut = new EntityBulkInserterDomainEventPublisherBehavior<AggregateEntity>(publisher, terminal);
        var entity = new AggregateEntity();
        entity.DomainEvents.Register(new EntityCreatedDomainEvent<AggregateEntity>(entity));

        var result = await sut.InsertAsync([entity]);

        result.IsSuccess.ShouldBeTrue();
        entity.DomainEvents.GetAll().ShouldBeEmpty();
        await publisher.Received(1).Send(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publisher_RetainsEventsWhenPublicationFails()
    {
        var terminal = new CapturingBulkInserter();
        var publisher = Substitute.For<IDomainEventPublisher>();
        publisher.Send(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResult>(Result.Failure()));
        var sut = new EntityBulkInserterDomainEventPublisherBehavior<AggregateEntity>(publisher, terminal);
        var entity = new AggregateEntity();
        entity.DomainEvents.Register(new EntityCreatedDomainEvent<AggregateEntity>(entity));

        var result = await sut.InsertAsync([entity]);

        result.IsFailure.ShouldBeTrue();
        entity.DomainEvents.GetAll().Count().ShouldBe(1);
        terminal.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Logging_DoesNotIncludeEntityPayload()
    {
        var provider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var terminal = new CapturingBulkInserter();
        var sut = new EntityBulkInserterLoggingBehavior<AggregateEntity>(loggerFactory, terminal);

        await sut.InsertAsync([new AggregateEntity { Secret = "sensitive-value" }]);

        provider.Messages.ShouldNotContain(message => message.Contains("sensitive-value", StringComparison.Ordinal));
        terminal.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Metrics_CompletesCurrentAndRecordsDuration()
    {
        using var meterFactory = new TestMeterFactory();
        using var listener = new MeterListener();
        var current = 0L;
        var durations = 0;
        listener.InstrumentPublished = (instrument, meterListener) => meterListener.EnableMeasurementEvents(instrument);
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "bulk_inserter_insert_current")
            {
                current += measurement;
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
        {
            if (instrument.Name == "bulk_inserter_insert_duration")
            {
                durations++;
            }
        });
        listener.Start();
        var sut = new EntityBulkInserterMetricsBehavior<AggregateEntity>(
            new CapturingBulkInserter(),
            new MetricsService(meterFactory));

        await sut.InsertAsync([new AggregateEntity()]);

        current.ShouldBe(0);
        durations.ShouldBe(1);
    }

    [Fact]
    public async Task Tracing_RecordsSuccessfulOperationStatus()
    {
        Activity stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "BridgingIT.DevKit.EntityBulkInserter",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var sut = new EntityBulkInserterTracingBehavior<AggregateEntity>(new CapturingBulkInserter());

        await sut.InsertAsync([new AggregateEntity()]);

        stopped.ShouldNotBeNull();
        stopped.Status.ShouldBe(ActivityStatusCode.Ok);
        stopped.GetTagItem("bulk_inserter.inserted_count").ShouldBe(1L);
    }

    private sealed class CapturingBulkInserter : IEntityBulkInserter<AggregateEntity>
    {
        public int InvocationCount { get; private set; }

        public IReadOnlyList<AggregateEntity> Entities { get; private set; } = [];

        public Task<Result<long>> InsertAsync(
            IEnumerable<AggregateEntity> entities,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            this.Entities = entities.ToArray();
            return Task.FromResult(Result<long>.Success(this.Entities.Count));
        }
    }

    private sealed class AggregateEntity : Entity<Guid>, IAggregateRoot, IAuditable, IConcurrency
    {
        public string Secret { get; set; }

        public DomainEvents DomainEvents { get; } = new();

        public AuditState AuditState { get; set; }

        public Guid ConcurrencyVersion { get; set; }
    }

    private sealed class TestMeterFactory : IMeterFactory, IDisposable
    {
        private readonly List<Meter> meters = [];

        public Meter Create(MeterOptions options) => this.Create(options.Name, options.Version, options.Tags);

        public Meter Create(string name, string version = null, IEnumerable<KeyValuePair<string, object>> tags = null)
        {
            var meter = new Meter(name, version, tags);
            this.meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in this.meters)
            {
                meter.Dispose();
            }
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this.Messages);

        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }
}
