// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerUpdatedDomainEvent : DomainEventBase
{
    public DinnerUpdatedDomainEvent() { } // needed for outbox deserialization

    public DinnerUpdatedDomainEvent(Dinner dinner)
    {
        EnsureArg.IsNotNull(dinner, nameof(dinner));

        this.Name = dinner.Name;
        this.DinnerId = dinner.Id?.Value ?? Guid.Empty;
        this.HostId = dinner.HostId?.Value ?? Guid.Empty;
        this.MenuId = dinner.MenuId?.Value ?? Guid.Empty;
    }

    public string Name { get; set; }

    //public DinnerId DinnerId { get; } // disabled due to json outbox deserialization issues (null)

    //public HostId HostId { get; } // disabled due to json outbox deserialization issues (null)

    //public MenuId MenuId { get; } // disabled due to json outbox deserialization issues (null)

    public Guid DinnerId { get; set; }

    public Guid HostId { get; set; }

    public Guid MenuId { get; set; }
}