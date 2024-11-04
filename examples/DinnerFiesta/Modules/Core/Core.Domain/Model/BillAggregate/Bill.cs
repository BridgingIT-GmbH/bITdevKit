// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class Bill : AuditableAggregateRoot<BillId, Guid>
{
    private Bill() { }

    private Bill(
        HostId hostId,
        DinnerId dinnerId,
        GuestId guestId,
        Price price)
    {
        this.DinnerId = dinnerId;
        this.GuestId = guestId;
        this.HostId = hostId;
        this.Price = price;
    }

    public HostId HostId { get; private set; }

    public DinnerId DinnerId { get; private set; }

    public GuestId GuestId { get; private set; }

    public Price Price { get; private set; }

    public static Bill Create(
        HostId hostId,
        DinnerId dinnerId,
        GuestId guestId,
        Price amount)
    {
        return new Bill(hostId,
            dinnerId,
            guestId,
            amount);
    }
}