// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain.Model;

public class GuestRating : AuditableEntity<GuestRatingId, Guid>
{
    private GuestRating() { }

    private GuestRating(DinnerId dinnerId, HostId hostId, Rating rating)
    {
        this.DinnerId = dinnerId;
        this.HostId = hostId;
        this.Rating = rating;
    }

    public HostId HostId { get; private set; }

    public DinnerId DinnerId { get; private set; }

    public Rating Rating { get; private set; }

    public static GuestRating Create(DinnerId dinnerId, HostId hostId, int rating)
    {
        return new GuestRating(dinnerId, hostId, Rating.Create(rating));
    }
}