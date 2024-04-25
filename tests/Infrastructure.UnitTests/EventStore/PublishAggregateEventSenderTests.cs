// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;
using BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;
using MediatR;

[UnitTest("Infrastructure")]
public class PublishAggregateEventSenderTests
{
    [Fact]
    public void IPublishAggregateEventSender_Class()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.None, requestSender, notifySender,
            outboxSender);
        Assert.NotNull(sender);
    }

    [Fact]
    public async Task IPublishAggregateEventSender_None_DidNotReceive()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.None, requestSender, notifySender,
            outboxSender);
        var evt = new PersonCreatedEvent("Max", "Mustermann");
        var person = new Person(evt);
        await sender.SendProjectionEventAsync(evt, person).AnyContext();
        await sender.SendEventOccuredAsync(evt, person).AnyContext();
        await sender.PublishEventOccuredAsync(evt, person).AnyContext();
        await sender.WriteToOutboxAsync(evt, person).AnyContext();
        await sender.PublishProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishEventOccuredAsync(evt, person).AnyContext();
        await outboxSender.DidNotReceiveWithAnyArgs().WriteToOutboxAsync(evt, person).AnyContext();
    }

    [Fact]
    public async Task IPublishAggregateEventSender_SendProjectionRequestUsingMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.SendProjectionRequestUsingMediator, requestSender, notifySender,
            outboxSender);
        var evt = new PersonCreatedEvent("Max", "Mustermann");
        var person = new Person(evt);
        await sender.SendProjectionEventAsync(evt, person).AnyContext();
        await sender.SendEventOccuredAsync(evt, person).AnyContext();
        await sender.PublishEventOccuredAsync(evt, person).AnyContext();
        await sender.WriteToOutboxAsync(evt, person).AnyContext();
        await sender.PublishProjectionEventAsync(evt, person).AnyContext();
        await requestSender.Received(1).SendProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishProjectionEventAsync(evt, person).AnyContext();
        await outboxSender.DidNotReceiveWithAnyArgs().WriteToOutboxAsync(evt, person).AnyContext();
    }

    [Fact]
    public async Task IPublishAggregateEventSender_NotifiyProjectionRequestUsingMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.NotifyForProjectionUsingMediator, requestSender, notifySender,
            outboxSender);
        var evt = new PersonCreatedEvent("Max", "Mustermann");
        var person = new Person(evt);
        await sender.SendProjectionEventAsync(evt, person).AnyContext();
        await sender.SendEventOccuredAsync(evt, person).AnyContext();
        await sender.PublishEventOccuredAsync(evt, person).AnyContext();
        await sender.WriteToOutboxAsync(evt, person).AnyContext();
        await sender.PublishProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishEventOccuredAsync(evt, person).AnyContext();
        await notifySender.Received(1).PublishProjectionEventAsync(evt, person).AnyContext();
        await outboxSender.DidNotReceiveWithAnyArgs().WriteToOutboxAsync(evt, person).AnyContext();
    }

    [Fact]
    public async Task IPublishAggregateEventSender_SendEventOccuredRequestUsingMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.SendEventOccuredRequestUsingMediator, requestSender, notifySender,
            outboxSender);
        var evt = new PersonCreatedEvent("Max", "Mustermann");
        var person = new Person(evt);
        await sender.SendProjectionEventAsync(evt, person).AnyContext();
        await sender.SendEventOccuredAsync(evt, person).AnyContext();
        await sender.PublishEventOccuredAsync(evt, person).AnyContext();
        await sender.WriteToOutboxAsync(evt, person).AnyContext();
        await sender.PublishProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendProjectionEventAsync(evt, person).AnyContext();
        await requestSender.Received(1).SendEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishProjectionEventAsync(evt, person).AnyContext();
        await outboxSender.DidNotReceiveWithAnyArgs().WriteToOutboxAsync(evt, person).AnyContext();
    }

    [Fact]
    public async Task IPublishAggregateEventSender_NotifyEventOccuredUsingMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.NotifyEventOccuredUsingMediator, requestSender, notifySender,
            outboxSender);
        var evt = new PersonCreatedEvent("Max", "Mustermann");
        var person = new Person(evt);
        await sender.SendProjectionEventAsync(evt, person).AnyContext();
        await sender.SendEventOccuredAsync(evt, person).AnyContext();
        await sender.PublishEventOccuredAsync(evt, person).AnyContext();
        await sender.WriteToOutboxAsync(evt, person).AnyContext();
        await sender.PublishProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendEventOccuredAsync(evt, person).AnyContext();
        await notifySender.Received(1).PublishEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishProjectionEventAsync(evt, person).AnyContext();
        await outboxSender.DidNotReceiveWithAnyArgs().WriteToOutboxAsync(evt, person).AnyContext();
    }

    [Fact]
    public async Task IPublishAggregateEventSender_AddToOutbox()
    {
        var mediator = Substitute.For<IMediator>();
        var requestSender = Substitute.For<IAggregateEventMediatorRequestSender>();
        var notifySender = Substitute.For<IAggregateEventMediatorNotificationSender>();
        var outboxSender = Substitute.For<IAggregateEventOutboxSender>();
        var sender = new PublishAggregateEventSender(mediator, EventStorePublishingModes.AddToOutbox, requestSender, notifySender,
            outboxSender);
        var evt = new PersonCreatedEvent("Max", "Mustermann");
        var person = new Person(evt);
        await sender.SendProjectionEventAsync(evt, person).AnyContext();
        await sender.SendEventOccuredAsync(evt, person).AnyContext();
        await sender.PublishEventOccuredAsync(evt, person).AnyContext();
        await sender.WriteToOutboxAsync(evt, person).AnyContext();
        await sender.PublishProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendProjectionEventAsync(evt, person).AnyContext();
        await requestSender.DidNotReceiveWithAnyArgs().SendEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishEventOccuredAsync(evt, person).AnyContext();
        await notifySender.DidNotReceiveWithAnyArgs().PublishProjectionEventAsync(evt, person).AnyContext();
        await outboxSender.Received(1).WriteToOutboxAsync(evt, person).AnyContext();
    }
}
