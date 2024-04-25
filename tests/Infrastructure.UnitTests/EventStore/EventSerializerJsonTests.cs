// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using System.IO;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;

[UnitTest("Infrastructure")]
public class EventSerializerJsonTests
{
    [Fact]
    public void TestSerializeAggregateEvent()
    {
        var serializer = new JsonNetSerializer();
        var @event = new PersonCreatedEvent("Tobias", "Meier");

        var buffer = Array.Empty<byte>();
        using (var mem = new MemoryStream())
        {
            serializer.Serialize(@event, mem);
            buffer = mem.ToArray();
        }

        buffer.Length.ShouldBeGreaterThan(0);

        object resultObj = null;
        using (var mem = new MemoryStream(buffer))
        {
            resultObj = serializer.Deserialize(mem, @event.GetType());
        }

        resultObj.ShouldNotBeNull();
        var result = resultObj as PersonCreatedEvent;
        result.ShouldNotBeNull();
        result.Firstname.ShouldBe(@event.Firstname);
        result.Surname.ShouldBe(@event.Surname);
        result.AggregateId.ShouldBe(@event.AggregateId);
    }

    [Fact]
    public void TestSerializeEvent()
    {
        const string value = "meinValue";

        var serializer = new JsonNetSerializer();
        var @event = SerializeMeEvent.Create(value);

        var buffer = Array.Empty<byte>();
        using (var mem = new MemoryStream())
        {
            serializer.Serialize(@event, mem);
            buffer = mem.ToArray();
        }

        buffer.Length.ShouldBeGreaterThan(0);

        object resultObj = null;
        using (var mem = new MemoryStream(buffer))
        {
            resultObj = serializer.Deserialize(mem, @event.GetType());
        }

        resultObj.ShouldNotBeNull();
        var result = resultObj as SerializeMeEvent;
        result.ShouldNotBeNull();
        result.Value.ShouldBe(value);
    }
}