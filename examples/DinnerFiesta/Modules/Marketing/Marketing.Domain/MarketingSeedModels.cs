// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using BridgingIT.DevKit.Common;

public static class MarketingSeedModels
{
    public static IEnumerable<Customer> Customers(long ticks) =>
        new[]
        {
            Customer.Create($"John{ticks}", $"Doe{ticks}", $"jdoe{ticks}@gmail.com"),
            Customer.Create($"Erik{ticks}", $"Larsson{ticks}", $"larsson{ticks}@gmail.com"),
            Customer.Create($"Sophie{ticks}", $"Andersen{ticks}", $"sopa{ticks}@gmail.com"),
            Customer.Create($"Isabella{ticks}", $"Müller{ticks}", $"isam{ticks}@gmail.com"),
            Customer.Create($"Matthias{ticks}", $"Schmidt{ticks}", $"matties{ticks}@gmail.com"),
            Customer.Create($"Li{ticks}", $"Wei{ticks}", $"liwei{ticks}@gmail.com"),
            Customer.Create($"Jane{ticks}", $"Smith{ticks}", $"js{ticks}@gmail.com"),
            Customer.Create($"Emily{ticks}", $"Johnson{ticks}", $"emjo{ticks}@gmail.com")
        }.ForEach(u => u.Id = CustomerId.Create($"{GuidGenerator.Create($"Customer_{u.Email.Value}_{ticks}")}"));
}