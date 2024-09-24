// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using Domain.EventSourcing.AggregatePublish;
using Domain.UnitTests.EventStore.Model;
using Domain.UnitTests.EventStore.Model.Events;
using EventSourcing.Publishing;
using MediatR;

[UnitTest("Infrastructure")]
public class OutboxTests
{
    private readonly Person demoPerson;
    private readonly PersonCreatedEvent demoPersonEvent;
    private readonly IMediator mediator;

    public OutboxTests()
    {
        this.mediator = Substitute.For<IMediator>();
        this.demoPerson = new Person("a", "b");
        this.demoPersonEvent = new PersonCreatedEvent(this.demoPerson.Surname, this.demoPerson.Firstname);
    }

    [Fact]
    public void PublishAggregateEventSenderCreated()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.None,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        eventSender.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishUnsavedDomainEvent_OutboxSenderNotCalled_None()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.None,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.DidNotReceiveWithAnyArgs()
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishUnsavedDomainEvent_OutboxSenderNotCalled_NotifyUsingMediator()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.NotifyForProjectionUsingMediator,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.DidNotReceiveWithAnyArgs()
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishUnsavedDomainEvent_OutboxSenderNotCalled_SendRequestUsingMediator()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.SendProjectionRequestUsingMediator,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.DidNotReceiveWithAnyArgs()
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishUnsavedDomainEvent_AddToOutbox()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.AddToOutbox,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.Received(1)
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorNotificationSender.DidNotReceiveWithAnyArgs()
            .PublishProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorRequestSender.DidNotReceiveWithAnyArgs()
            .SendProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishUnsavedDomainEvent_AddToOutboxAndSendRequest()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.AddToOutbox | EventStorePublishingModes.SendProjectionRequestUsingMediator,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.Received(1)
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishSavedDomainEvent_AddToOutbox()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.AddToOutbox,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.PublishProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.DidNotReceiveWithAnyArgs()
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorNotificationSender.DidNotReceiveWithAnyArgs()
            .PublishProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorRequestSender.DidNotReceiveWithAnyArgs()
            .SendProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishSavedDomainEvent_SendRequest()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.SendProjectionRequestUsingMediator,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.SendProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.DidNotReceiveWithAnyArgs()
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorNotificationSender.DidNotReceiveWithAnyArgs()
            .PublishProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorRequestSender.Received(1)
            .SendProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }

    [Fact]
    public async Task PublishAggregateEventSender_PublishSavedDomainEvent_Notify()
    {
        var aggregateEventMediatorRequestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var aggregateEventMediatorNotificationSender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var eventSender = new PublishAggregateEventSender(this.mediator,
            EventStorePublishingModes.NotifyForProjectionUsingMediator,
            aggregateEventMediatorRequestSender,
            aggregateEventMediatorNotificationSender,
            outboxSender);
        await eventSender.PublishProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        eventSender.ShouldNotBeNull();
        await outboxSender.DidNotReceiveWithAnyArgs()
            .WriteToOutboxAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorNotificationSender.Received(1)
            .PublishProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
        await aggregateEventMediatorRequestSender.DidNotReceiveWithAnyArgs()
            .SendProjectionEventAsync(this.demoPersonEvent, this.demoPerson)
            .AnyContext();
    }
}