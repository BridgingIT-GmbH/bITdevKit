// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain;

public class HostUpdatedDomainEvent : DomainEventBase
{
    public HostUpdatedDomainEvent() { } // needed for outbox deserialization

    public HostUpdatedDomainEvent(Host host)
    {
        EnsureArg.IsNotNull(host, nameof(host));

        //this.HostId = HostId.Create(host.Id.Value);
    }

    public HostId HostId { get; }
}