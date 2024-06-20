// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain.Model;

public class DinnerReservationStatus(int id, string name) : Enumeration(id, name)
{
    public static DinnerReservationStatus PendingGuestApproval = new(1, nameof(PendingGuestApproval));
    public static DinnerReservationStatus Reserved = new(2, nameof(Reserved));
    public static DinnerReservationStatus Cancelled = new(3, nameof(Cancelled));

    public static IEnumerable<DinnerStatus> GetAll() => GetAll<DinnerStatus>();
}