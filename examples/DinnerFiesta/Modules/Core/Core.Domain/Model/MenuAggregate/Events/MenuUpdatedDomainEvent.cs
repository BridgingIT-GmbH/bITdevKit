﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;

public class MenuUpdatedDomainEvent : DomainEventBase
{
    public MenuUpdatedDomainEvent(Menu menu)
    {
        this.Menu = menu;
    }

    public Menu Menu { get; }
}