﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

using MediatR;

public class AggregateEvent : DomainEventWithGuid, IAggregateEvent, INotification
{
    protected AggregateEvent(Guid id, int version)
        : base(id)
    {
        this.AggregateVersion = version;
    }

    private AggregateEvent() { }

    public int AggregateVersion { get; set; }
}