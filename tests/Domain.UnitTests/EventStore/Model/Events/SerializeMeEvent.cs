// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;

using BridgingIT.DevKit.Domain.EventSourcing;

public class SerializeMeEvent : DomainEventWithGuid
{
    public string Value { get; set; }

    public static SerializeMeEvent Create(string value)
    {
        var ret = new SerializeMeEvent();
        ret.Value = value;

        return ret;
    }
}