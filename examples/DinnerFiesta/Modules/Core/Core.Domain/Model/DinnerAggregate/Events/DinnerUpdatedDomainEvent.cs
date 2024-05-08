// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;

public class DinnerUpdatedDomainEvent : DomainEventBase
{
    public DinnerUpdatedDomainEvent(Dinner dinner)
    {
        this.Dinner = dinner;
    }

    public Dinner Dinner { get; }
}