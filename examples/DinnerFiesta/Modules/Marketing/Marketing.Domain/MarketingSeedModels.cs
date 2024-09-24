// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;

using Common;

public static class MarketingSeedModels
{
    public static IEnumerable<Customer> Customers(long ticks)
    {
        return new[]
        {
            Customer.Create($"John{ticks}", $"Doe{ticks}", $"jdoe{ticks}@gmail.com"),
            // 06.06 - id = 7b978699-9406-7a9b-db9e-fdac969e5b40
            // 07.06 - id = 3b0f0156-bd89-4ca3-bf14-2be79f23299e + b2936bb0-2644-41d7-94cb-63ca48efd560
            Customer.Create($"Erik{ticks}", $"Larsson{ticks}", $"larsson{ticks}@gmail.com"),
            Customer.Create($"Sophie{ticks}", $"Andersen{ticks}", $"sopa{ticks}@gmail.com"),
            Customer.Create($"Isabella{ticks}", $"Müller{ticks}", $"isam{ticks}@gmail.com"),
            Customer.Create($"Matthias{ticks}", $"Schmidt{ticks}", $"matties{ticks}@gmail.com"),
            Customer.Create($"Li{ticks}", $"Wei{ticks}", $"liwei{ticks}@gmail.com"),
            Customer.Create($"Jane{ticks}", $"Smith{ticks}", $"js{ticks}@gmail.com"),
            Customer.Create($"Emily{ticks}", $"Johnson{ticks}", $"emjo{ticks}@gmail.com")
        }.ForEach(c => c.Id = CustomerId.Create($"{GuidGenerator.Create($"Customer_{c.Email.Value}_{ticks}")}"));
    }
}