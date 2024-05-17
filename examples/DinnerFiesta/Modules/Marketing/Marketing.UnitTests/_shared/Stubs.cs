// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.UnitTests;

using System.Collections.Generic;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

public static class Stubs
{
    public static IEnumerable<Customer> Customers(long ticks) => MarketingSeedModels.Customers(ticks);
}