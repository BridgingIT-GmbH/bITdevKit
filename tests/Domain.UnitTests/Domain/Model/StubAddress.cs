// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System.Collections.Generic;
using BridgingIT.DevKit.Domain.Model;

public class StubAddress : ValueObject
{
    public string Street { get; set; }

    public string City { get; set; }

    public string PostalCode { get; set; }

    public string HouseNumber { get; set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Street;
        yield return this.City;
        yield return this.PostalCode;
        yield return this.HouseNumber;
    }
}