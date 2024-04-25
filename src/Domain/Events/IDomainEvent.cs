// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using MediatR;

public interface IDomainEvent : INotification // TODO: move to Domain.Mediator
{
    Guid EventId { get; }

    DateTimeOffset Timestamp { get; }
}