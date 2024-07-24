// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain.Model;

public class MenuSectionItemId : EntityId<Guid>
{
    private MenuSectionItemId()
    {
    }

    private MenuSectionItemId(Guid guid)
    {
        this.Value = guid;
    }

    public override Guid Value { get; protected set; }

    public static MenuSectionItemId Create()
    {
        return new MenuSectionItemId(Guid.NewGuid());
    }

    public static MenuSectionItemId Create(Guid value)
    {
        return new MenuSectionItemId(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}
