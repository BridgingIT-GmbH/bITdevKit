// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain.Model;

public class MenuReview : AuditableAggregateRoot<MenuReviewId, Guid>
{
    private MenuReview()
    {
    }

    private MenuReview(
        Rating rating,
        string comment,
        HostId hostId,
        MenuId menuId,
        GuestId guestId,
        DinnerId dinnerId)
    {
        this.Rating = rating;
        this.Comment = comment;
        this.HostId = hostId;
        this.MenuId = menuId;
        this.GuestId = guestId;
        this.DinnerId = dinnerId;
    }

    public HostId HostId { get; private set; }

    public DinnerId DinnerId { get; private set; }

    public MenuId MenuId { get; private set; }

    public GuestId GuestId { get; private set; }

    public Rating Rating { get; private set; }

    public string Comment { get; private set; }

    public static MenuReview Create(
        int rating,
        string comment,
        HostId hostId,
        MenuId menuId,
        GuestId guestId,
        DinnerId dinnerId)
    {
        return new MenuReview(
            Rating.Create(rating),
            comment,
            hostId,
            menuId,
            guestId,
            dinnerId);
    }
}
