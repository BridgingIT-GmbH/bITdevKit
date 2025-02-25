﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class Dinner : AuditableAggregateRoot<DinnerId, Guid>
{
    private readonly List<DinnerReservation> reservations = [];

    private Dinner() { }

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

    public DateTimeOffset? StartedDateTime { get; }

    public DateTimeOffset? EndedDateTime { get; }

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
        var dinner = new Dinner(name,
            description,
            schedule,
            location,
            isPublic,
            maxGuests,
            menuId,
            hostId,
            price,
            imageUrl);

        dinner.DomainEvents.Register(new DinnerCreatedDomainEvent(dinner));

        return dinner;
    }

    public Dinner ChangeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return this;
        }

        this.Name = name;

        this.DomainEvents.Register(new DinnerUpdatedDomainEvent(this), true);

        return this;
    }

    public Dinner ChangeSchedule(DinnerSchedule schedule)
    {
        if (schedule is null)
        {
            return this;
        }

        Rule.Add(
            DinnerRules.ScheduleShouldBeValid(schedule.StartDateTime, schedule.EndDateTime))
            .Check();

        this.Schedule = schedule;

        this.DomainEvents.Register(new DinnerUpdatedDomainEvent(this), true);

        return this;
    }

    public Dinner SetStatus(DinnerStatus status)
    {
        if (status is null)
        {
            return this;
        }

        Rule.Add().Check();

        this.Status = status;

        this.DomainEvents.Register(new DinnerUpdatedDomainEvent(this), true);

        return this;
    }
}