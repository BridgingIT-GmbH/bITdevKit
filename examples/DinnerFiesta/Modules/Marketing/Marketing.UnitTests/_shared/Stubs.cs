// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.UnitTests;

using Domain;

public static class Stubs
{
    public static IEnumerable<Customer> Customers(long ticks)
    {
        return MarketingSeedModels.Customers(ticks);
    }
}