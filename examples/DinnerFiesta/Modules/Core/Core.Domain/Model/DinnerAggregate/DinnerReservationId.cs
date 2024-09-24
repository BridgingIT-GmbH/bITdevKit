// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain.Model;

public class DinnerReservationId : EntityId<Guid>
{
    private DinnerReservationId() { }

    private DinnerReservationId(Guid guid)
    {
        this.Value = guid;
    }

    public override Guid Value { get; protected set; }

    public static DinnerReservationId Create()
    {
        return new DinnerReservationId(Guid.NewGuid());
    }

    public static DinnerReservationId Create(Guid value)
    {
        return new DinnerReservationId(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}