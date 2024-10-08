﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using DevKit.Domain;

public class CustomerCreatedDomainEvent(Customer customer) : DomainEventBase
{
    public Customer Customer { get; } = customer;
}