// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain.Model;

public class Guest : AuditableAggregateRoot<GuestId, Guid>
{
    private readonly List<DinnerId> upcomingDinnerIds = new();
    private readonly List<DinnerId> pastDinnerIds = new();
    private readonly List<DinnerId> pendingDinnerIds = new();
    private readonly List<BillId> billIds = new();
    private readonly List<MenuReviewId> menuReviewIds = new();
    private readonly List<GuestRating> ratings = new();

    private Guest()
    {
    }

    private Guest(
        string firstName,
        string lastName,
        UserId userId,
        Uri profileImage = null)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.UserId = userId;
        this.ProfileImage = profileImage;
    }

    public UserId UserId { get; }

    public IReadOnlyList<DinnerId> UpcomingDinnerIds => this.upcomingDinnerIds.AsReadOnly();

    public IReadOnlyList<DinnerId> PastDinnerIds => this.pastDinnerIds.AsReadOnly();

    public IReadOnlyList<DinnerId> PendingDinnerIds => this.pendingDinnerIds.AsReadOnly();

    public IReadOnlyList<BillId> BillIds => this.billIds.AsReadOnly();

    public IReadOnlyList<MenuReviewId> MenuReviewIds => this.menuReviewIds.AsReadOnly();

    public IReadOnlyList<GuestRating> Ratings => this.ratings.AsReadOnly();

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public Uri ProfileImage { get; private set; } // TODO: use url value object

    public static Guest Create(
        string firstName,
        string lastName,
        UserId userId,
        Uri profileImage = null)
    {
        return new Guest(
            firstName,
            lastName,
            userId,
            profileImage);
    }
}
