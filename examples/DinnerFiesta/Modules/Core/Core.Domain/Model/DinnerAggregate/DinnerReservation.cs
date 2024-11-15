// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerReservation : AuditableEntity<DinnerReservationId, Guid>
{
    private DinnerReservation() { }

    private DinnerReservation(
        GuestId guestId,
        int guestCount,
        DateTimeOffset? arrivalDateTime,
        BillId billId,
        DinnerReservationStatus status)
    {
        this.GuestId = guestId;
        this.GuestCount = guestCount;
        this.ArrivalDateTime = arrivalDateTime;
        this.BillId = billId;
        this.Status = status;
    }

    public int GuestCount { get; private set; }

    public GuestId GuestId { get; private set; }

    public BillId BillId { get; private set; }

    public DinnerReservationStatus Status { get; private set; }

    public DateTimeOffset? ArrivalDateTime { get; private set; }

    public static DinnerReservation Create(
        GuestId guestId,
        int guestCount,
        DinnerReservationStatus status,
        BillId billId = null,
        DateTimeOffset? arrivalDateTime = null)
    {
        return new DinnerReservation(guestId,
            guestCount,
            arrivalDateTime,
            billId,
            status);
    }
}