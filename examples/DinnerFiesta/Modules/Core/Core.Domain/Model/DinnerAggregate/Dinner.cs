// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

public class Dinner : AuditableAggregateRoot<DinnerId, Guid>
{
    private readonly List<DinnerReservation> reservations = new();

    private Dinner()
    {
    }

    private Dinner(
        string name,
        string description,
        DinnerSchedule schedule,
        DinnerLocation location,
        bool isPublic,
        int maxGuests,
        MenuId menuId,
        HostId hostId,
        Price price,
        Uri imageUrl = null)
    {
        this.Name = name;
        this.Description = description;
        this.Schedule = schedule;
        this.IsPublic = isPublic;
        this.MaxGuests = maxGuests;
        this.Price = price;
        this.MenuId = menuId;
        this.HostId = hostId;
        this.ImageUrl = imageUrl;
        this.Location = location;
        this.Status = DinnerStatus.Draft;
    }

    public HostId HostId { get; private set; }

    public MenuId MenuId { get; private set; }

    public IReadOnlyList<DinnerReservation> Reservations => this.reservations.AsReadOnly();

    public Price Price { get; private set; }

    public DinnerLocation Location { get; private set; }

    public DinnerStatus Status { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public DinnerSchedule Schedule { get; private set; }

    public DateTimeOffset? StartedDateTime { get; private set; }

    public DateTimeOffset? EndedDateTime { get; private set; }

    public Uri ImageUrl { get; private set; } // TODO: use url value object

    public bool IsPublic { get; private set; }

    public int MaxGuests { get; private set; }

    public static Dinner Create(
        string name,
        string description,
        DinnerSchedule schedule,
        DinnerLocation location,
        bool isPublic,
        int maxGuests,
        MenuId menuId,
        HostId hostId,
        Price price,
        Uri imageUrl = null)
    {
        var dinner = new Dinner(
            name,
            description,
            schedule,
            location,
            isPublic,
            maxGuests,
            menuId,
            hostId,
            price,
            imageUrl);

        dinner.DomainEvents.Register(
            new DinnerCreatedDomainEvent(dinner));

        return dinner;
    }

    public Dinner ChangeName(string name)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(name, nameof(name));

        this.Name = name;
        return this;
    }

    public Dinner ChangeSchedule(DinnerSchedule schedule)
    {
        EnsureArg.IsNotNull(schedule, nameof(schedule));

        Check.Throw(new IBusinessRule[]
        {
            new ScheduleShouldBeValidRule(schedule.StartDateTime, schedule.EndDateTime),
        });

        this.Schedule = schedule;
        return this;
    }

    public Dinner SetStatus(DinnerStatus status)
    {
        EnsureArg.IsNotNull(status, nameof(status));

        Check.Throw(new IBusinessRule[]
        {
            // TODO: check certain transitions which may not be allowed
        });

        this.Status = status;
        return this;
    }
}