// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class MenuSectionItem : Entity<MenuSectionItemId, Guid>
{
    private MenuSectionItem() { }

    private MenuSectionItem(string name, string description)
    {
        this.Name = name;
        this.Description = description;
    }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public static MenuSectionItem Create(string name, string description)
    {
        return new MenuSectionItem(name, description);
    }
}