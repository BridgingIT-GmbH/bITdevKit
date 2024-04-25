// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using BridgingIT.DevKit.Domain.EventSourcing;

[UnitTest("Infrastructure")]
public class DomainEventTests
{
    [Fact]
    public void DomainEventWithAggregateId()
    {
        const int aggregateId = 10;
        var sut = new DomainEvent<int>(aggregateId);
        sut.ShouldNotBeNull();
        sut.EventId.ToString().ShouldNotBeNullOrEmpty();
        sut.AggregateId.ShouldBe(aggregateId);
    }

    [Fact]
    public void DomainEventWithoutExplicitAggregateId()
    {
        var sut = new DomainEvent<int>();
        sut.ShouldNotBeNull();
        sut.EventId.ToString().ShouldNotBeNullOrEmpty();
        sut.AggregateId.ShouldBe(0);
    }
}