﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class GuestRatingId : EntityId<Guid>
{
    private GuestRatingId() { }

    private GuestRatingId(Guid guid)
    {
        this.Value = guid;
    }

    public override Guid Value { get; protected set; }

    public static GuestRatingId Create()
    {
        return new GuestRatingId(Guid.NewGuid());
    }

    public static GuestRatingId Create(Guid value)
    {
        return new GuestRatingId(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}