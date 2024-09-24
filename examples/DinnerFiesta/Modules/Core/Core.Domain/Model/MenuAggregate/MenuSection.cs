// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain.Model;

public class MenuSection : Entity<MenuSectionId, Guid>
{
    private readonly List<MenuSectionItem> items;

    private MenuSection() { }

    private MenuSection(
        string name,
        string description,
        IEnumerable<MenuSectionItem> items)
    {
        this.Name = name;
        this.Description = description;
        this.items = items?.ToList() ?? [];
    }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public IReadOnlyList<MenuSectionItem> Items => this.items.AsReadOnly();

    public static MenuSection Create(
        string name,
        string description,
        IEnumerable<MenuSectionItem> items = null)
    {
        return new MenuSection(name, description, items);
    }
}