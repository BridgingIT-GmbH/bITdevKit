// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain.Model;

public class Host : AuditableAggregateRoot<HostId, Guid>
{
    private readonly List<MenuId> menuIds = new();
    private readonly List<DinnerId> dinnerIds = new();

    private Host()
    {
    }

    private Host(
        string firstName,
        string lastName,
        UserId userId,
        AverageRating averageRating,
        Uri profileImage = null)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.UserId = userId;
        this.AverageRating = averageRating;
        this.ProfileImage = profileImage;
    }

    public IReadOnlyList<MenuId> MenuIds => this.menuIds.AsReadOnly();

    public IReadOnlyList<DinnerId> DinnerIds => this.dinnerIds.AsReadOnly();

    public UserId UserId { get; private set; }

    public AverageRating AverageRating { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public Uri ProfileImage { get; private set; } // TODO: use url value object

    public static Host Create(
        string firstName,
        string lastName,
        UserId userId,
        Uri profileImage = null)
    {
        return new Host(
            firstName,
            lastName,
            userId,
            AverageRating.Create(),
            profileImage);
    }

    public void ChangeName(string firstName, string lastName)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(firstName, nameof(firstName));
        EnsureArg.IsNotNull(lastName, nameof(lastName));

        this.FirstName = firstName;
        this.LastName = lastName;
    }
}