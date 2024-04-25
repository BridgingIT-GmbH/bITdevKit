// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain.Model;

public class Menu : AuditableAggregateRoot<MenuId, Guid>
{
    private readonly List<MenuSection> sections = new();
    private readonly List<DinnerId> dinnerIds = new();
    private readonly List<MenuReviewId> menuReviewIds = new();

    private Menu()
    {
    }

    private Menu(
        HostId hostId,
        string name,
        string description,
        AverageRating averageRating,
        IEnumerable<MenuSection> sections)
    {
        this.HostId = hostId;
        this.Name = name;
        this.Description = description;
        this.AverageRating = averageRating;
        this.sections = sections?.ToList() ?? new();
    }

    public HostId HostId { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public IReadOnlyList<MenuSection> Sections => this.sections.AsReadOnly();

    public IReadOnlyList<DinnerId> DinnerIds => this.dinnerIds.AsReadOnly();

    public IReadOnlyList<MenuReviewId> MenuReviewIds => this.menuReviewIds.AsReadOnly();

    public AverageRating AverageRating { get; private set; }

    public static Menu Create(
        HostId hostId,
        string name,
        string description,
        IEnumerable<MenuSection> sections = null)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(hostId, nameof(hostId));
        EnsureArg.IsNotNull(name, nameof(name));
        EnsureArg.IsNotNull(description, nameof(description));

        var menu = new Menu(
            hostId,
            name,
            description,
            AverageRating.Create(),
            sections);

        menu.DomainEvents.Register(
            new MenuCreatedDomainEvent(menu));

        return menu;
    }

    public Menu ChangeName(string name)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(name, nameof(name));

        this.Name = name;
        return this;
    }

    public Menu AddDinnerId(DinnerId dinnerId)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(dinnerId, nameof(dinnerId));

        this.dinnerIds.Add(dinnerId);
        return this;
    }

    public Menu AddRating(Rating rating)
    {
        // TODO: replace with Rules
        EnsureArg.IsNotNull(rating, nameof(rating));

        this.AverageRating.Add(rating);
        return this;
    }
}