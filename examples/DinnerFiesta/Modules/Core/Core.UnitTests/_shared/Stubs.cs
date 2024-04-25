// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests;

using System.Collections.Generic;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public static class Stubs
{
    public static IEnumerable<User> Users(long ticks) => CoreSeeds.Users(ticks);

    public static IEnumerable<Host> Hosts(long ticks) => CoreSeeds.Hosts(ticks);

    public static IEnumerable<Menu> Menus(long ticks) => CoreSeeds.Menus(ticks);

    public static IEnumerable<Dinner> Dinners(long ticks) => CoreSeeds.Dinners(ticks);
}